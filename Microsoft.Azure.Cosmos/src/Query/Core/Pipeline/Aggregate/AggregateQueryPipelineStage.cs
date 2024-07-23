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
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate.Aggregators;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using static IndexUtilizationHelper;

    internal class AggregateQueryPipelineStage : QueryPipelineStageBase
    {
        /// <summary>
        /// This class does most of the work, since a query like:
        /// 
        /// SELECT VALUE AVG(c.age)
        /// FROM c
        /// 
        /// is really just an aggregation on a single grouping (the whole collection).
        /// </summary>
        private readonly SingleGroupAggregator singleGroupAggregator;

        /// <summary>
        /// We need to keep track of whether the projection has the 'VALUE' keyword.
        /// </summary>
        private readonly bool isValueQuery;

        protected bool returnedFinalPage;

        /// <summary>
        /// Initializes a new instance of the AggregateDocumentQueryExecutionComponent class.
        /// </summary>
        /// <param name="source">The source component that will supply the local aggregates from multiple continuations and partitions.</param>
        /// <param name="singleGroupAggregator">The single group aggregator that we will feed results into.</param>
        /// <param name="isValueQuery">Whether or not the query has the 'VALUE' keyword.</param>
        /// <remarks>This constructor is private since there is some async initialization that needs to happen in CreateAsync().</remarks>
        private AggregateQueryPipelineStage(
            IQueryPipelineStage source,
            SingleGroupAggregator singleGroupAggregator,
            bool isValueQuery)
            : base(source)
        {
            this.singleGroupAggregator = singleGroupAggregator ?? throw new ArgumentNullException(nameof(singleGroupAggregator));
            this.isValueQuery = isValueQuery;
        }

        public override async ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (this.returnedFinalPage)
            {
                return false;
            }

            // Note-2016-10-25-felixfan: Given what we support now, we should expect to return only 1 document.
            // Note-2019-07-11-brchon: We can return empty pages until all the documents are drained,
            // but then we will have to design a continuation token.

            double requestCharge = 0;
            IReadOnlyDictionary<string, string> cumulativeAdditionalHeaders = default;

            while (await this.inputStage.MoveNextAsync(trace, cancellationToken))
            {
                TryCatch<QueryPage> tryGetPageFromSource = this.inputStage.Current;
                if (tryGetPageFromSource.Failed)
                {
                    this.Current = tryGetPageFromSource;
                    return true;
                }

                QueryPage sourcePage = tryGetPageFromSource.Result;

                requestCharge += sourcePage.RequestCharge;

                cumulativeAdditionalHeaders = AccumulateIndexUtilization(
                    cumulativeHeaders: cumulativeAdditionalHeaders,
                    currentHeaders: sourcePage.AdditionalHeaders);

                foreach (CosmosElement element in sourcePage.Documents)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    RewrittenAggregateProjections rewrittenAggregateProjections = new RewrittenAggregateProjections(
                        this.isValueQuery,
                        element);
                    this.singleGroupAggregator.AddValues(rewrittenAggregateProjections.Payload);
                }
            }

            List<CosmosElement> finalResult = new List<CosmosElement>();
            CosmosElement aggregationResult = this.singleGroupAggregator.GetResult();
            if (aggregationResult != null)
            {
                finalResult.Add(aggregationResult);
            }

            QueryPage queryPage = new QueryPage(
                documents: finalResult,
                requestCharge: requestCharge,
                activityId: default,
                cosmosQueryExecutionInfo: default,
                distributionPlanSpec: default,
                disallowContinuationTokenMessage: default,
                additionalHeaders: cumulativeAdditionalHeaders,
                state: default,
                streaming: default);

            this.Current = TryCatch<QueryPage>.FromResult(queryPage);
            this.returnedFinalPage = true;
            return true;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            IReadOnlyList<AggregateOperator> aggregates,
            IReadOnlyDictionary<string, AggregateOperator?> aliasToAggregateType,
            IReadOnlyList<string> orderedAliases,
            bool hasSelectValue,
            CosmosElement continuationToken,
            MonadicCreatePipelineStage monadicCreatePipelineStage)
        {
            if (monadicCreatePipelineStage == null)
            {
                throw new ArgumentNullException(nameof(monadicCreatePipelineStage));
            }

            TryCatch<SingleGroupAggregator> tryCreateSingleGroupAggregator = SingleGroupAggregator.TryCreate(
                aggregates,
                aliasToAggregateType,
                orderedAliases,
                hasSelectValue,
                continuationToken: null);
            if (tryCreateSingleGroupAggregator.Failed)
            {
                return TryCatch<IQueryPipelineStage>.FromException(tryCreateSingleGroupAggregator.Exception);
            }

            TryCatch<IQueryPipelineStage> tryCreateSource = monadicCreatePipelineStage(continuationToken);
            if (tryCreateSource.Failed)
            {
                return tryCreateSource;
            }

            AggregateQueryPipelineStage stage = new AggregateQueryPipelineStage(
                tryCreateSource.Result,
                tryCreateSingleGroupAggregator.Result,
                hasSelectValue);

            return TryCatch<IQueryPipelineStage>.FromResult(stage);
        }

        /// <summary>
        /// Struct for getting the payload out of the rewritten projection.
        /// </summary>
        private readonly struct RewrittenAggregateProjections
        {
            public RewrittenAggregateProjections(bool isValueAggregateQuery, CosmosElement raw)
            {
                if (raw == null)
                {
                    throw new ArgumentNullException(nameof(raw));
                }

                if (isValueAggregateQuery)
                {
                    // SELECT VALUE [{"item": {"sum": SUM(c.blah), "count": COUNT(c.blah)}}]
                    if (!(raw is CosmosArray aggregates))
                    {
                        throw new ArgumentException($"{nameof(RewrittenAggregateProjections)} was not an array for a value aggregate query. Type is: {raw.GetType()}");
                    }

                    this.Payload = aggregates[0];
                }
                else
                {
                    if (!(raw is CosmosObject cosmosObject))
                    {
                        throw new ArgumentException($"{nameof(raw)} must not be an object.");
                    }

                    if (!cosmosObject.TryGetValue("payload", out CosmosElement cosmosPayload))
                    {
                        throw new InvalidOperationException($"Underlying object does not have an 'payload' field.");
                    }

                    this.Payload = cosmosPayload ?? throw new ArgumentException($"{nameof(RewrittenAggregateProjections)} does not have a 'payload' property.");
                }
            }

            public CosmosElement Payload { get; }
        }
    }
}
