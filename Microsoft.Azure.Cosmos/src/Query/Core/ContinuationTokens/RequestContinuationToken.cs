// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;

    /// <summary>
    /// Base class that all continuation tokens will follow.
    /// This serves as an adapter pattern, so that all different types of continuation tokens can have a common interface.
    /// </summary>
    internal abstract class RequestContinuationToken
    {
        public static RequestContinuationToken Create(string continuationToken)
        {
            return new StringRequestContinuationToken(continuationToken);
        }

        public static RequestContinuationToken Create(CosmosElement continuationToken)
        {
            return new CosmosElementRequestContinuationToken(continuationToken);
        }

        public abstract bool IsNull { get; }

        public abstract bool TryConvertToCosmosElement<TCosmosElement>(out TCosmosElement cosmosElement)
            where TCosmosElement : CosmosElement;
    }
}
