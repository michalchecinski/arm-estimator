﻿using Microsoft.Extensions.Logging;

internal class StorageAccountQueryFilter : IQueryFilter
{
    private const string ServiceId = "DZH317F1HKN0";

    private readonly WhatIfAfterChange afterState;
    private readonly ILogger logger;

    public StorageAccountQueryFilter(WhatIfAfterChange afterState, ILogger logger)
    {
        this.afterState = afterState;
        this.logger = logger;
    }

    public string? GetFiltersBasedOnDesiredState()
    {
        var location = this.afterState.location;
        var sku = this.afterState.sku?.name;
        if(sku == null)
        {
            this.logger.LogError("Can't create a filter for Storage Account when SKU is unavailable.");
            return null;
        }

        var skuIds = StorageAccountSupportedData.SkuToSkuIdMap[sku];
        var skuIdsFilter = string.Join(" or ", skuIds.Select(_ => $"skuId eq '{_}'"));

        return $"$filter=serviceId eq '{ServiceId}' and armRegionName eq '{location}' and ({skuIdsFilter})";
    }
}
