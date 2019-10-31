//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;

    /// <summary>
    /// Execution component that is able to aggregate local aggregates from multiple continuations and partitions.
    /// At a high level aggregates queries only return a "partial" aggregate.
    /// "partial" means that the result is only valid for that one continuation (and one partition).
    /// For example suppose you have the query "SELECT COUNT(1) FROM c" and you have a single partition collection, 
    /// then you will get one count for each continuation of the query.
    /// If you wanted the true result for this query, then you will have to take the sum of all continuations.
    /// The reason why we have multiple continuations is because for a long running query we have to break up the results into multiple continuations.
    /// Fortunately all the aggregates can be aggregated across continuations and partitions.
    /// </summary>
    internal abstract partial class AggregateDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
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
        private readonly bool isValueAggregateQuery;

        /// <summary>
        /// Initializes a new instance of the AggregateDocumentQueryExecutionComponent class.
        /// </summary>
        /// <param name="source">The source component that will supply the local aggregates from multiple continuations and partitions.</param>
        /// <param name="singleGroupAggregator">The single group aggregator that we will feed results into.</param>
        /// <param name="isValueAggregateQuery">Whether or not the query has the 'VALUE' keyword.</param>
        /// <remarks>This constructor is private since there is some async initialization that needs to happen in CreateAsync().</remarks>
        protected AggregateDocumentQueryExecutionComponent(
            IDocumentQueryExecutionComponent source,
            SingleGroupAggregator singleGroupAggregator,
            bool isValueAggregateQuery)
            : base(source)
        {
            if (singleGroupAggregator == null)
            {
                throw new ArgumentNullException(nameof(singleGroupAggregator));
            }

            this.singleGroupAggregator = singleGroupAggregator;
            this.isValueAggregateQuery = isValueAggregateQuery;
        }

        /// <summary>
        /// Creates a AggregateDocumentQueryExecutionComponent.
        /// </summary>
        /// <param name="executionEnvironment">The environment to execute on.</param>
        /// <param name="queryClient">The query client.</param>
        /// <param name="aggregates">The aggregates.</param>
        /// <param name="aliasToAggregateType">The alias to aggregate type.</param>
        /// <param name="orderedAliases">The ordering of the aliases.</param>
        /// <param name="hasSelectValue">Whether or not the query has the 'VALUE' keyword.</param>
        /// <param name="requestContinuation">The continuation token to resume from.</param>
        /// <param name="createSourceCallback">The callback to create the source component that supplies the local aggregates.</param>
        /// <returns>The AggregateDocumentQueryExecutionComponent.</returns>
        public static async Task<IDocumentQueryExecutionComponent> CreateAsync(
            ExecutionEnvironment executionEnvironment,
            CosmosQueryClient queryClient,
            AggregateOperator[] aggregates,
            IReadOnlyDictionary<string, AggregateOperator?> aliasToAggregateType,
            IReadOnlyList<string> orderedAliases,
            bool hasSelectValue,
            string requestContinuation,
            Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback)
        {
            IDocumentQueryExecutionComponent aggregateDocumentQueryExecutionComponent;
            switch (executionEnvironment)
            {
                case ExecutionEnvironment.Client:
                    aggregateDocumentQueryExecutionComponent = await ClientAggregateDocumentQueryExecutionComponent.CreateAsync(
                        queryClient,
                        aggregates,
                        aliasToAggregateType,
                        orderedAliases,
                        hasSelectValue,
                        requestContinuation,
                        createSourceCallback);
                    break;

                case ExecutionEnvironment.Compute:
                    aggregateDocumentQueryExecutionComponent = await ComputeAggregateDocumentQueryExecutionComponent.CreateAsync(
                        queryClient,
                        aggregates,
                        aliasToAggregateType,
                        orderedAliases,
                        hasSelectValue,
                        requestContinuation,
                        createSourceCallback);
                    break;

                default:
                    throw new ArgumentException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}.");
            }

            return aggregateDocumentQueryExecutionComponent;
        }

        /// <summary>
        /// Struct for getting the payload out of the rewritten projection.
        /// </summary>
        private struct RewrittenAggregateProjections
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
                        throw new ArgumentException($"{nameof(RewrittenAggregateProjections)} was not an array for a value aggregate query. Type is: {raw.Type}");
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

                    // SELECT {"$1": {"item": {"sum": SUM(c.blah), "count": COUNT(c.blah)}}} AS payload
                    if (cosmosPayload == null)
                    {
                        throw new ArgumentException($"{nameof(RewrittenAggregateProjections)} does not have a 'payload' property.");
                    }

                    this.Payload = cosmosPayload;
                }
            }

            public CosmosElement Payload
            {
                get;
            }
        }
    }
}
