﻿using ACE.Caching;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ACE.WhatIf;

internal class AzureWhatIfHandler
{
    private static readonly Lazy<HttpClient> httpClient = new(() => new HttpClient());

    private readonly string scopeId;
    private readonly string? resourceGroupName;
    private readonly string template;
    private readonly DeploymentMode deploymentMode;
    private readonly string parameters;
    private readonly ILogger logger;
    private readonly CommandType commandType;
    private readonly string? location;
    private readonly LocalCacheHandler? cache;

    public AzureWhatIfHandler(string scopeId,
                              string? resourceGroupName,
                              string template,
                              DeploymentMode deploymentMode,
                              string parameters,
                              ILogger logger,
                              CommandType commandType,
                              string? location,
                              bool disableCache)
    {
        this.scopeId = scopeId;
        this.resourceGroupName = resourceGroupName;
        this.template = template;
        this.deploymentMode = deploymentMode;
        this.parameters = parameters;
        this.logger = logger;
        this.commandType = commandType;
        this.location = location;

        if(disableCache == false)
        {
            this.cache = new LocalCacheHandler(scopeId, resourceGroupName, template, parameters);
        }
    }

    public async Task<WhatIfResponse?> GetResponseWithRetries()
    {
        this.logger.LogInformation("What If status:");
        this.logger.LogInformation("");

        if(this.cache != null)
        {
            var cachedResponse = this.cache.GetCachedData();
            if(cachedResponse != null)
            {
                this.logger.AddEstimatorMessage("What If data loaded from cache.");
                return cachedResponse;
            }

            this.logger.AddEstimatorMessage("Cache miss for What If data.");
        }
        else
        {
            this.logger.AddEstimatorMessage("Cache is disabled.");
        }
        
        var response = await SendInitialRequest();
        if(response.IsSuccessStatusCode == false)
        {
            var result = await response.Content.ReadAsStringAsync();
            this.logger.LogError(result);

            return null;
        }

        var maxRetries = 5;
        var currentRetry = 1;
        
        while (response.StatusCode == HttpStatusCode.Accepted && currentRetry < maxRetries)
        {
            var retryAfterHeader = response.Headers.RetryAfter;
            var retryAfter = retryAfterHeader == null ? TimeSpan.FromSeconds(15) : retryAfterHeader.Delta;

            this.logger.AddEstimatorMessage("Waiting for response from What If API.");
            await Task.Delay(retryAfter.HasValue ? retryAfter.Value.Seconds * 1000 : 15000);

            var location = response.Headers.Location;

            if(location == null)
            {
                throw new Exception("Location header can't be null when awaiting response.");
            }

            response = await SendAndWaitForResponse(location);
            currentRetry++;
        }

#if DEBUG
        var rawData = await response.Content.ReadAsStringAsync();
#endif

        var data = JsonSerializer.Deserialize<WhatIfResponse>(await response.Content.ReadAsStreamAsync());
        if(this.cache != null && data != null)
        {
            this.logger.AddEstimatorMessage("Saving What If response to cache.");
            this.cache.SaveData(data);
        }

        return data;
    }

    private async Task<HttpResponseMessage> SendInitialRequest()
    {
        var token = GetToken();
        var request = new HttpRequestMessage(HttpMethod.Post, CreateUrlBasedOnScope());

        string? templateContent;
        if(this.commandType == CommandType.ResourceGroup)
        {
            templateContent = JsonSerializer.Serialize(new EstimatePayload(this.template, this.deploymentMode, this.parameters), new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        else
        {
            templateContent = JsonSerializer.Serialize(new EstimatePayload(this.template, this.deploymentMode, this.parameters, this.location), new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        }

        request.Content = new StringContent(templateContent, Encoding.UTF8, "application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.Value.SendAsync(request);
        return response;
    }

    private string CreateUrlBasedOnScope()
    {
        return this.commandType switch
        {
            CommandType.ResourceGroup => $"https://management.azure.com/subscriptions/{this.scopeId}/resourcegroups/{this.resourceGroupName}/providers/Microsoft.Resources/deployments/arm-estimator/whatIf?api-version=2021-04-01",
            CommandType.Subscription => $"https://management.azure.com/subscriptions/{this.scopeId}/providers/Microsoft.Resources/deployments/arm-estimator/whatIf?api-version=2021-04-01",
            CommandType.ManagementGroup => $"https://management.azure.com/providers/Microsoft.Management/managementGroups/{this.scopeId}/providers/Microsoft.Resources/deployments/arm-estimator/whatIf?api-version=2021-04-01",
            CommandType.Tenant => $"https://management.azure.com/providers/Microsoft.Resources/deployments/arm-estimator/whatIf?api-version=2021-04-01",
            _ => $"https://management.azure.com/subscriptions/{this.scopeId}/resourcegroups/{this.resourceGroupName}/providers/Microsoft.Resources/deployments/arm-estimator/whatIf?api-version=2021-04-01",
        };
    }

    private static async Task<HttpResponseMessage> SendAndWaitForResponse(Uri location)
    {
        var token = GetToken();
        var request = new HttpRequestMessage(HttpMethod.Get, location);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.Value.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return response;
    }

    private static string GetToken()
    {
        var options = new DefaultAzureCredentialOptions()
        {
            ExcludeVisualStudioCodeCredential = true,
            ExcludeVisualStudioCredential = true
        };

        var token = new DefaultAzureCredential(options).GetToken(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None).Token;
        return token;
    }
}
