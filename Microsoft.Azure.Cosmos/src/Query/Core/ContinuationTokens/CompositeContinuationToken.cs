//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;

    /// <summary>
    /// A composite continuation token that has both backend continuation token and partition range information. 
    /// </summary>
    internal sealed class CompositeContinuationToken : IPartitionedToken
    {
        private static class PropertyNames
        {
            public const string Token = "token";
            public const string Range = "range";

            public const string Min = "min";
            public const string Max = "max";
        }

        [JsonProperty(PropertyNames.Token)]
        public string Token
        {
            get;
            set;
        }

        [JsonProperty(PropertyNames.Range)]
        [JsonConverter(typeof(RangeJsonConverter))]
        public Documents.Routing.Range<string> Range
        {
            get;
            set;
        }

        [JsonIgnore]
        public Range<string> PartitionRange => this.Range;

        public object ShallowCopy()
        {
            return this.MemberwiseClone();
        }

        public static CosmosElement ToCosmosElement(CompositeContinuationToken compositeContinuationToken)
        {
            CosmosElement token = compositeContinuationToken.Token == null ? (CosmosElement)CosmosNull.Create() : (CosmosElement)CosmosString.Create(compositeContinuationToken.Token);
            return CosmosObject.Create(
                new Dictionary<string, CosmosElement>()
                {
                    { CompositeContinuationToken.PropertyNames.Token, token },
                    {
                        CompositeContinuationToken.PropertyNames.Range,
                        CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { PropertyNames.Min, CosmosString.Create(compositeContinuationToken.Range.Min) },
                                { PropertyNames.Max, CosmosString.Create(compositeContinuationToken.Range.Max) }
                            })
                    },
                });
        }

        public static TryCatch<CompositeContinuationToken> TryCreateFromCosmosElement(CosmosElement cosmosElement)
        {
            if (!(cosmosElement is CosmosObject cosmosObject))
            {
                return TryCatch<CompositeContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(CompositeContinuationToken)} is not an object: {cosmosElement}"));
            }

            if (!cosmosObject.TryGetValue(PropertyNames.Token, out CosmosElement rawToken))
            {
                return TryCatch<CompositeContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(CompositeContinuationToken)} is missing field: '{PropertyNames.Token}': {cosmosElement}"));
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
                return TryCatch<CompositeContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(CompositeContinuationToken)} is missing field: '{PropertyNames.Range}': {cosmosElement}"));
            }

            if (!rawRange.TryGetValue(PropertyNames.Min, out CosmosString rawMin))
            {
                return TryCatch<CompositeContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(CompositeContinuationToken)} is missing field: '{PropertyNames.Min}': {cosmosElement}"));
            }

            string min = rawMin.Value;

            if (!rawRange.TryGetValue(PropertyNames.Max, out CosmosString rawMax))
            {
                return TryCatch<CompositeContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(CompositeContinuationToken)} is missing field: '{PropertyNames.Max}': {cosmosElement}"));
            }

            string max = rawMax.Value;

            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>(min, max, true, false);

            CompositeContinuationToken compositeContinuationToken = new CompositeContinuationToken()
            {
                Token = token,
                Range = range,
            };

            return TryCatch<CompositeContinuationToken>.FromResult(compositeContinuationToken);
        }
    }
}
