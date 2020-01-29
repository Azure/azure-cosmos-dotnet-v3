// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens
{
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class StringRequestContinuationToken : RequestContinuationToken
    {
        public static readonly StringRequestContinuationToken Null = new StringRequestContinuationToken(continuationToken: null);

        public StringRequestContinuationToken(string continuationToken)
        {
            this.Value = continuationToken;
        }

        public string Value { get; }

        public override bool IsNull => string.IsNullOrWhiteSpace(this.Value);

        public override bool TryConvertToCosmosElement<TCosmosElement>(out TCosmosElement cosmosElement)
        {
            return CosmosElement.TryParse(this.Value, out cosmosElement);
        }
    }
}
