//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// ParallelDocumentQueryExecutionContext is a concrete implementation for CrossPartitionQueryExecutionContext.
    /// This class is responsible for draining cross partition queries that do not have order by conditions.
    /// The way parallel queries work is that it drains from the left most partition first.
    /// This class handles draining in the correct order and can also stop and resume the query 
    /// by generating a continuation token and resuming from said continuation token.
    /// </summary>
    internal sealed class ParallelDocumentQueryExecutionContext : CrossPartitionQueryExecutionContext
    {
        /// <summary>
        /// The comparer used to determine which document to serve next.
        /// </summary>
        private static readonly IComparer<DocumentProducerTree> MoveNextComparer = new ParllelDocumentProducerTreeComparer();

        /// <summary>
        /// The function to determine which partition to fetch from first.
        /// </summary>
        private static readonly Func<DocumentProducerTree, int> FetchPriorityFunction = documentProducerTree => int.Parse(documentProducerTree.PartitionKeyRange.Id);

        /// <summary>
        /// The comparer used to determine, which continuation tokens should be returned to the user.
        /// </summary>
        private static readonly IEqualityComparer<CosmosElement> EqualityComparer = new ParallelEqualityComparer();

        /// <summary>
        /// Initializes a new instance of the ParallelDocumentQueryExecutionContext class.
        /// </summary>
        /// <param name="constructorParams">The parameters for constructing the base class.</param>
        /// <param name="rewrittenQuery">The rewritten query.</param>
        private ParallelDocumentQueryExecutionContext(
            DocumentQueryExecutionContextBase.InitParams constructorParams,
            string rewrittenQuery)
            : base(
                constructorParams,
                rewrittenQuery,
                ParallelDocumentQueryExecutionContext.MoveNextComparer,
                ParallelDocumentQueryExecutionContext.FetchPriorityFunction,
                ParallelDocumentQueryExecutionContext.EqualityComparer)
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

                IEnumerable<DocumentProducer> activeDocumentProducers = this.GetActiveDocumentProducers();
                return activeDocumentProducers.Count() > 0 ? JsonConvert.SerializeObject(
                    activeDocumentProducers.Select((documentProducer) => new CompositeContinuationToken
                    {
                        Token = documentProducer.PreviousContinuationToken,
                        Range = documentProducer.PartitionKeyRange.ToRange()
                    }),
                    DefaultJsonSerializationSettings.Value) : null;
            }
        }

        /// <summary>
        /// Creates a ParallelDocumentQueryExecutionContext
        /// </summary>
        /// <param name="constructorParams">The params the construct the base class.</param>
        /// <param name="initParams">The params to initialize the cross partition context.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on, which in turn returns a ParallelDocumentQueryExecutionContext.</returns>
        public static async Task<ParallelDocumentQueryExecutionContext> CreateAsync(
            DocumentQueryExecutionContextBase.InitParams constructorParams,
            CrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams,
            CancellationToken token)
        {
            Debug.Assert(
                !initParams.PartitionedQueryExecutionInfo.QueryInfo.HasOrderBy,
                "Parallel~Context must not have order by query info.");

            ParallelDocumentQueryExecutionContext context = new ParallelDocumentQueryExecutionContext(
                constructorParams,
                initParams.PartitionedQueryExecutionInfo.QueryInfo.RewrittenQuery);

            await context.InitializeAsync(
                initParams.CollectionRid,
                initParams.PartitionKeyRanges,
                initParams.InitialPageSize,
                initParams.RequestContinuation,
                token);

            return context;
        }

        /// <summary>
        /// Drains documents from this execution context.
        /// </summary>
        /// <param name="maxElements">The maximum number of documents to drains.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task that when awaited on returns a DoucmentFeedResponse of results.</returns>
        public override async Task<QueryResponse> DrainAsync(int maxElements, CancellationToken token)
        {
            // In order to maintain the continuation token for the user we must drain with a few constraints
            // 1) We fully drain from the left most partition before moving on to the next partition
            // 2) We drain only full pages from the document producer so we aren't left with a partial page
            //  otherwise we would need to add to the continuation token how many items to skip over on that page.

            // Only drain from the leftmost (current) document producer tree
            DocumentProducerTree currentDocumentProducerTree = this.PopCurrentDocumentProducerTree();

            // This might be the first time we have seen this document producer tree so we need to buffer documents
            if (currentDocumentProducerTree.Current == null)
            {
                await currentDocumentProducerTree.MoveNextAsync(token);
            }

            int itemsLeftInCurrentPage = currentDocumentProducerTree.ItemsLeftInCurrentPage;

            // Only drain full pages or less if this is a top query.
            List<CosmosElement> results = new List<CosmosElement>();
            for (int i = 0; i < Math.Min(itemsLeftInCurrentPage, maxElements); i++)
            {
                results.Add(currentDocumentProducerTree.Current);
                await currentDocumentProducerTree.MoveNextAsync(token);
            }

            this.PushCurrentDocumentProducerTree(currentDocumentProducerTree);

            // At this point the document producer tree should have internally called MoveNextPage, since we fully drained a page.
            return QueryResponse.CreateSuccess(
                result: results,
                count: results.Count,
                responseLengthBytes: this.GetAndResetResponseLengthBytes(),
                responseHeaders: this.GetResponseHeaders());
        }

        /// <summary>
        /// Initialize the execution context.
        /// </summary>
        /// <param name="collectionRid">The collection rid.</param>
        /// <param name="partitionKeyRanges">The partition key ranges to drain documents from.</param>
        /// <param name="initialPageSize">The initial page size.</param>
        /// <param name="requestContinuation">The continuation token to resume from.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on.</returns>
        private Task InitializeAsync(
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
                            throw new BadRequestException($"Invalid Range in the continuation token {requestContinuation} for Parallel~Context.");
                        }
                    }

                    if (suppliedCompositeContinuationTokens.Length == 0)
                    {
                        throw new BadRequestException($"Invalid format for continuation token {requestContinuation} for Parallel~Context.");
                    }
                }
                catch (JsonException ex)
                {
                    throw new BadRequestException($"Invalid JSON in continuation token {requestContinuation} for Parallel~Context, exception: {ex.Message}");
                }

                filteredPartitionKeyRanges = this.GetPartitionKeyRangesForContinuation(suppliedCompositeContinuationTokens, partitionKeyRanges, out targetIndicesForFullContinuation);
            }

            return base.InitializeAsync(
                collectionRid: collectionRid,
                partitionKeyRanges: filteredPartitionKeyRanges,
                initialPageSize: initialPageSize,
                querySpecForInit: this.QuerySpec,
                targetRangeToContinuationMap: (targetIndicesForFullContinuation != null) ? targetIndicesForFullContinuation.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Token) : null,
                deferFirstPage: true,
                filter: null,
                filterCallback: null,
                token: token);
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
        /// For parallel queries we drain from left partition to right,
        /// then by rid order within those partitions.
        /// </summary>
        private sealed class ParllelDocumentProducerTreeComparer : IComparer<DocumentProducerTree>
        {
            /// <summary>
            /// Compares two document producer trees in a parallel context and returns their comparison.
            /// </summary>
            /// <param name="documentProducerTree1">The first document producer tree.</param>
            /// <param name="documentProducerTree2">The second document producer tree.</param>
            /// <returns>
            /// A negative number if the first comes before the second.
            /// Zero if the two document producer trees are interchangeable.
            /// A positive number if the second comes before the first.
            /// </returns>
            public int Compare(
                DocumentProducerTree documentProducerTree1,
                DocumentProducerTree documentProducerTree2)
            {
                if (object.ReferenceEquals(documentProducerTree1, documentProducerTree2))
                {
                    return 0;
                }

                if (documentProducerTree1.HasMoreResults && !documentProducerTree2.HasMoreResults)
                {
                    return -1;
                }

                if (!documentProducerTree1.HasMoreResults && documentProducerTree2.HasMoreResults)
                {
                    return 1;
                }

                // Either both don't have results or both do.
                PartitionKeyRange partitionKeyRange1 = documentProducerTree1.PartitionKeyRange;
                PartitionKeyRange partitionKeyRange2 = documentProducerTree2.PartitionKeyRange;
                return string.CompareOrdinal(
                    partitionKeyRange1.MinInclusive,
                    partitionKeyRange2.MinInclusive);
            }
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
