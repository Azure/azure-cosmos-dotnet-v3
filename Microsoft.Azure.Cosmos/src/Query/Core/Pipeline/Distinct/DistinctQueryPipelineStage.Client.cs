//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Newtonsoft.Json;

    internal abstract partial class DistinctQueryPipelineStage : QueryPipelineStageBase
    {
        /// <summary>
        /// Client implementaiton of Distinct. Here we only serialize the continuation token if there is a matching DISTINCT.
        /// </summary>
        private sealed class ClientDistinctQueryPipelineStage : DistinctQueryPipelineStage
        {
            private static readonly string DisallowContinuationTokenMessage = "DISTINCT queries only return continuation tokens when there is a matching ORDER BY clause." +
            "For example if your query is 'SELECT DISTINCT VALUE c.name FROM c', then rewrite it as 'SELECT DISTINCT VALUE c.name FROM c ORDER BY c.name'.";

            private readonly DistinctQueryType distinctQueryType;

            private ClientDistinctQueryPipelineStage(
                DistinctQueryType distinctQueryType,
                DistinctMap distinctMap,
                IQueryPipelineStage source)
                : base(distinctMap, source)
            {
                if ((distinctQueryType != DistinctQueryType.Unordered) && (distinctQueryType != DistinctQueryType.Ordered))
                {
                    throw new ArgumentException($"Unknown {nameof(DistinctQueryType)}: {distinctQueryType}.");
                }

                this.distinctQueryType = distinctQueryType;
            }

            public static TryCatch<IQueryPipelineStage> MonadicCreate(
                CosmosElement requestContinuation,
                MonadicCreatePipelineStage monadicCreatePipelineStage,
                DistinctQueryType distinctQueryType)
            {
                if (monadicCreatePipelineStage == null)
                {
                    throw new ArgumentNullException(nameof(monadicCreatePipelineStage));
                }

                DistinctContinuationToken distinctContinuationToken;
                if (requestContinuation != null)
                {
                    if (!DistinctContinuationToken.TryParse(requestContinuation, out distinctContinuationToken))
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
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
                    return TryCatch<IQueryPipelineStage>.FromException(tryCreateDistinctMap.Exception);
                }

                CosmosElement sourceToken;
                if (distinctContinuationToken.SourceToken != null)
                {
                    TryCatch<CosmosElement> tryParse = CosmosElement.Monadic.Parse(distinctContinuationToken.SourceToken);
                    if (tryParse.Failed)
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
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

                TryCatch<IQueryPipelineStage> tryCreateSource = monadicCreatePipelineStage(sourceToken);
                if (!tryCreateSource.Succeeded)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(tryCreateSource.Exception);
                }

                return TryCatch<IQueryPipelineStage>.FromResult(
                    new ClientDistinctQueryPipelineStage(
                        distinctQueryType,
                        tryCreateDistinctMap.Result,
                        tryCreateSource.Result));
            }

            protected override async Task<TryCatch<QueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await this.inputStage.MoveNextAsync();
                TryCatch<QueryPage> tryGetSourcePage = this.inputStage.Current;
                if (tryGetSourcePage.Failed)
                {
                    return tryGetSourcePage;
                }

                QueryPage sourcePage = tryGetSourcePage.Result;

                List<CosmosElement> distinctResults = new List<CosmosElement>();
                foreach (CosmosElement document in sourcePage.Documents)
                {
                    if (this.distinctMap.Add(document, out UInt128 _))
                    {
                        distinctResults.Add(document);
                    }
                }

                // For clients we write out the continuation token if it's a streaming query.
                QueryPage queryPage;
                if (this.distinctQueryType == DistinctQueryType.Ordered)
                {
                    QueryState state;
                    if (sourcePage.State != null)
                    {
                        string updatedContinuationToken = new DistinctContinuationToken(
                            sourceToken: ((CosmosString)sourcePage.State.Value).Value,
                            distinctMapToken: this.distinctMap.GetContinuationToken()).ToString();
                        state = new QueryState(CosmosString.Create(updatedContinuationToken));
                    }
                    else
                    {
                        state = null;
                    }

                    queryPage = new QueryPage(
                        documents: distinctResults,
                        requestCharge: sourcePage.RequestCharge,
                        activityId: sourcePage.ActivityId,
                        responseLengthInBytes: sourcePage.ResponseLengthInBytes,
                        cosmosQueryExecutionInfo: sourcePage.CosmosQueryExecutionInfo,
                        disallowContinuationTokenMessage: sourcePage.DisallowContinuationTokenMessage,
                        state: state);
                }
                else
                {
                    queryPage = new QueryPage(
                        documents: distinctResults,
                        requestCharge: sourcePage.RequestCharge,
                        activityId: sourcePage.ActivityId,
                        responseLengthInBytes: sourcePage.ResponseLengthInBytes,
                        cosmosQueryExecutionInfo: sourcePage.CosmosQueryExecutionInfo,
                        disallowContinuationTokenMessage: ClientDistinctQueryPipelineStage.DisallowContinuationTokenMessage,
                        state: null);
                }

                return TryCatch<QueryPage>.FromResult(queryPage);
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