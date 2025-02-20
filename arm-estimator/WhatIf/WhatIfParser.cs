﻿using ACE.Compilation;
using Microsoft.Extensions.Logging;

namespace ACE.WhatIf;

internal class WhatIfParser
{
    private readonly TemplateType templateType;
    private readonly string scopeId;
    private readonly string? resourceGroupName;
    private readonly string template;
    private readonly DeploymentMode mode;
    private readonly string parameters;
    private readonly ILogger<Program> logger;
    private readonly CommandType commandType;
    private readonly string? location;
    private readonly bool disableCache;

    public WhatIfParser(
        TemplateType templateType,
        string scopeId,
        string? resourceGroupName,
        string template,
        DeploymentMode mode,
        string parameters,
        ILogger<Program> logger,
        CommandType commandType,
        string? location,
        bool disableCache)
    {
        this.templateType = templateType;
        this.scopeId = scopeId;
        this.resourceGroupName = resourceGroupName;
        this.template = template;
        this.mode = mode;
        this.parameters = parameters;
        this.logger = logger;
        this.commandType = commandType;
        this.location = location;
        this.disableCache = disableCache;
    }

    public async Task<WhatIfResponse?> GetWhatIfData()
    {
        if(this.templateType == TemplateType.ArmTemplateOrBicep)
        {
            var handler = new AzureWhatIfHandler(
                this.scopeId, 
                this.resourceGroupName, 
                this.template, 
                this.mode, 
                this.parameters, 
                this.logger,
                this.commandType,
                this.location,
                this.disableCache);

            return await handler.GetResponseWithRetries();
        }

        if(this.templateType == TemplateType.Terraform)
        {
            var parser = new TerraformTemplateParser(this.template, this.logger);
            return parser.GetConfigurationAsWhatIfData();
        }

        return null;
    }
}
