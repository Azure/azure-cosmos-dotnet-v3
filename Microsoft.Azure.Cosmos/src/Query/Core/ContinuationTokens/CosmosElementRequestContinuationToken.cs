// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens
{
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class CosmosElementRequestContinuationToken : RequestContinuationToken
    {
        public CosmosElementRequestContinuationToken(CosmosElement continuationToken)
        {
            this.Value = continuationToken;
        }

        public CosmosElement Value { get; }

        public override bool IsNull => this.Value == null;

        public override bool TryConvertToCosmosElement<TCosmosElement>(out TCosmosElement cosmosElement)
        {
            cosmosElement = (TCosmosElement)this.Value;
            return true;
        }
    }
}
