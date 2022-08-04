﻿using Azure.Core;
using Microsoft.Extensions.Logging;

internal class AppServicePlanRetailQuery : BaseRetailQuery, IRetailQuery
{
    public AppServicePlanRetailQuery(WhatIfChange change, ResourceIdentifier id, ILogger logger)
        : base(change, id, logger)
    {
    }

    public string? GetQueryUrl(string location)
    {
        if (this.change.after == null && this.change.before == null)
        {
            this.logger.LogError("Can't generate Retail API query if desired state is unavailable.");
            return null;
        }

        var change = this.change.after == null ? this.change.before : this.change.after;
        if (change == null)
        {
            this.logger.LogError("Couldn't determine after / before state.");
            return null;
        }

        var filter = new AppServicePlanQueryFilter(change, this.logger).GetFiltersBasedOnDesiredState(location);
        return $"{BaseQuery}{filter}";
    }
}
