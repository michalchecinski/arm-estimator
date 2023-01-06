﻿using Azure.Core;
using Microsoft.Extensions.Logging;

internal class BotServiceRetailQuery : BaseRetailQuery, IRetailQuery
{
    public BotServiceRetailQuery(WhatIfChange change, ResourceIdentifier id, ILogger logger, CurrencyCode currency)
        : base(change, id, logger, currency)
    {
    }

    public RetailAPIResponse? GetFakeResponse()
    {
        throw new NotImplementedException();
    }

    public string? GetQueryUrl(string location)
    {
        if (this.change.after == null && this.change.before == null)
        {
            this.logger.LogError("Can't generate Retail API query if desired state is unavailable.");
            return null;
        }

        var change = this.change.after ?? this.change.before;
        if(change == null)
        {
            this.logger.LogError("Couldn't determine after / before state.");
            return null;
        }

        var filter = new BotServiceQueryFilter(change, this.logger).GetFiltersBasedOnDesiredState(location);
        return $"{baseQuery}{filter}";
    }
}
