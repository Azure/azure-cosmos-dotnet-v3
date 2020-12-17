// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.DCount
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate.Aggregators;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;

    internal abstract partial class DCountQueryPipelineStage : QueryPipelineStageBase
    {
        private static readonly IReadOnlyList<CosmosElement> EmptyResults = new List<CosmosElement>().AsReadOnly();

        private sealed class ComputeDCountQueryPipelineStage : DCountQueryPipelineStage
        {
            private static readonly CosmosString DoneSourceToken = CosmosString.Create("DONE");

            private ComputeDCountQueryPipelineStage(
                IQueryPipelineStage source,
                IAggregator countAggregator,
                DCountInfo info,
                CancellationToken cancellationToken)
                : base(source, countAggregator, info, cancellationToken)
            {
                // all the work is done in the base constructor.
            }

            public static TryCatch<IQueryPipelineStage> MonadicCreate(
                DCountInfo info,
                CosmosElement continuationToken,
                CancellationToken cancellationToken,
                MonadicCreatePipelineStage monadicCreatePipelineStage)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DCountContinuationToken dcountContinuationToken;
                if (continuationToken != null)
                {
                    if (!DCountContinuationToken.TryCreateFromCosmosElement(
                        continuationToken,
                        out dcountContinuationToken))
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
                            new MalformedContinuationTokenException(
                                $"Malfomed {nameof(DCountContinuationToken)}: '{continuationToken}'"));
                    }
                }
                else
                {
                    dcountContinuationToken = new DCountContinuationToken(countAggregatorContinuationToken: null, sourceContinuationToken: null);
                }

                TryCatch<IAggregator> tryCreateSingleGroupAggregator = CountAggregator.TryCreate(
                    continuationToken: dcountContinuationToken.CountAggregatorContinuationToken);
                if (tryCreateSingleGroupAggregator.Failed)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(tryCreateSingleGroupAggregator.Exception);
                }

                TryCatch<IQueryPipelineStage> tryCreateSource;
                if (dcountContinuationToken.SourceContinuationToken is CosmosString stringToken && (stringToken.Value == DoneSourceToken.Value))
                {
                    tryCreateSource = TryCatch<IQueryPipelineStage>.FromResult(EmptyQueryPipelineStage.Singleton);
                }
                else
                {
                    tryCreateSource = monadicCreatePipelineStage(dcountContinuationToken.SourceContinuationToken, cancellationToken);
                }

                if (tryCreateSource.Failed)
                {
                    return tryCreateSource;
                }

                ComputeDCountQueryPipelineStage stage = new ComputeDCountQueryPipelineStage(
                    tryCreateSource.Result,
                    tryCreateSingleGroupAggregator.Result,
                    info,
                    cancellationToken);

                return TryCatch<IQueryPipelineStage>.FromResult(stage);
            }

            public override async ValueTask<bool> MoveNextAsync(ITrace trace)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                if (trace == null)
                {
                    throw new ArgumentNullException(nameof(trace));
                }

                if (this.returnedFinalPage)
                {
                    this.Current = default;
                    return false;
                }

                // Draining aggregates is broken down into two stages
                QueryPage queryPage;
                if (await this.inputStage.MoveNextAsync(trace))
                {
                    // Stage 1:
                    // Drain the aggregates fully from all continuations and all partitions
                    // And return empty pages in the meantime.
                    TryCatch<QueryPage> tryGetSourcePage = this.inputStage.Current;
                    if (tryGetSourcePage.Failed)
                    {
                        this.Current = tryGetSourcePage;
                        return true;
                    }

                    QueryPage sourcePage = tryGetSourcePage.Result;
                    this.cancellationToken.ThrowIfCancellationRequested();
                    this.countAggregator.Aggregate(CosmosNumber64.Create(sourcePage.Documents.Count));

                    DCountContinuationToken dcountContinuationToken = new DCountContinuationToken(
                        countAggregatorContinuationToken: this.countAggregator.GetCosmosElementContinuationToken(),
                        sourceContinuationToken: sourcePage.State != null ? sourcePage.State.Value : DoneSourceToken);
                    QueryState queryState = new QueryState(DCountContinuationToken.ToCosmosElement(dcountContinuationToken));
                    QueryPage emptyPage = new QueryPage(
                        documents: EmptyResults,
                        requestCharge: sourcePage.RequestCharge,
                        activityId: sourcePage.ActivityId,
                        responseLengthInBytes: sourcePage.ResponseLengthInBytes,
                        cosmosQueryExecutionInfo: sourcePage.CosmosQueryExecutionInfo,
                        disallowContinuationTokenMessage: sourcePage.DisallowContinuationTokenMessage,
                        state: queryState);

                    queryPage = emptyPage;
                }
                else
                {
                    // Stage 2:
                    // Return the final page after draining.
                    List<CosmosElement> finalResult = new List<CosmosElement>();
                    CosmosElement aggregationResult = this.GetFinalResult();
                    if (aggregationResult != null)
                    {
                        finalResult.Add(aggregationResult);
                    }

                    QueryPage finalPage = new QueryPage(
                        documents: finalResult,
                        requestCharge: default,
                        activityId: default,
                        responseLengthInBytes: default,
                        cosmosQueryExecutionInfo: default,
                        disallowContinuationTokenMessage: default,
                        state: default);

                    queryPage = finalPage;
                    this.returnedFinalPage = true;
                }

                this.Current = TryCatch<QueryPage>.FromResult(queryPage);
                return true;
            }

            private sealed class DCountContinuationToken
            {
                private const string SourceTokenName = "SourceToken";
                private const string DCountTokenName = "DCountToken";

                public DCountContinuationToken(
                    CosmosElement countAggregatorContinuationToken,
                    CosmosElement sourceContinuationToken)
                {
                    this.CountAggregatorContinuationToken = countAggregatorContinuationToken;
                    this.SourceContinuationToken = sourceContinuationToken;
                }

                public CosmosElement CountAggregatorContinuationToken { get; }

                public CosmosElement SourceContinuationToken { get; }

                public static CosmosElement ToCosmosElement(DCountContinuationToken dcountContinuationToken)
                {
                    Dictionary<string, CosmosElement> dictionary = new Dictionary<string, CosmosElement>()
                    {
                        {
                            DCountContinuationToken.SourceTokenName,
                            dcountContinuationToken.SourceContinuationToken
                        },
                        {
                            DCountContinuationToken.DCountTokenName,
                            dcountContinuationToken.CountAggregatorContinuationToken
                        }
                    };

                    return CosmosObject.Create(dictionary);
                }

                public static bool TryCreateFromCosmosElement(
                    CosmosElement continuationToken,
                    out DCountContinuationToken aggregateContinuationToken)
                {
                    if (continuationToken == null)
                    {
                        throw new ArgumentNullException(nameof(continuationToken));
                    }

                    if (!(continuationToken is CosmosObject rawAggregateContinuationToken))
                    {
                        aggregateContinuationToken = default;
                        return false;
                    }

                    if (!rawAggregateContinuationToken.TryGetValue(
                        DCountContinuationToken.DCountTokenName,
                        out CosmosElement countAggregatorContinuationToken))
                    {
                        aggregateContinuationToken = default;
                        return false;
                    }

                    if (!rawAggregateContinuationToken.TryGetValue(
                        DCountContinuationToken.SourceTokenName,
                        out CosmosElement sourceContinuationToken))
                    {
                        aggregateContinuationToken = default;
                        return false;
                    }

                    aggregateContinuationToken = new DCountContinuationToken(
                        countAggregatorContinuationToken: countAggregatorContinuationToken,
                        sourceContinuationToken: sourceContinuationToken);
                    return true;
                }
            }
        }
    }
}
