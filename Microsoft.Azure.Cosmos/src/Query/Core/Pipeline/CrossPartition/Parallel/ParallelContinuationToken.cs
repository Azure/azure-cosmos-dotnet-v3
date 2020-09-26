//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// A composite continuation token that has both backend continuation token and partition range information. 
    /// </summary>
    internal sealed class ParallelContinuationToken : IPartitionedToken
    {
        private static class PropertyNames
        {
            public const string Token = "token";
            public const string Range = "range";

            public const string Min = "min";
            public const string Max = "max";
        }

        public ParallelContinuationToken(string token, Range<string> range)
        {
            this.Token = token;
            this.Range = range ?? throw new ArgumentNullException(nameof(range));
        }

        public string Token { get; }

        public Range<string> Range { get; }

        public static CosmosElement ToCosmosElement(ParallelContinuationToken parallelContinuationToken)
        {
            CosmosElement token = parallelContinuationToken.Token == null ? (CosmosElement)CosmosNull.Create() : (CosmosElement)CosmosString.Create(parallelContinuationToken.Token);
            return CosmosObject.Create(
                new Dictionary<string, CosmosElement>()
                {
                    { ParallelContinuationToken.PropertyNames.Token, token },
                    {
                        ParallelContinuationToken.PropertyNames.Range,
                        CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { PropertyNames.Min, CosmosString.Create(parallelContinuationToken.Range.Min) },
                                { PropertyNames.Max, CosmosString.Create(parallelContinuationToken.Range.Max) }
                            })
                    },
                });
        }

        public static TryCatch<ParallelContinuationToken> TryCreateFromCosmosElement(CosmosElement cosmosElement)
        {
            if (!(cosmosElement is CosmosObject cosmosObject))
            {
                return TryCatch<ParallelContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(ParallelContinuationToken)} is not an object: {cosmosElement}"));
            }

            if (!cosmosObject.TryGetValue(PropertyNames.Token, out CosmosElement rawToken))
            {
                return TryCatch<ParallelContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(ParallelContinuationToken)} is missing field: '{PropertyNames.Token}': {cosmosElement}"));
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

            if (!cosmosObject.TryGetValue(PropertyNames.Range, out CosmosObject rawRange))
            {
                return TryCatch<ParallelContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(ParallelContinuationToken)} is missing field: '{PropertyNames.Range}': {cosmosElement}"));
            }

            if (!rawRange.TryGetValue(PropertyNames.Min, out CosmosString rawMin))
            {
                return TryCatch<ParallelContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(ParallelContinuationToken)} is missing field: '{PropertyNames.Min}': {cosmosElement}"));
            }

            string min = rawMin.Value;

            if (!rawRange.TryGetValue(PropertyNames.Max, out CosmosString rawMax))
            {
                return TryCatch<ParallelContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(ParallelContinuationToken)} is missing field: '{PropertyNames.Max}': {cosmosElement}"));
            }

            string max = rawMax.Value;

            Range<string> range = new Documents.Routing.Range<string>(min, max, true, false);

            ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(token, range);

            return TryCatch<ParallelContinuationToken>.FromResult(parallelContinuationToken);
        }
    }
}
