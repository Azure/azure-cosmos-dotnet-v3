// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate.Aggregators;
    using Microsoft.Azure.Cosmos.Tracing;

    internal abstract partial class AggregateQueryPipelineStage : QueryPipelineStageBase
    {
        private static readonly IReadOnlyList<CosmosElement> EmptyResults = new List<CosmosElement>().AsReadOnly();

        private sealed class ComputeAggregateQueryPipelineStage : AggregateQueryPipelineStage
        {
            private static readonly CosmosString DoneSourceToken = CosmosString.Create("DONE");

            private ComputeAggregateQueryPipelineStage(
                IQueryPipelineStage source,
                SingleGroupAggregator singleGroupAggregator,
                bool isValueAggregateQuery,
                CancellationToken cancellationToken)
                : base(source, singleGroupAggregator, isValueAggregateQuery, cancellationToken)
            {
                // all the work is done in the base constructor.
            }

            public static TryCatch<IQueryPipelineStage> MonadicCreate(
                IReadOnlyList<AggregateOperator> aggregates,
                IReadOnlyDictionary<string, AggregateOperator?> aliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                bool hasSelectValue,
                CosmosElement continuationToken,
                CancellationToken cancellationToken,
                MonadicCreatePipelineStage monadicCreatePipelineStage)
            {
                cancellationToken.ThrowIfCancellationRequested();

                AggregateContinuationToken aggregateContinuationToken;
                if (continuationToken != null)
                {
                    if (!AggregateContinuationToken.TryCreateFromCosmosElement(
                        continuationToken,
                        out aggregateContinuationToken))
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
                            new MalformedContinuationTokenException(
                                $"Malfomed {nameof(AggregateContinuationToken)}: '{continuationToken}'"));
                    }
                }
                else
                {
                    aggregateContinuationToken = new AggregateContinuationToken(singleGroupAggregatorContinuationToken: null, sourceContinuationToken: null);
                }

                TryCatch<SingleGroupAggregator> tryCreateSingleGroupAggregator = SingleGroupAggregator.TryCreate(
                    aggregates,
                    aliasToAggregateType,
                    orderedAliases,
                    hasSelectValue,
                    aggregateContinuationToken.SingleGroupAggregatorContinuationToken);
                if (tryCreateSingleGroupAggregator.Failed)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(tryCreateSingleGroupAggregator.Exception);
                }

                TryCatch<IQueryPipelineStage> tryCreateSource;
                if (aggregateContinuationToken.SourceContinuationToken is CosmosString stringToken && (stringToken.Value == DoneSourceToken.Value))
                {
                    tryCreateSource = TryCatch<IQueryPipelineStage>.FromResult(EmptyQueryPipelineStage.Singleton);
                }
                else
                {
                    tryCreateSource = monadicCreatePipelineStage(aggregateContinuationToken.SourceContinuationToken, cancellationToken);
                }

                if (tryCreateSource.Failed)
                {
                    return tryCreateSource;
                }

                ComputeAggregateQueryPipelineStage stage = new ComputeAggregateQueryPipelineStage(
                    tryCreateSource.Result,
                    tryCreateSingleGroupAggregator.Result,
                    hasSelectValue,
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
                    foreach (CosmosElement element in sourcePage.Documents)
                    {
                        this.cancellationToken.ThrowIfCancellationRequested();

                        RewrittenAggregateProjections rewrittenAggregateProjections = new RewrittenAggregateProjections(
                            this.isValueQuery,
                            element);
                        this.singleGroupAggregator.AddValues(rewrittenAggregateProjections.Payload);
                    }

                    AggregateContinuationToken aggregateContinuationToken = new AggregateContinuationToken(
                        singleGroupAggregatorContinuationToken: this.singleGroupAggregator.GetCosmosElementContinuationToken(),
                        sourceContinuationToken: sourcePage.State != null ? sourcePage.State.Value : DoneSourceToken);
                    QueryState queryState = new QueryState(AggregateContinuationToken.ToCosmosElement(aggregateContinuationToken));
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
                    CosmosElement aggregationResult = this.singleGroupAggregator.GetResult();
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

            private sealed class AggregateContinuationToken
            {
                private const string SourceTokenName = "SourceToken";
                private const string AggregationTokenName = "AggregationToken";

                public AggregateContinuationToken(
                    CosmosElement singleGroupAggregatorContinuationToken,
                    CosmosElement sourceContinuationToken)
                {
                    this.SingleGroupAggregatorContinuationToken = singleGroupAggregatorContinuationToken;
                    this.SourceContinuationToken = sourceContinuationToken;
                }

                public CosmosElement SingleGroupAggregatorContinuationToken { get; }

                public CosmosElement SourceContinuationToken { get; }

                public static CosmosElement ToCosmosElement(AggregateContinuationToken aggregateContinuationToken)
                {
                    Dictionary<string, CosmosElement> dictionary = new Dictionary<string, CosmosElement>()
                    {
                        {
                            AggregateContinuationToken.SourceTokenName,
                            aggregateContinuationToken.SourceContinuationToken
                        },
                        {
                            AggregateContinuationToken.AggregationTokenName,
                            aggregateContinuationToken.SingleGroupAggregatorContinuationToken
                        }
                    };

                    return CosmosObject.Create(dictionary);
                }

                public static bool TryCreateFromCosmosElement(
                    CosmosElement continuationToken,
                    out AggregateContinuationToken aggregateContinuationToken)
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
                        AggregateContinuationToken.AggregationTokenName,
                        out CosmosElement singleGroupAggregatorContinuationToken))
                    {
                        aggregateContinuationToken = default;
                        return false;
                    }

                    if (!rawAggregateContinuationToken.TryGetValue(
                        AggregateContinuationToken.SourceTokenName,
                        out CosmosElement sourceContinuationToken))
                    {
                        aggregateContinuationToken = default;
                        return false;
                    }

                    aggregateContinuationToken = new AggregateContinuationToken(
                        singleGroupAggregatorContinuationToken: singleGroupAggregatorContinuationToken,
                        sourceContinuationToken: sourceContinuationToken);
                    return true;
                }
            }
        }
    }
}
