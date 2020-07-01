// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers
{
    using System;
    using Microsoft.Azure.Cosmos.Pagination;

    internal sealed class QueryState : State
    {
        public QueryState(string continuationToken)
        {
            this.ContinuationToken = continuationToken ?? throw new ArgumentNullException(nameof(continuationToken));
        }

        public string ContinuationToken { get; }
    }
}
