// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;

    internal sealed class QueryState : State
    {
        public QueryState(CosmosElement value)
        {
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public CosmosElement Value { get; }
    }
}