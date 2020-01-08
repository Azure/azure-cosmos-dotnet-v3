//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Routing;
    using Newtonsoft.Json;

    /// <summary>
    /// A composite continuation token that has both backend continuation token and partition range information. 
    /// </summary>
    internal sealed class CompositeContinuationToken
    {
        private const string TokenName = "token";
        private const string RangeName = "range";

        private const string MinName = "min";
        private const string MaxName = "max";

        [JsonProperty(TokenName)]
        public string Token
        {
            get;
            set;
        }

        [JsonProperty(RangeName)]
        [JsonConverter(typeof(RangeJsonConverter))]
        public Documents.Routing.Range<string> Range
        {
            get;
            set;
        }

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
                    { CompositeContinuationToken.TokenName, token },
                    {
                        CompositeContinuationToken.RangeName,
                        CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { MinName, CosmosString.Create(compositeContinuationToken.Range.Min) },
                                { MaxName, CosmosString.Create(compositeContinuationToken.Range.Max) }
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

            if (!cosmosObject.TryGetValue(TokenName, out CosmosElement rawToken))
            {
                return TryCatch<CompositeContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(CompositeContinuationToken)} is missing field: '{TokenName}': {cosmosElement}"));
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

            if (!cosmosObject.TryGetValue(RangeName, out CosmosObject rawRange))
            {
                return TryCatch<CompositeContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(CompositeContinuationToken)} is missing field: '{RangeName}': {cosmosElement}"));
            }

            if (!rawRange.TryGetValue(MinName, out CosmosString rawMin))
            {
                return TryCatch<CompositeContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(CompositeContinuationToken)} is missing field: '{MinName}': {cosmosElement}"));
            }

            string min = rawMin.Value;

            if (!rawRange.TryGetValue(MaxName, out CosmosString rawMax))
            {
                return TryCatch<CompositeContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(CompositeContinuationToken)} is missing field: '{MaxName}': {cosmosElement}"));
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
