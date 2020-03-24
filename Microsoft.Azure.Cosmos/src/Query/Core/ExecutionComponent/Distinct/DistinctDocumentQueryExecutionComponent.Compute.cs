//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Distinct
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal abstract partial class DistinctDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        /// <summary>
        /// Compute implementation of DISTINCT.
        /// Here we never serialize the continuation token, but you can always retrieve it on demand with TryGetContinuationToken.
        /// </summary>
        private sealed class ComputeDistinctDocumentQueryExecutionComponent : DistinctDocumentQueryExecutionComponent
        {
            private static readonly string UseTryGetContinuationTokenMessage = $"Use TryGetContinuationToken";

            private ComputeDistinctDocumentQueryExecutionComponent(
                DistinctQueryType distinctQueryType,
                DistinctMap distinctMap,
                IDocumentQueryExecutionComponent source)
                : base(distinctMap, source)
            {
            }

            public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateAsync(
                CosmosElement requestContinuation,
                Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync,
                DistinctQueryType distinctQueryType)
            {
                if (tryCreateSourceAsync == null)
                {
                    throw new ArgumentNullException(nameof(tryCreateSourceAsync));
                }

                DistinctContinuationToken distinctContinuationToken;
                if (requestContinuation != null)
                {
                    if (!DistinctContinuationToken.TryParse(requestContinuation, out distinctContinuationToken))
                    {
                        return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException($"Invalid {nameof(DistinctContinuationToken)}: {requestContinuation}"));
                    }
                }
                else
                {
                    distinctContinuationToken = new DistinctContinuationToken(sourceToken: null, distinctMapToken: null);
                }

                TryCatch<DistinctMap> tryCreateDistinctMap = DistinctMap.TryCreate(
                    distinctQueryType,
                    distinctContinuationToken.DistinctMapToken);
                if (!tryCreateDistinctMap.Succeeded)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(tryCreateDistinctMap.Exception);
                }

                TryCatch<IDocumentQueryExecutionComponent> tryCreateSource = await tryCreateSourceAsync(
                    distinctContinuationToken.SourceToken);
                if (!tryCreateSource.Succeeded)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(tryCreateSource.Exception);
                }

                return TryCatch<IDocumentQueryExecutionComponent>.FromResult(
                    new ComputeDistinctDocumentQueryExecutionComponent(
                        distinctQueryType,
                        tryCreateDistinctMap.Result,
                        tryCreateSource.Result));
            }

            /// <summary>
            /// Drains a page of results returning only distinct elements.
            /// </summary>
            /// <param name="maxElements">The maximum number of items to drain.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A page of distinct results.</returns>
            public override async Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken cancellationToken)
            {
                List<CosmosElement> distinctResults = new List<CosmosElement>();
                QueryResponseCore sourceResponse = await base.DrainAsync(maxElements, cancellationToken);

                if (!sourceResponse.IsSuccess)
                {
                    return sourceResponse;
                }

                foreach (CosmosElement document in sourceResponse.CosmosElements)
                {
                    if (this.distinctMap.Add(document, out UInt128 hash))
                    {
                        distinctResults.Add(document);
                    }
                }

                return QueryResponseCore.CreateSuccess(
                        result: distinctResults,
                        continuationToken: null,
                        disallowContinuationTokenMessage: ComputeDistinctDocumentQueryExecutionComponent.UseTryGetContinuationTokenMessage,
                        activityId: sourceResponse.ActivityId,
                        requestCharge: sourceResponse.RequestCharge,
                        responseLengthBytes: sourceResponse.ResponseLengthBytes);
            }

            public override CosmosElement GetCosmosElementContinuationToken()
            {
                if (this.IsDone)
                {
                    return default;
                }

                DistinctContinuationToken distinctContinuationToken = new DistinctContinuationToken(
                    sourceToken: this.Source.GetCosmosElementContinuationToken(),
                    distinctMapToken: this.distinctMap.GetCosmosElementContinuationToken());
                return DistinctContinuationToken.ToCosmosElement(distinctContinuationToken);
            }

            private readonly struct DistinctContinuationToken
            {
                private const string SourceTokenName = "SourceToken";
                private const string DistinctMapTokenName = "DistinctMapToken";

                public DistinctContinuationToken(CosmosElement sourceToken, CosmosElement distinctMapToken)
                {
                    this.SourceToken = sourceToken;
                    this.DistinctMapToken = distinctMapToken;
                }

                public CosmosElement SourceToken { get; }

                public CosmosElement DistinctMapToken { get; }

                public static CosmosElement ToCosmosElement(DistinctContinuationToken distinctContinuationToken)
                {
                    Dictionary<string, CosmosElement> dictionary = new Dictionary<string, CosmosElement>()
                    {
                        {
                            DistinctContinuationToken.SourceTokenName,
                            distinctContinuationToken.SourceToken
                        },
                        {
                            DistinctContinuationToken.DistinctMapTokenName,
                            distinctContinuationToken.DistinctMapToken
                        }
                    };

                    return CosmosObject.Create(dictionary);
                }

                public static bool TryParse(
                    CosmosElement requestContinuationToken,
                    out DistinctContinuationToken distinctContinuationToken)
                {
                    if (requestContinuationToken == null)
                    {
                        distinctContinuationToken = default;
                        return false;
                    }

                    if (!(requestContinuationToken is CosmosObject rawObject))
                    {
                        distinctContinuationToken = default;
                        return false;
                    }

                    if (!rawObject.TryGetValue(SourceTokenName, out CosmosElement sourceToken))
                    {
                        distinctContinuationToken = default;
                        return false;
                    }

                    if (!rawObject.TryGetValue(DistinctMapTokenName, out CosmosElement distinctMapToken))
                    {
                        distinctContinuationToken = default;
                        return false;
                    }

                    distinctContinuationToken = new DistinctContinuationToken(sourceToken: sourceToken, distinctMapToken: distinctMapToken);
                    return true;
                }
            }

            public override bool TryGetFeedToken(
                string containerResourceId,
                SqlQuerySpec sqlQuerySpec,
                out QueryFeedTokenInternal feedToken)
            {
                if (this.IsDone)
                {
                    feedToken = null;
                    return true;
                }

                if (!this.Source.TryGetFeedToken(containerResourceId, sqlQuerySpec, out feedToken))
                {
                    feedToken = null;
                    return false;
                }

                if (feedToken?.QueryFeedToken is FeedTokenEPKRange tokenEPKRange)
                {
                    DistinctContinuationToken distinctContinuationToken = new DistinctContinuationToken(
                    sourceToken: this.Source.GetCosmosElementContinuationToken(),
                    distinctMapToken: this.distinctMap.GetCosmosElementContinuationToken());
                    feedToken = new QueryFeedTokenInternal(FeedTokenEPKRange.Copy(
                        tokenEPKRange,
                        DistinctContinuationToken.ToCosmosElement(distinctContinuationToken).ToString()),
                        feedToken.QueryDefinition);
                }

                return true;
            }
        }
    }
}
