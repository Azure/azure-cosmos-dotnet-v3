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
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal abstract partial class DistinctDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        /// <summary>
        /// Client implementaiton of Distinct. Here we only serialize the continuation token if there is a matching DISTINCT.
        /// </summary>
        private sealed class ClientDistinctDocumentQueryExecutionComponent : DistinctDocumentQueryExecutionComponent
        {
            private static readonly string DisallowContinuationTokenMessage = "DISTINCT queries only return continuation tokens when there is a matching ORDER BY clause." +
            "For example if your query is 'SELECT DISTINCT VALUE c.name FROM c', then rewrite it as 'SELECT DISTINCT VALUE c.name FROM c ORDER BY c.name'.";

            private readonly DistinctQueryType distinctQueryType;

            private ClientDistinctDocumentQueryExecutionComponent(
                DistinctQueryType distinctQueryType,
                DistinctMap distinctMap,
                IDocumentQueryExecutionComponent source)
                : base(distinctMap, source)
            {
                if ((distinctQueryType != DistinctQueryType.Unordered) && (distinctQueryType != DistinctQueryType.Ordered))
                {
                    throw new ArgumentException($"Unknown {nameof(DistinctQueryType)}: {distinctQueryType}.");
                }

                this.distinctQueryType = distinctQueryType;
            }

            public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateAsync(
                string requestContinuation,
                Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync,
                DistinctQueryType distinctQueryType)
            {
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

                TryCatch<IDocumentQueryExecutionComponent> tryCreateSource = await tryCreateSourceAsync(distinctContinuationToken.SourceToken);
                if (!tryCreateSource.Succeeded)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(tryCreateSource.Exception);
                }

                return TryCatch<IDocumentQueryExecutionComponent>.FromResult(
                    new ClientDistinctDocumentQueryExecutionComponent(
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

                // For clients we write out the continuation token if it's a streaming query.
                QueryResponseCore queryResponseCore;
                if (this.TryGetContinuationToken(out string continuationToken))
                {
                    queryResponseCore = QueryResponseCore.CreateSuccess(
                        result: distinctResults,
                        continuationToken: continuationToken,
                        disallowContinuationTokenMessage: null,
                        activityId: sourceResponse.ActivityId,
                        requestCharge: sourceResponse.RequestCharge,
                        diagnostics: sourceResponse.Diagnostics,
                        responseLengthBytes: sourceResponse.ResponseLengthBytes);
                }
                else
                {
                    queryResponseCore = QueryResponseCore.CreateSuccess(
                        result: distinctResults,
                        continuationToken: null,
                        disallowContinuationTokenMessage: ClientDistinctDocumentQueryExecutionComponent.DisallowContinuationTokenMessage,
                        activityId: sourceResponse.ActivityId,
                        requestCharge: sourceResponse.RequestCharge,
                        diagnostics: sourceResponse.Diagnostics,
                        responseLengthBytes: sourceResponse.ResponseLengthBytes);
                }

                return queryResponseCore;
            }

            public override bool TryGetContinuationToken(out string continuationToken)
            {
                if (this.distinctQueryType != DistinctQueryType.Ordered)
                {
                    continuationToken = null;
                    return false;
                }

                if (this.IsDone)
                {
                    continuationToken = null;
                    return true;
                }

                if (!this.Source.TryGetContinuationToken(out string sourceContinuationToken))
                {
                    continuationToken = default;
                    return false;
                }

                continuationToken = new DistinctContinuationToken(
                    sourceContinuationToken,
                    this.distinctMap.GetContinuationToken()).ToString();
                return true;
            }

            public override bool TryGetFeedToken(out FeedToken feedToken)
            {
                if (this.distinctQueryType != DistinctQueryType.Ordered)
                {
                    feedToken = null;
                    return false;
                }

                if (this.IsDone)
                {
                    feedToken = null;
                    return true;
                }

                if (!this.Source.TryGetFeedToken(out feedToken))
                {
                    feedToken = null;
                    return false;
                }

                FeedTokenEPKRange feedTokenInternal = feedToken as FeedTokenEPKRange;
                feedToken = new FeedTokenEPKRange(
                    feedTokenInternal.ContainerRid,
                    feedTokenInternal.CompositeContinuationTokens.Select(token => token.Range).ToList(),
                    new DistinctContinuationToken(
                        feedTokenInternal.GetContinuation(),
                        this.distinctMap.GetContinuationToken()).ToString());
                return true;
            }
        }
    }
}
