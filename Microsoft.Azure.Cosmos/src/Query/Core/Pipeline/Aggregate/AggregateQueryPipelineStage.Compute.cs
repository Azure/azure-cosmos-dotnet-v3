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

    internal abstract partial class AggregateQueryPipelineStage : QueryPipelineStageBase
    {
        private static readonly IReadOnlyList<CosmosElement> EmptyResults = new List<CosmosElement>().AsReadOnly();

        private sealed class ComputeAggregateQueryPipelineStage : AggregateQueryPipelineStage
        {
            private static readonly CosmosString DoneSourceToken = CosmosString.Create("DONE");

            private ComputeAggregateQueryPipelineStage(
                IQueryPipelineStage source,
                SingleGroupAggregator singleGroupAggregator,
                bool isValueAggregateQuery)
                : base(source, singleGroupAggregator, isValueAggregateQuery)
            {
                // all the work is done in the base constructor.
            }

            public static async Task<TryCatch<IQueryPipelineStage>> TryCreateAsync(
                IReadOnlyList<AggregateOperator> aggregates,
                IReadOnlyDictionary<string, AggregateOperator?> aliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                bool hasSelectValue,
                CosmosElement continuationToken,
                Func<CosmosElement, Task<TryCatch<IQueryPipelineStage>>> tryCreateSourceAsync)
            {
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
                    aggregateContinuationToken = new AggregateContinuationToken(null, null);
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
                    tryCreateSource = TryCatch<IQueryPipelineStage>.FromResult(FinishedQueryPipelineStage.Value);
                }
                else
                {
                    tryCreateSource = await tryCreateSourceAsync(aggregateContinuationToken.SourceContinuationToken);
                }

                if (tryCreateSource.Failed)
                {
                    return tryCreateSource;
                }

                ComputeAggregateQueryPipelineStage stage = new ComputeAggregateQueryPipelineStage(
                    tryCreateSource.Result,
                    tryCreateSingleGroupAggregator.Result,
                    hasSelectValue);

                return TryCatch<IQueryPipelineStage>.FromResult(stage);
            }

            protected override async Task<TryCatch<QueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Draining aggregates is broken down into two stages
                if (!this.inputStage.HasMoreResults)
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

                    return TryCatch<QueryPage>.FromResult(finalPage);
                }

                // Stage 1:
                // Drain the aggregates fully from all continuations and all partitions
                // And return empty pages in the meantime.
                await this.inputStage.MoveNextAsync();
                TryCatch<QueryPage> tryGetSourcePage = this.inputStage.Current;
                if (tryGetSourcePage.Failed)
                {
                    return tryGetSourcePage;
                }

                QueryPage sourcePage = tryGetSourcePage.Result;
                foreach (CosmosElement element in sourcePage.Documents)
                {
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

                return TryCatch<QueryPage>.FromResult(emptyPage);
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
