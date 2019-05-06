//-----------------------------------------------------------------------
// <copyright file="AggregateDocumentQueryExecutionComponent.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query.Aggregation;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// Execution component that is able to aggregate local aggregates from multiple continuations and partitions.
    /// At a high level aggregates queries only return a local aggregate meaning that the value that is returned is only valid for that one continuation (and one partition).
    /// For example suppose you have the query "SELECT Count(1) from c" and you have a single partition collection, 
    /// then you will get one count for each continuation of the query.
    /// If you wanted the true result for this query, then you will have to take the sum of all continuations.
    /// The reason why we have multiple continuations is because for a long running query we have to break up the results into multiple continuations.
    /// Fortunately all the aggregates can be aggregated across continuations and partitions.
    /// </summary>
    internal sealed class AggregateDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        /// <summary>
        /// aggregators[i] is the i'th aggregate in this query execution component.
        /// </summary>
        private readonly IAggregator[] aggregators;

        /// <summary>
        /// Initializes a new instance of the AggregateDocumentQueryExecutionComponent class.
        /// </summary>
        /// <param name="source">The source component that will supply the local aggregates from multiple continuations and partitions.</param>
        /// <param name="aggregateOperators">The aggregate operators for this query.</param>
        /// <remarks>This constructor is private since there is some async initialization that needs to happen in CreateAsync().</remarks>
        private AggregateDocumentQueryExecutionComponent(IDocumentQueryExecutionComponent source, AggregateOperator[] aggregateOperators)
            : base(source)
        {
            this.aggregators = new IAggregator[aggregateOperators.Length];
            for (int i = 0; i < aggregateOperators.Length; ++i)
            {
                switch (aggregateOperators[i])
                {
                    case AggregateOperator.Average:
                        this.aggregators[i] = new AverageAggregator();
                        break;
                    case AggregateOperator.Count:
                        this.aggregators[i] = new CountAggregator();
                        break;
                    case AggregateOperator.Max:
                        this.aggregators[i] = new MinMaxAggregator(false);
                        break;
                    case AggregateOperator.Min:
                        this.aggregators[i] = new MinMaxAggregator(true);
                        break;
                    case AggregateOperator.Sum:
                        this.aggregators[i] = new SumAggregator();
                        break;
                    default:
                        string errorMessage = "Unexpected value: " + aggregateOperators[i].ToString();
                        Debug.Assert(false, errorMessage);
                        throw new InvalidProgramException(errorMessage);
                }
            }
        }

        /// <summary>
        /// Creates a AggregateDocumentQueryExecutionComponent.
        /// </summary>
        /// <param name="aggregateOperators">The aggregate operators for this query.</param>
        /// <param name="requestContinuation">The continuation token to resume from.</param>
        /// <param name="createSourceCallback">The callback to create the source component that supplies the local aggregates.</param>
        /// <returns>The AggregateDocumentQueryExecutionComponent.</returns>
        public static async Task<AggregateDocumentQueryExecutionComponent> CreateAsync(
            AggregateOperator[] aggregateOperators,
            string requestContinuation,
            Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback)
        {
            return new AggregateDocumentQueryExecutionComponent(await createSourceCallback(requestContinuation), aggregateOperators);
        }

        /// <summary>
        /// Drains at most 'maxElements' documents from the <see cref="AggregateDocumentQueryExecutionComponent"/> .
        /// </summary>
        /// <param name="maxElements">This value is ignored, since the aggregates are aggregated for you.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The aggregate result after all the continuations have been followed.</returns>
        /// <remarks>
        /// Note that this functions follows all continuations meaning that it won't return until all continuations are drained.
        /// This means that if you have a long running query this function will take a very long time to return.
        /// </remarks>
        public override async Task<CosmosQueryResponse> DrainAsync(int maxElements, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            // Note-2016-10-25-felixfan: Given what we support now, we should expect to return only 1 document.
            double requestCharge = 0;
            long responseLengthBytes = 0;
            List<Uri> replicaUris = new List<Uri>();
            ClientSideRequestStatistics requestStatistics = new ClientSideRequestStatistics();
            PartitionedQueryMetrics partitionedQueryMetrics = new PartitionedQueryMetrics();

            while (!this.IsDone)
            {
                CosmosQueryResponse result = await base.DrainAsync(int.MaxValue, token);
                if (!result.IsSuccessStatusCode)
                {
                    return result;
                }

                requestCharge += result.Headers.RequestCharge;
                responseLengthBytes += result.ResponseLengthBytes;
                //partitionedQueryMetrics += new PartitionedQueryMetrics(result.QueryMetrics);
                if (result.RequestStatistics != null)
                {
                    replicaUris.AddRange(result.RequestStatistics.ContactedReplicas);
                }

                foreach (CosmosElement item in result.CosmosElements)
                {
                    if (!(item is CosmosArray comosArray))
                    {
                        throw new InvalidOperationException("Expected an array of aggregate results from the execution context.");
                    }

                    List<AggregateItem> aggregateItems = new List<AggregateItem>();
                    foreach (CosmosElement arrayItem in comosArray)
                    {
                        aggregateItems.Add(new AggregateItem(arrayItem));
                    }

                    Debug.Assert(
                        aggregateItems.Count == this.aggregators.Length,
                        $"Expected {this.aggregators.Length} values, but received {aggregateItems.Count}.");

                    for (int i = 0; i < this.aggregators.Length; ++i)
                    {
                        this.aggregators[i].Aggregate(aggregateItems[i].Item);
                    }
                }
            }

            List<CosmosElement> finalResult = this.BindAggregateResults(
                this.aggregators.Select(aggregator => aggregator.GetResult()));

            // The replicaUris may have duplicates.
            requestStatistics.ContactedReplicas.AddRange(replicaUris);

            return CosmosQueryResponse.CreateSuccess(
                result: finalResult,
                count: finalResult.Count,
                responseLengthBytes: responseLengthBytes,
                responseHeaders: new CosmosQueryResponseMessageHeaders(continauationToken: null, disallowContinuationTokenMessage: null)
                {
                    RequestCharge = requestCharge
                });
        }

        /// <summary>
        /// Filters out all the aggregate results that are Undefined.
        /// </summary>
        /// <param name="aggregateResults">The result for each aggregator.</param>
        /// <returns>The aggregate results that are not Undefined.</returns>
        private List<CosmosElement> BindAggregateResults(IEnumerable<CosmosElement> aggregateResults)
        {
            // Note-2016-11-08-felixfan: Given what we support now, we should expect aggregateResults.Length == 1.
            // Note-2018-03-07-brchon: This is because we only support aggregate queries like "SELECT VALUE max(c.blah) from c"
            // and that is because it allows us to sum the local maxes and also avoids queries like "SELECT ABS(max(c.blah)) / 10 from c",
            // which would require more static analysis to pull off.
            string assertMessage = "Only support binding 1 aggregate function to projection.";
            Debug.Assert(this.aggregators.Length == 1, assertMessage);
            if (this.aggregators.Length != 1)
            {
                throw new NotSupportedException(assertMessage);
            }

            List<CosmosElement> result = new List<CosmosElement>();
            foreach(CosmosElement aggregateResult in aggregateResults)
            {
                if (aggregateResult != null)
                {
                    result.Add(aggregateResult);
                }
            }

            return result;
        }
    }
}
