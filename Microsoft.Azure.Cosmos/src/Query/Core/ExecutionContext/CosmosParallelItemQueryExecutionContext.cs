//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Newtonsoft.Json;
    using PartitionKeyRange = Documents.PartitionKeyRange;

    /// <summary>
    /// CosmosParallelItemQueryExecutionContext is a concrete implementation for CrossPartitionQueryExecutionContext.
    /// This class is responsible for draining cross partition queries that do not have order by conditions.
    /// The way parallel queries work is that it drains from the left most partition first.
    /// This class handles draining in the correct order and can also stop and resume the query 
    /// by generating a continuation token and resuming from said continuation token.
    /// </summary>
    internal sealed class CosmosParallelItemQueryExecutionContext : CosmosCrossPartitionQueryExecutionContext
    {
        /// <summary>
        /// The comparer used to determine which document to serve next.
        /// </summary>
        private static readonly IComparer<ItemProducerTree> MoveNextComparer = new ParallelItemProducerTreeComparer();

        /// <summary>
        /// The function to determine which partition to fetch from first.
        /// </summary>
        private static readonly Func<ItemProducerTree, int> FetchPriorityFunction = documentProducerTree => int.Parse(documentProducerTree.PartitionKeyRange.Id);

        /// <summary>
        /// The comparer used to determine, which continuation tokens should be returned to the user.
        /// </summary>
        private static readonly IEqualityComparer<CosmosElement> EqualityComparer = new ParallelEqualityComparer();

        /// <summary>
        /// Initializes a new instance of the CosmosParallelItemQueryExecutionContext class.
        /// </summary>
        /// <param name="queryContext">The parameters for constructing the base class.</param>
        /// <param name="maxConcurrency">The max concurrency</param>
        /// <param name="maxBufferedItemCount">The max buffered item count</param>
        /// <param name="maxItemCount">Max item count</param>
        private CosmosParallelItemQueryExecutionContext(
            CosmosQueryContext queryContext,
            int? maxConcurrency,
            int? maxItemCount,
            int? maxBufferedItemCount)
            : base(
                queryContext: queryContext,
                maxConcurrency: maxConcurrency,
                maxItemCount: maxItemCount,
                maxBufferedItemCount: maxBufferedItemCount,
                moveNextComparer: CosmosParallelItemQueryExecutionContext.MoveNextComparer,
                fetchPrioirtyFunction: CosmosParallelItemQueryExecutionContext.FetchPriorityFunction,
                equalityComparer: CosmosParallelItemQueryExecutionContext.EqualityComparer)
        {
        }

        /// <summary>
        /// For parallel queries the continuation token semantically holds two pieces of information:
        /// 1) What physical partition did the user read up to
        /// 2) How far into said partition did they read up to
        /// And since the client consumes queries strictly in a left to right order we can partition the documents:
        /// 1) Documents left of the continuation token have been drained
        /// 2) Documents to the right of the continuation token still need to be served.
        /// This is useful since we can have a single continuation token for all partitions.
        /// </summary>
        protected override string ContinuationToken
        {
            get
            {
                if (this.IsDone)
                {
                    return null;
                }

                IEnumerable<ItemProducer> activeItemProducers = this.GetActiveItemProducers();
                return activeItemProducers.Count() > 0 ? JsonConvert.SerializeObject(
                    activeItemProducers.Select((documentProducer) => new CompositeContinuationToken
                    {
                        Token = documentProducer.PreviousContinuationToken,
                        Range = documentProducer.PartitionKeyRange.ToRange()
                    }),
                    DefaultJsonSerializationSettings.Value) : null;
            }
        }

        /// <summary>
        /// Creates a CosmosParallelItemQueryExecutionContext
        /// </summary>
        /// <param name="queryContext">The params the construct the base class.</param>
        /// <param name="initParams">The params to initialize the cross partition context.</param>
        /// <param name="requestContinuationToken">The request continuation.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on, which in turn returns a CosmosParallelItemQueryExecutionContext.</returns>
        public static async Task<CosmosParallelItemQueryExecutionContext> CreateAsync(
            CosmosQueryContext queryContext,
            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams,
            string requestContinuationToken,
            CancellationToken token)
        {
            Debug.Assert(
                !initParams.PartitionedQueryExecutionInfo.QueryInfo.HasOrderBy,
                "Parallel~Context must not have order by query info.");

            CosmosParallelItemQueryExecutionContext context = new CosmosParallelItemQueryExecutionContext(
                queryContext: queryContext,
                maxConcurrency: initParams.MaxConcurrency,
                maxItemCount: initParams.MaxItemCount,
                maxBufferedItemCount: initParams.MaxBufferedItemCount);

            await context.InitializeAsync(
                sqlQuerySpec: initParams.SqlQuerySpec,
                collectionRid: initParams.CollectionRid,
                partitionKeyRanges: initParams.PartitionKeyRanges,
                initialPageSize: initParams.InitialPageSize,
                requestContinuation: requestContinuationToken,
                token: token);

            return context;
        }

        /// <summary>
        /// Drains documents from this execution context.
        /// </summary>
        /// <param name="maxElements">The maximum number of documents to drains.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that when awaited on returns a DoucmentFeedResponse of results.</returns>
        public override async Task<IReadOnlyList<CosmosElement>> InternalDrainAsync(int maxElements, CancellationToken cancellationToken)
        {
            // In order to maintain the continuation token for the user we must drain with a few constraints
            // 1) We fully drain from the left most partition before moving on to the next partition
            // 2) We drain only full pages from the document producer so we aren't left with a partial page
            //  otherwise we would need to add to the continuation token how many items to skip over on that page.

            // Only drain from the leftmost (current) document producer tree
            ItemProducerTree currentItemProducerTree = this.PopCurrentItemProducerTree();

            // This might be the first time we have seen this document producer tree so we need to buffer documents
            if (currentItemProducerTree.Current == null)
            {
                await this.MoveNextHelperAsync(currentItemProducerTree, cancellationToken);
            }

            int itemsLeftInCurrentPage = currentItemProducerTree.ItemsLeftInCurrentPage;

            // Only drain full pages or less if this is a top query.
            List<CosmosElement> results = new List<CosmosElement>();
            for (int i = 0; i < Math.Min(itemsLeftInCurrentPage, maxElements); i++)
            {
                results.Add(currentItemProducerTree.Current);
                if (await this.MoveNextHelperAsync(currentItemProducerTree, cancellationToken))
                {
                    break;
                }
            }

            this.PushCurrentItemProducerTree(currentItemProducerTree);

            // At this point the document producer tree should have internally called MoveNextPage, since we fully drained a page.
            return results;
        }

        /// <summary>
        /// Initialize the execution context.
        /// </summary>
        /// <param name="sqlQuerySpec">SQL query spec.</param>
        /// <param name="collectionRid">The collection rid.</param>
        /// <param name="partitionKeyRanges">The partition key ranges to drain documents from.</param>
        /// <param name="initialPageSize">The initial page size.</param>
        /// <param name="requestContinuation">The continuation token to resume from.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on.</returns>
        private async Task InitializeAsync(
            SqlQuerySpec sqlQuerySpec,
            string collectionRid,
            List<PartitionKeyRange> partitionKeyRanges,
            int initialPageSize,
            string requestContinuation,
            CancellationToken token)
        {
            IReadOnlyList<PartitionKeyRange> filteredPartitionKeyRanges;
            Dictionary<string, CompositeContinuationToken> targetIndicesForFullContinuation = null;
            if (string.IsNullOrEmpty(requestContinuation))
            {
                // If no continuation token is given then we need to hit all of the supplied partition key ranges.
                filteredPartitionKeyRanges = partitionKeyRanges;
            }
            else
            {
                // If a continuation token is given then we need to figure out partition key range it maps to
                // in order to filter the partition key ranges.
                // For example if suppliedCompositeContinuationToken.Range.Min == partition3.Range.Min,
                // then we know that partitions 0, 1, 2 are fully drained.
                CompositeContinuationToken[] suppliedCompositeContinuationTokens = null;

                try
                {
                    suppliedCompositeContinuationTokens = JsonConvert.DeserializeObject<CompositeContinuationToken[]>(requestContinuation, DefaultJsonSerializationSettings.Value);
                    foreach (CompositeContinuationToken suppliedCompositeContinuationToken in suppliedCompositeContinuationTokens)
                    {
                        if (suppliedCompositeContinuationToken.Range == null || suppliedCompositeContinuationToken.Range.IsEmpty)
                        {
                            throw this.queryClient.CreateBadRequestException(
                                message: $"Invalid Range in the continuation token {requestContinuation} for Parallel~Context.");
                        }
                    }

                    if (suppliedCompositeContinuationTokens.Length == 0)
                    {
                        throw this.queryClient.CreateBadRequestException(
                            message: $"Invalid format for continuation token {requestContinuation} for Parallel~Context.");
                    }
                }
                catch (JsonException ex)
                {
                    throw this.queryClient.CreateBadRequestException(
                        message: $"Invalid JSON in continuation token {requestContinuation} for Parallel~Context, exception: {ex.Message}");
                }

                filteredPartitionKeyRanges = this.GetPartitionKeyRangesForContinuation(suppliedCompositeContinuationTokens, partitionKeyRanges, out targetIndicesForFullContinuation);
            }

            await base.InitializeAsync(
                collectionRid,
                filteredPartitionKeyRanges,
                initialPageSize,
                sqlQuerySpec,
                (targetIndicesForFullContinuation != null) ? targetIndicesForFullContinuation.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Token) : null,
                true,
                null,
                null,
                token);
        }

        /// <summary>
        /// Given a continuation token and a list of partitionKeyRanges this function will return a list of partition key ranges you should resume with.
        /// Note that the output list is just a right hand slice of the input list, since we know that for any continuation of a parallel query it is just
        /// resuming from the partition that the query left off that.
        /// </summary>
        /// <param name="suppliedCompositeContinuationTokens">The continuation tokens that the user has supplied.</param>
        /// <param name="partitionKeyRanges">The partition key ranges.</param>
        /// <param name="targetRangeToContinuationMap">The output dictionary of partition key ranges to continuation token.</param>
        /// <returns>The subset of partition to actually target.</returns>
        private IReadOnlyList<PartitionKeyRange> GetPartitionKeyRangesForContinuation(
            CompositeContinuationToken[] suppliedCompositeContinuationTokens,
            List<PartitionKeyRange> partitionKeyRanges,
            out Dictionary<string, CompositeContinuationToken> targetRangeToContinuationMap)
        {
            targetRangeToContinuationMap = new Dictionary<string, CompositeContinuationToken>();
            int minIndex = this.FindTargetRangeAndExtractContinuationTokens(
                partitionKeyRanges,
                suppliedCompositeContinuationTokens.Select(token => Tuple.Create(token, token.Range)),
                out targetRangeToContinuationMap);

            // We know that all partitions to the left of the continuation token are fully drained so we can filter them out
            return new PartialReadOnlyList<PartitionKeyRange>(
                partitionKeyRanges,
                minIndex,
                partitionKeyRanges.Count - minIndex);
        }

        /// <summary>
        /// Comparer used to determine if we should return the continuation token to the user
        /// </summary>
        /// <remarks>This basically just says that the two object are never equals, so that we don't return a continuation for a partition we have started draining.</remarks>
        private sealed class ParallelEqualityComparer : IEqualityComparer<CosmosElement>
        {
            /// <summary>
            /// Returns whether two parallel query items are equal.
            /// </summary>
            /// <param name="x">The first item.</param>
            /// <param name="y">The second item.</param>
            /// <returns>Whether two parallel query items are equal.</returns>
            public bool Equals(CosmosElement x, CosmosElement y)
            {
                return x == y;
            }

            /// <summary>
            /// Gets the hash code of an object.
            /// </summary>
            /// <param name="obj">The object to hash.</param>
            /// <returns>The hash code for the object.</returns>
            public int GetHashCode(CosmosElement obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
