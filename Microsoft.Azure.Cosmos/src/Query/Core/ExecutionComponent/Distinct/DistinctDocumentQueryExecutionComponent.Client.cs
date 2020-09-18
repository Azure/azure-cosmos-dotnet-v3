//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Distinct
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Newtonsoft.Json;

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
                            new MalformedContinuationTokenException(
                                $"Invalid {nameof(DistinctContinuationToken)}: {requestContinuation}"));
                    }
                }
                else
                {
                    distinctContinuationToken = new DistinctContinuationToken(
                        sourceToken: null,
                        distinctMapToken: null);
                }

                CosmosElement distinctMapToken;
                if (distinctContinuationToken.DistinctMapToken != null)
                {
                    distinctMapToken = CosmosString.Create(distinctContinuationToken.DistinctMapToken);
                }
                else
                {
                    distinctMapToken = null;
                }

                TryCatch<DistinctMap> tryCreateDistinctMap = DistinctMap.TryCreate(
                    distinctQueryType,
                    distinctMapToken);
                if (!tryCreateDistinctMap.Succeeded)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(tryCreateDistinctMap.Exception);
                }

                CosmosElement sourceToken;
                if (distinctContinuationToken.SourceToken != null)
                {
                    TryCatch<CosmosElement> tryParse = CosmosElement.Monadic.Parse(distinctContinuationToken.SourceToken);
                    if (tryParse.Failed)
                    {
                        return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException(
                                message: $"Invalid Source Token: {distinctContinuationToken.SourceToken}",
                                innerException: tryParse.Exception));
                    }

                    sourceToken = tryParse.Result;
                }
                else
                {
                    sourceToken = null;
                }

                TryCatch<IDocumentQueryExecutionComponent> tryCreateSource = await tryCreateSourceAsync(sourceToken);
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
                cancellationToken.ThrowIfCancellationRequested();

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
                if (this.distinctQueryType == DistinctQueryType.Ordered)
                {
                    string updatedContinuationToken;
                    if (this.IsDone)
                    {
                        updatedContinuationToken = null;
                    }
                    else
                    {
                        updatedContinuationToken = new DistinctContinuationToken(
                            sourceToken: sourceResponse.ContinuationToken,
                            distinctMapToken: this.distinctMap.GetContinuationToken()).ToString();
                    }

                    queryResponseCore = QueryResponseCore.CreateSuccess(
                        result: distinctResults,
                        continuationToken: updatedContinuationToken,
                        disallowContinuationTokenMessage: null,
                        activityId: sourceResponse.ActivityId,
                        requestCharge: sourceResponse.RequestCharge,
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
                        responseLengthBytes: sourceResponse.ResponseLengthBytes);
                }

                return queryResponseCore;
            }

            public override CosmosElement GetCosmosElementContinuationToken()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Continuation token for distinct queries.
            /// </summary>
            private sealed class DistinctContinuationToken
            {
                private static class PropertyNames
                {
                    public const string SourceToken = "SourceToken";
                    public const string DistinctMapToken = "DistinctMapToken";
                }

                public DistinctContinuationToken(string sourceToken, string distinctMapToken)
                {
                    this.SourceToken = sourceToken;
                    this.DistinctMapToken = distinctMapToken;
                }

                public string SourceToken { get; }

                public string DistinctMapToken { get; }

                /// <summary>
                /// Tries to parse a DistinctContinuationToken from a string.
                /// </summary>
                /// <param name="cosmosElement">The value to parse.</param>
                /// <param name="distinctContinuationToken">The output DistinctContinuationToken.</param>
                /// <returns>True if we successfully parsed the DistinctContinuationToken, else false.</returns>
                public static bool TryParse(
                    CosmosElement cosmosElement,
                    out DistinctContinuationToken distinctContinuationToken)
                {
                    if (!(cosmosElement is CosmosObject cosmosObject))
                    {
                        distinctContinuationToken = default;
                        return false;
                    }

                    if (!cosmosObject.TryGetValue(
                        DistinctContinuationToken.PropertyNames.SourceToken,
                        out CosmosString sourceToken))
                    {
                        distinctContinuationToken = default;
                        return false;
                    }

                    if (!cosmosObject.TryGetValue(
                        DistinctContinuationToken.PropertyNames.DistinctMapToken,
                        out CosmosString distinctMapToken))
                    {
                        distinctContinuationToken = default;
                        return false;
                    }

                    distinctContinuationToken = new DistinctContinuationToken(sourceToken.Value, distinctMapToken.Value);
                    return true;
                }

                /// <summary>
                /// Gets the serialized form of DistinctContinuationToken
                /// </summary>
                /// <returns>The serialized form of DistinctContinuationToken</returns>
                public override string ToString()
                {
                    return JsonConvert.SerializeObject(this);
                }
            }
        }
    }
}
