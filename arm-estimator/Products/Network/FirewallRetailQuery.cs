﻿using ACE.WhatIf;
using Azure.Core;
using Microsoft.Extensions.Logging;

internal class FirewallRetailQuery : BaseRetailQuery, IRetailQuery
{
    public FirewallRetailQuery(WhatIfChange change, CommonResourceIdentifier id, ILogger logger, CurrencyCode currency, WhatIfChange[] changes) : base(change, id, logger, currency, changes)
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

        var filter = new FirewallQueryFilter(change, this.logger).GetFiltersBasedOnDesiredState(location);
        return $"{baseQuery}{filter}";
    }
}
