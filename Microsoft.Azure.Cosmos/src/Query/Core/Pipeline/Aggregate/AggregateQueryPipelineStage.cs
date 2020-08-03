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
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate.Aggregators;
    using static Microsoft.Azure.Cosmos.Query.Core.Pipeline.PipelineFactory;

    /// <summary>
    /// Stage that is able to aggregate local aggregates from multiple continuations and partitions.
    /// At a high level aggregates queries only return a "partial" aggregate.
    /// "partial" means that the result is only valid for that one continuation (and one partition).
    /// For example suppose you have the query "SELECT COUNT(1) FROM c" and you have a single partition collection, 
    /// then you will get one count for each continuation of the query.
    /// If you wanted the true result for this query, then you will have to take the sum of all continuations.
    /// The reason why we have multiple continuations is because for a long running query we have to break up the results into multiple continuations.
    /// Fortunately all the aggregates can be aggregated across continuations and partitions.
    /// </summary>
    internal abstract partial class AggregateQueryPipelineStage : QueryPipelineStageBase
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

        /// <summary>
        /// Initializes a new instance of the AggregateDocumentQueryExecutionComponent class.
        /// </summary>
        /// <param name="source">The source component that will supply the local aggregates from multiple continuations and partitions.</param>
        /// <param name="singleGroupAggregator">The single group aggregator that we will feed results into.</param>
        /// <param name="isValueQuery">Whether or not the query has the 'VALUE' keyword.</param>
        /// <remarks>This constructor is private since there is some async initialization that needs to happen in CreateAsync().</remarks>
        public AggregateQueryPipelineStage(
            IQueryPipelineStage source,
            SingleGroupAggregator singleGroupAggregator,
            bool isValueQuery)
            : base(source)
        {
            this.singleGroupAggregator = singleGroupAggregator ?? throw new ArgumentNullException(nameof(singleGroupAggregator));
            this.isValueQuery = isValueQuery;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            ExecutionEnvironment executionEnvironment,
            IReadOnlyList<AggregateOperator> aggregates,
            IReadOnlyDictionary<string, AggregateOperator?> aliasToAggregateType,
            IReadOnlyList<string> orderedAliases,
            bool hasSelectValue,
            CosmosElement continuationToken,
            MonadicCreatePipelineStage monadicCreatePipelineStage) => executionEnvironment switch
            {
                ExecutionEnvironment.Client => ClientAggregateQueryPipelineStage.MonadicCreate(
                    aggregates,
                    aliasToAggregateType,
                    orderedAliases,
                    hasSelectValue,
                    continuationToken,
                    monadicCreatePipelineStage),
                ExecutionEnvironment.Compute => ComputeAggregateQueryPipelineStage.MonadicCreate(
                    aggregates,
                    aliasToAggregateType,
                    orderedAliases,
                    hasSelectValue,
                    continuationToken,
                    monadicCreatePipelineStage),
                _ => throw new ArgumentException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}."),
            };

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
