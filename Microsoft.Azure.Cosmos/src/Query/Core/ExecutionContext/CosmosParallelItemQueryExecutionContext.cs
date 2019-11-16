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
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
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
        /// <param name="testSettings">Test settings.</param>
        private CosmosParallelItemQueryExecutionContext(
            CosmosQueryContext queryContext,
            int? maxConcurrency,
            int? maxItemCount,
            int? maxBufferedItemCount,
            TestInjections testSettings)
            : base(
                queryContext: queryContext,
                maxConcurrency: maxConcurrency,
                maxItemCount: maxItemCount,
                maxBufferedItemCount: maxBufferedItemCount,
                moveNextComparer: CosmosParallelItemQueryExecutionContext.MoveNextComparer,
                fetchPrioirtyFunction: CosmosParallelItemQueryExecutionContext.FetchPriorityFunction,
                equalityComparer: CosmosParallelItemQueryExecutionContext.EqualityComparer,
                testSettings: testSettings)
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
                IEnumerable<ItemProducer> activeItemProducers = this.GetActiveItemProducers();
                string continuationToken;
                if (activeItemProducers.Any())
                {
                    IEnumerable<CompositeContinuationToken> compositeContinuationTokens = activeItemProducers.Select((documentProducer) => new CompositeContinuationToken
                    {
                        Token = documentProducer.CurrentContinuationToken,
                        Range = documentProducer.PartitionKeyRange.ToRange()
                    });
                    continuationToken = JsonConvert.SerializeObject(compositeContinuationTokens, DefaultJsonSerializationSettings.Value);
                }
                else
                {
                    continuationToken = null;
                }

                return continuationToken;
            }
        }

        public static async Task<TryCatch<CosmosParallelItemQueryExecutionContext>> TryCreateAsync(
            CosmosQueryContext queryContext,
            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams,
            string requestContinuationToken,
            CancellationToken cancellationToken)
        {
            Debug.Assert(
                !initParams.PartitionedQueryExecutionInfo.QueryInfo.HasOrderBy,
                "Parallel~Context must not have order by query info.");

            cancellationToken.ThrowIfCancellationRequested();

            CosmosParallelItemQueryExecutionContext context = new CosmosParallelItemQueryExecutionContext(
                queryContext: queryContext,
                maxConcurrency: initParams.MaxConcurrency,
                maxItemCount: initParams.MaxItemCount,
                maxBufferedItemCount: initParams.MaxBufferedItemCount,
                testSettings: initParams.TestSettings);

            return await context.TryInitializeAsync(
                sqlQuerySpec: initParams.SqlQuerySpec,
                collectionRid: initParams.CollectionRid,
                partitionKeyRanges: initParams.PartitionKeyRanges,
                initialPageSize: initParams.InitialPageSize,
                requestContinuation: requestContinuationToken,
                cancellationToken: cancellationToken);
        }

        public override async Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // In order to maintain the continuation token for the user we must drain with a few constraints
            // 1) We fully drain from the left most partition before moving on to the next partition
            // 2) We drain only full pages from the document producer so we aren't left with a partial page
            //  otherwise we would need to add to the continuation token how many items to skip over on that page.

            // Only drain from the leftmost (current) document producer tree
            ItemProducerTree currentItemProducerTree = this.PopCurrentItemProducerTree();
            List<CosmosElement> results = new List<CosmosElement>();
            try
            {
                (bool gotNextPage, QueryResponseCore? failureResponse) = await currentItemProducerTree.TryMoveNextPageAsync(cancellationToken);
                if (failureResponse != null)
                {
                    return failureResponse.Value;
                }

                if (gotNextPage)
                {
                    int itemsLeftInCurrentPage = currentItemProducerTree.ItemsLeftInCurrentPage;

                    // Only drain full pages or less if this is a top query.
                    currentItemProducerTree.TryMoveNextDocumentWithinPage();
                    int numberOfItemsToDrain = Math.Min(itemsLeftInCurrentPage, maxElements);
                    for (int i = 0; i < numberOfItemsToDrain; i++)
                    {
                        results.Add(currentItemProducerTree.Current);
                        currentItemProducerTree.TryMoveNextDocumentWithinPage();
                    }
                }
            }
            finally
            {
                this.PushCurrentItemProducerTree(currentItemProducerTree);
            }

            return QueryResponseCore.CreateSuccess(
                    result: results,
                    requestCharge: this.requestChargeTracker.GetAndResetCharge(),
                    activityId: null,
                    responseLengthBytes: this.GetAndResetResponseLengthBytes(),
                    disallowContinuationTokenMessage: null,
                    continuationToken: this.ContinuationToken,
                    diagnostics: this.GetAndResetDiagnostics());
        }

        /// <summary>
        /// Initialize the execution context.
        /// </summary>
        /// <param name="sqlQuerySpec">SQL query spec.</param>
        /// <param name="collectionRid">The collection rid.</param>
        /// <param name="partitionKeyRanges">The partition key ranges to drain documents from.</param>
        /// <param name="initialPageSize">The initial page size.</param>
        /// <param name="requestContinuation">The continuation token to resume from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task to await on.</returns>
        private async Task<TryCatch<CosmosParallelItemQueryExecutionContext>> TryInitializeAsync(
            SqlQuerySpec sqlQuerySpec,
            string collectionRid,
            List<PartitionKeyRange> partitionKeyRanges,
            int initialPageSize,
            string requestContinuation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TryCatch<ParallelInitInfo> tryGetInitInfo = TryGetInitializationInfoFromContinuationToken(
                partitionKeyRanges,
                requestContinuation);
            if (!tryGetInitInfo.Succeeded)
            {
                return TryCatch<CosmosParallelItemQueryExecutionContext>.FromException(tryGetInitInfo.Exception);
            }

            ParallelInitInfo initializationInfo = tryGetInitInfo.Result;
            IReadOnlyList<PartitionKeyRange> filteredPartitionKeyRanges = initializationInfo.PartialRanges;
            IReadOnlyDictionary<string, CompositeContinuationToken> targetIndicesForFullContinuation = initializationInfo.ContinuationTokens;
            TryCatch<bool> tryInitialize = await base.TryInitializeAsync(
                collectionRid,
                filteredPartitionKeyRanges,
                initialPageSize,
                sqlQuerySpec,
                targetIndicesForFullContinuation?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Token),
                true,
                null,
                null,
                cancellationToken);
            if (!tryInitialize.Succeeded)
            {
                return TryCatch<CosmosParallelItemQueryExecutionContext>.FromException(tryInitialize.Exception);
            }

            return TryCatch<CosmosParallelItemQueryExecutionContext>.FromResult(this);
        }

        /// <summary>
        /// Given a continuation token and a list of partitionKeyRanges this function will return a list of partition key ranges you should resume with.
        /// Note that the output list is just a right hand slice of the input list, since we know that for any continuation of a parallel query it is just
        /// resuming from the partition that the query left off that.
        /// </summary>
        /// <param name="partitionKeyRanges">The partition key ranges.</param>
        /// <param name="continuationToken">The continuation tokens that the user has supplied.</param>
        /// <returns>The subset of partition to actually target and continuation tokens.</returns>
        private static TryCatch<ParallelInitInfo> TryGetInitializationInfoFromContinuationToken(
            List<PartitionKeyRange> partitionKeyRanges,
            string continuationToken)
        {
            if (continuationToken == null)
            {
                return TryCatch<ParallelInitInfo>.FromResult(
                    new ParallelInitInfo(
                        partitionKeyRanges,
                        null));
            }
            else
            {
                if (!TryParseContinuationToken(continuationToken, out CompositeContinuationToken[] tokens))
                {
                    return TryCatch<ParallelInitInfo>.FromException(
                        new Exception($"Invalid format for continuation token {continuationToken} for {nameof(CosmosParallelItemQueryExecutionContext)}"));
                }

                return CosmosCrossPartitionQueryExecutionContext.TryFindTargetRangeAndExtractContinuationTokens(
                    partitionKeyRanges,
                    tokens.Select(token => Tuple.Create(token, token.Range)))
                    .Try<ParallelInitInfo>((indexAndTokens) =>
                    {
                        int minIndex = indexAndTokens.TargetIndex;
                        IReadOnlyDictionary<string, CompositeContinuationToken> rangeToToken = indexAndTokens.ContinuationTokens;

                        // We know that all partitions to the left of the continuation token are fully drained so we can filter them out
                        IReadOnlyList<PartitionKeyRange> filteredRanges = new PartialReadOnlyList<PartitionKeyRange>(
                            partitionKeyRanges,
                            minIndex,
                            partitionKeyRanges.Count - minIndex);

                        return new ParallelInitInfo(
                            filteredRanges,
                            rangeToToken);
                    });
            }
        }

        private static bool TryParseContinuationToken(string continuationToken, out CompositeContinuationToken[] tokens)
        {
            if (continuationToken == null)
            {
                throw new ArgumentNullException(nameof(continuationToken));
            }

            try
            {
                tokens = JsonConvert.DeserializeObject<CompositeContinuationToken[]>(continuationToken, DefaultJsonSerializationSettings.Value);

                if (tokens.Length == 0)
                {
                    tokens = default;
                    return false;
                }

                foreach (CompositeContinuationToken token in tokens)
                {
                    if ((token.Range == null) || token.Range.IsEmpty)
                    {
                        tokens = default;
                        return false;
                    }
                }

                return true;
            }
            catch (JsonException)
            {
                tokens = default;
                return false;
            }
        }

        private readonly struct ParallelInitInfo
        {
            public ParallelInitInfo(IReadOnlyList<PartitionKeyRange> partialRanges, IReadOnlyDictionary<string, CompositeContinuationToken> continuationTokens)
            {
                this.PartialRanges = partialRanges;
                this.ContinuationTokens = continuationTokens;
            }

            public IReadOnlyList<PartitionKeyRange> PartialRanges { get; }

            public IReadOnlyDictionary<string, CompositeContinuationToken> ContinuationTokens { get; }
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
