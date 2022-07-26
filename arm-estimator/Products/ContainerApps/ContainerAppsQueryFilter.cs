﻿using Microsoft.Extensions.Logging;

internal class ContainerAppsQueryFilter : IQueryFilter
{
    private const string ServiceId = "DZH319F70F09";

    private readonly WhatIfAfterBeforeChange afterState;

    public ContainerAppsQueryFilter(WhatIfAfterBeforeChange afterState, ILogger logger)
    {
        this.afterState = afterState;
    }

    public string? GetFiltersBasedOnDesiredState(string location)
    {
        return $"$filter=serviceId eq '{ServiceId}' and armRegionName eq '{location}' and skuId eq 'DZH318Z0B0NC/0001'";
    }
}
