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
        /// Compute implementation of DISTINCT.
        /// Here we never serialize the continuation token, but you can always retrieve it on demand with TryGetContinuationToken.
        /// </summary>
        private sealed class ComputeDistinctQueryPipelineStage : DistinctQueryPipelineStage
        {
            private static readonly string UseTryGetContinuationTokenMessage = $"Use TryGetContinuationToken";

            private ComputeDistinctQueryPipelineStage(
                DistinctMap distinctMap,
                IQueryPipelineStage source,
                CancellationToken cancellationToken)
                : base(distinctMap, source, cancellationToken)
            {
            }

            public static TryCatch<IQueryPipelineStage> MonadicCreate(
                CosmosElement requestContinuation,
                CancellationToken cancellationToken,
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
                    distinctContinuationToken = new DistinctContinuationToken(sourceToken: null, distinctMapToken: null);
                }

                TryCatch<DistinctMap> tryCreateDistinctMap = DistinctMap.TryCreate(
                    distinctQueryType,
                    distinctContinuationToken.DistinctMapToken);
                if (!tryCreateDistinctMap.Succeeded)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(tryCreateDistinctMap.Exception);
                }

                TryCatch<IQueryPipelineStage> tryCreateSource = monadicCreatePipelineStage(distinctContinuationToken.SourceToken, cancellationToken);
                if (!tryCreateSource.Succeeded)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(tryCreateSource.Exception);
                }

                return TryCatch<IQueryPipelineStage>.FromResult(
                    new ComputeDistinctQueryPipelineStage(
                        tryCreateDistinctMap.Result,
                        tryCreateSource.Result,
                        cancellationToken));
            }

            protected override async Task<TryCatch<QueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
            {
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

                QueryState queryState;
                if (sourcePage.State != null)
                {
                    DistinctContinuationToken distinctContinuationToken = new DistinctContinuationToken(
                        sourceToken: sourcePage.State.Value,
                        distinctMapToken: this.distinctMap.GetCosmosElementContinuationToken());
                    queryState = new QueryState(DistinctContinuationToken.ToCosmosElement(distinctContinuationToken));
                }
                else
                {
                    queryState = null;
                }

                QueryPage queryPage = new QueryPage(
                    documents: distinctResults,
                    requestCharge: sourcePage.RequestCharge,
                    activityId: sourcePage.ActivityId,
                    responseLengthInBytes: sourcePage.ResponseLengthInBytes,
                    cosmosQueryExecutionInfo: sourcePage.CosmosQueryExecutionInfo,
                    disallowContinuationTokenMessage: ComputeDistinctQueryPipelineStage.UseTryGetContinuationTokenMessage,
                    state: queryState);

                return TryCatch<QueryPage>.FromResult(queryPage);
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
        }
    }
}