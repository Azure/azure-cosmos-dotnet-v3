//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.SinglePartition
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// A continuation token that has both backend continuation token and partition range information. 
    /// </summary>
    internal sealed class TryExecuteContinuationToken : IPartitionedToken
    {
        public TryExecuteContinuationToken(string token, Range<string> range)
        {
            this.Token = token;
            this.Range = range ?? throw new ArgumentNullException(nameof(range));
        }

        public string Token { get; }

        public Range<string> Range { get; }

        public static CosmosElement ToCosmosElement(TryExecuteContinuationToken continuationToken)
        {
            CosmosElement token = continuationToken.Token == null ? (CosmosElement)CosmosNull.Create() : (CosmosElement)CosmosString.Create(continuationToken.Token);
            return CosmosObject.Create(
                new Dictionary<string, CosmosElement>()
                {
                    { CompositeContinuationToken.PropertyNames.Token, token },
                    {
                        CompositeContinuationToken.PropertyNames.Range,
                        CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { CompositeContinuationToken.PropertyNames.Min, CosmosString.Create(continuationToken.Range.Min) },
                                { CompositeContinuationToken.PropertyNames.Max, CosmosString.Create(continuationToken.Range.Max) }
                            })
                    },
                });
        }

        public static TryCatch<TryExecuteContinuationToken> TryCreateFromCosmosElement(CosmosElement cosmosElement)
        {
            if (!(cosmosElement is CosmosObject cosmosObject))
            {
                return TryCatch<TryExecuteContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(TryExecuteContinuationToken)} is not an object: {cosmosElement}"));
            }

            if (!cosmosObject.TryGetValue(CompositeContinuationToken.PropertyNames.Token, out CosmosElement rawToken))
            {
                return TryCatch<TryExecuteContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(TryExecuteContinuationToken)} is missing field: '{CompositeContinuationToken.PropertyNames.Token}': {cosmosElement}"));
            }

            string token;
            if (rawToken is CosmosString rawTokenString)
            {
                token = rawTokenString.Value;
            }
            else
            {
                token = null;
            }

            if (!cosmosObject.TryGetValue(CompositeContinuationToken.PropertyNames.Range, out CosmosObject rawRange))
            {
                return TryCatch<TryExecuteContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(TryExecuteContinuationToken)} is missing field: '{CompositeContinuationToken.PropertyNames.Range}': {cosmosElement}"));
            }

            if (!rawRange.TryGetValue(CompositeContinuationToken.PropertyNames.Min, out CosmosString rawMin))
            {
                return TryCatch<TryExecuteContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(TryExecuteContinuationToken)} is missing field: '{CompositeContinuationToken.PropertyNames.Min}': {cosmosElement}"));
            }

            string min = rawMin.Value;

            if (!rawRange.TryGetValue(CompositeContinuationToken.PropertyNames.Max, out CosmosString rawMax))
            {
                return TryCatch<TryExecuteContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(TryExecuteContinuationToken)} is missing field: '{CompositeContinuationToken.PropertyNames.Max}': {cosmosElement}"));
            }

            string max = rawMax.Value;

            Range<string> range = new Documents.Routing.Range<string>(min, max, isMinInclusive: true, isMaxInclusive: false);

            TryExecuteContinuationToken continuationToken = new TryExecuteContinuationToken(token, range);

            return TryCatch<TryExecuteContinuationToken>.FromResult(continuationToken);
        }
    }
}
