﻿using Azure.Core;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal class Program
{
    public static string? GetInformationalVersion() => Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    private static async Task<int> Main(string[] args)
    {
        var templateFileArg = new Argument<FileInfo>("template-file", "Template file to analyze");
        var susbcriptionIdArg = new Argument<string>("subscription-id", "Subscription ID");
        var resourceGroupArg = new Argument<string>("resource-group", "Resource group name");
        var deploymentModeOption = new Option<DeploymentMode>("--mode", () => { return DeploymentMode.Incremental; }, "Deployment mode");
        var thresholdOption = new Option<int>("--threshold", () => { return -1; }, "Estimation threshold");
        var parametersOption = new Option<FileInfo?>("--parameters", () => { return null; }, "Path to a file containing values of template parameters");

        var command = new RootCommand("Azure Resource Manager cost estimator.");
        command.AddOption(deploymentModeOption);
        command.AddOption(thresholdOption);
        command.AddOption(parametersOption);
        command.AddArgument(templateFileArg);
        command.AddArgument(susbcriptionIdArg);
        command.AddArgument(resourceGroupArg);
        command.SetHandler(async (file, subscription, resourceGroup, deploymentMode, threshold, parametersFilePath) =>
            await Estimate(file, subscription, resourceGroup, deploymentMode, threshold, parametersFilePath), 
            templateFileArg, 
            susbcriptionIdArg, 
            resourceGroupArg, 
            deploymentModeOption,
            thresholdOption,
            parametersOption);

        return await command.InvokeAsync(args);
    }

    private static async Task Estimate(
        FileInfo file, 
        string subscriptionId, 
        string resourceGroupName, 
        DeploymentMode deploymentMode,
        int threshold,
        FileInfo? parametersFile)
    {
        using (var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.AddEstimatorLogger();
        }))
        {
            var logger = loggerFactory.CreateLogger<Program>();
            DisplayWelcomeScreen(logger);

            var template = Regex.Replace(File.ReadAllText(file.FullName), @"\s+", string.Empty);  // Make JSON a single-line value
            var parameters = "{}";

            if (parametersFile != null)
            {
                parameters = Regex.Replace(File.ReadAllText(parametersFile.FullName), @"\s+", string.Empty);
            }

            var handler = new AzureWhatIfHandler(subscriptionId, resourceGroupName, template, deploymentMode, parameters, logger);
            var whatIfData = await handler.GetResponseWithRetries();

            if (whatIfData != null && whatIfData.status == "Failed")
            {
                logger.LogError("An error happened when performing WhatIf operation.");

                if (whatIfData.error != null)
                {
                    var errorDetails = JsonSerializer.Serialize(whatIfData.error, typeof(WhatIfError), new JsonSerializerOptions()
                    {
                        WriteIndented = true
                    });

                    logger.LogError(errorDetails);
                }

                return;
            }

            if (whatIfData == null || whatIfData.properties == null || whatIfData.properties.changes == null || whatIfData.properties.changes.Length == 0)
            {
                logger.LogInformation("No changes detected.");
                return;
            }

            logger.LogInformation("Detected {noOfChanges} changes.", whatIfData.properties.changes.Length);
            logger.LogInformation("-------------------------------");

            ReportChangesToConsole(whatIfData.properties.changes, logger);

            logger.LogInformation("");
            logger.LogInformation("-------------------------------");
            logger.LogInformation("");

            var totalCost = await new WhatIfProcessor(logger, whatIfData.properties.changes).Process();
            if (threshold != -1 && totalCost > threshold)
            {
                logger.LogError("Estimated cost [{totalCost} USD] exceeds configured threshold [{threshold} USD].", totalCost, threshold);
                Environment.Exit(1);
            }
        }
    }

    private static void DisplayWelcomeScreen(ILogger<Program> logger)
    {
        logger.LogInformation("ARM Cost Estimator [{version}]", GetInformationalVersion());
        logger.LogInformation("------------------------------");
        logger.LogInformation("");
        logger.LogInformation("General help: https://github.com/TheCloudTheory/arm-estimator/discussions");
        logger.LogInformation("Bugs & issues: https://github.com/TheCloudTheory/arm-estimator/issues");
        logger.LogInformation("");
        logger.LogInformation("------------------------------");
        logger.LogInformation("");
    }

    private static void ReportChangesToConsole(WhatIfChange[] changes, ILogger logger)
    {
        foreach (var change in changes)
        {
            if (change == null) continue;

            if(change.resourceId == null)
            {
                logger.LogWarning("Couldn't find resource ID.");
                continue;
            }

            if (change.changeType == null)
            {
                logger.LogWarning("Couldn't find change type.");
                continue;
            }

            var id = new ResourceIdentifier(change.resourceId);
            var formattedChangeType = change.changeType?.ToString().ToUpperInvariant();
            logger.LogInformation("{change} - {name} [{type}]", formattedChangeType, id.Name, id.ResourceType);
        }
    }
}