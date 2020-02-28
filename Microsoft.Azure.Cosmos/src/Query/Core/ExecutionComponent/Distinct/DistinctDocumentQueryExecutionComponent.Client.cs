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
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
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
                    if (!DistinctContinuationToken.TryParse(requestContinuation.ToString(), out distinctContinuationToken))
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
                    if (!CosmosElement.TryParse(distinctContinuationToken.SourceToken, out sourceToken))
                    {
                        return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException(
                                $"Invalid Source Token: {distinctContinuationToken.SourceToken}"));
                    }
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

            public override void SerializeState(IJsonWriter jsonWriter)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Continuation token for distinct queries.
            /// </summary>
            private sealed class DistinctContinuationToken
            {
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
                /// <param name="stringRequestContinuationToken">The value to parse.</param>
                /// <param name="distinctContinuationToken">The output DistinctContinuationToken.</param>
                /// <returns>True if we successfully parsed the DistinctContinuationToken, else false.</returns>
                public static bool TryParse(
                    string stringRequestContinuationToken,
                    out DistinctContinuationToken distinctContinuationToken)
                {
                    try
                    {
                        distinctContinuationToken = JsonConvert.DeserializeObject<DistinctContinuationToken>(stringRequestContinuationToken);
                        return true;
                    }
                    catch (JsonException)
                    {
                        distinctContinuationToken = default;
                        return false;
                    }
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
