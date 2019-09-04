//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Routing;

    /// <summary>
    /// This class is responsible for fetching documents from a partition and all it's descendants, which is modeled as a tree of document producers.
    /// The root node is responsible for buffering documents from the root partition and the children recursively buffer documents for their corresponding partitions.
    /// The tree itself allows a user to iterate through it's documents using a comparator and Current / Move Next Async functions.
    /// Note that if a user wants to determine the current document it will take the max of it's buffered documents and the recursive max of it's children.
    /// Also note that if there are no buffered documents for any node in the recursive evaluation, then those nodes will go for a fetch.
    /// Finally note that due to the tree structure of this class it is inherently split proof.
    /// If any leaf node in the tree encounters a split exception it will spawn child document producer trees (any many as needed, so multiple splits is handled) and continue on as if the split never happened.
    /// This code does not handle merges, but we will cross that bridge when we have to (I am currently thinking about a linked list where the nodes represent document producers and you can merge adjacent nodes).
    /// As a implementation detail the documents are buffered and logically enumerated as a nested loop. The following is the pseudo code:
    /// for partition in document_producer_tree:
    ///     for page in partition:
    ///         for document in page:
    ///             yield document.
    /// And the way this is done is by buffering pages and updating the state of the ItemProducerTree whenever a user crosses a page boundary.
    /// </summary>
    internal sealed class ItemProducerTree : IEnumerable<ItemProducerTree>
    {
        /// <summary>
        /// The child partitions of this node in the tree that are added after a split.
        /// </summary>
        private readonly PriorityQueue<ItemProducerTree> children;

        /// <summary>
        /// Callback to create child document producer trees once a split happens.
        /// </summary>
        private readonly Func<Documents.PartitionKeyRange, string, ItemProducerTree> createItemProducerTreeCallback;

        /// <summary>
        /// Whether or not to defer fetching the first page from all the partitions.
        /// </summary>
        private readonly bool deferFirstPage;

        /// <summary>
        /// The collection rid to drain from. 
        /// </summary>
        private readonly string collectionRid;

        /// <summary>
        /// Semaphore to ensure mutual exclusion during fetching from a tree.
        /// This is to ensure that there is no race conditions during splits.
        /// </summary>
        private readonly SemaphoreSlim executeWithSplitProofingSemaphore;

        private readonly CosmosQueryClient queryClient;

        /// <summary>
        /// Initializes a new instance of the ItemProducerTree class.
        /// </summary>
        /// <param name="queryContext">query context.</param>
        /// <param name="querySpecForInit">query spec init.</param>
        /// <param name="partitionKeyRange">The partition key range.</param>
        /// <param name="produceAsyncCompleteCallback">Callback to invoke once a fetch finishes.</param>
        /// <param name="itemProducerTreeComparer">Comparer to determine, which tree to produce from.</param>
        /// <param name="equalityComparer">Comparer to see if we need to return the continuation token for a partition.</param>
        /// <param name="deferFirstPage">Whether or not to defer fetching the first page.</param>
        /// <param name="collectionRid">The collection to drain from.</param>
        /// <param name="initialPageSize">The initial page size.</param>
        /// <param name="initialContinuationToken">The initial continuation token.</param>
        public ItemProducerTree(
            CosmosQueryContext queryContext,
            SqlQuerySpec querySpecForInit,
            Documents.PartitionKeyRange partitionKeyRange,
            ProduceAsyncCompleteDelegate produceAsyncCompleteCallback,
            IComparer<ItemProducerTree> itemProducerTreeComparer,
            IEqualityComparer<CosmosElement> equalityComparer,
            bool deferFirstPage,
            string collectionRid,
            long initialPageSize = 50,
            string initialContinuationToken = null)
        {
            if (queryContext == null)
            {
                throw new ArgumentNullException($"{nameof(queryContext)}");
            }

            if (itemProducerTreeComparer == null)
            {
                throw new ArgumentNullException($"{nameof(itemProducerTreeComparer)}");
            }

            if (produceAsyncCompleteCallback == null)
            {
                throw new ArgumentNullException($"{nameof(produceAsyncCompleteCallback)}");
            }

            if (itemProducerTreeComparer == null)
            {
                throw new ArgumentNullException($"{nameof(itemProducerTreeComparer)}");
            }

            if (equalityComparer == null)
            {
                throw new ArgumentNullException($"{nameof(equalityComparer)}");
            }

            if (string.IsNullOrEmpty(collectionRid))
            {
                throw new ArgumentException($"{nameof(collectionRid)} can not be null or empty.");
            }

            this.Root = new ItemProducer(
                queryContext,
                querySpecForInit,
                partitionKeyRange,
                (itemsBuffered, resourceUnitUsage, queryMetrics, requestLength, token) => produceAsyncCompleteCallback(this, itemsBuffered, resourceUnitUsage, queryMetrics, requestLength, token),
                equalityComparer,
                initialPageSize,
                initialContinuationToken);

            this.queryClient = queryContext.QueryClient;
            this.children = new PriorityQueue<ItemProducerTree>(itemProducerTreeComparer, true);
            this.deferFirstPage = deferFirstPage;
            this.collectionRid = collectionRid;
            this.createItemProducerTreeCallback = ItemProducerTree.CreateItemProducerTreeCallback(
                queryContext,
                querySpecForInit,
                produceAsyncCompleteCallback,
                itemProducerTreeComparer,
                equalityComparer,
                deferFirstPage,
                collectionRid,
                initialPageSize);
            this.executeWithSplitProofingSemaphore = new SemaphoreSlim(1, 1);
        }

        public delegate void ProduceAsyncCompleteDelegate(
            ItemProducerTree itemProducerTree,
            int numberOfDocuments,
            double requestCharge,
            QueryMetrics queryMetrics,
            long responseLengthInBytes,
            CancellationToken token);

        /// <summary>
        /// Gets the root document from the tree.
        /// </summary>
        public ItemProducer Root { get; }

        /// <summary>
        /// Gets the partition key range from the current document producer tree.
        /// </summary>
        public Documents.PartitionKeyRange PartitionKeyRange
        {
            get
            {
                if (this.CurrentItemProducerTree == this)
                {
                    return this.Root.PartitionKeyRange;
                }
                else
                {
                    return this.CurrentItemProducerTree.PartitionKeyRange;
                }
            }
        }

        /// <summary>
        /// Gets or sets the filter for the current document producer tree.
        /// </summary>
        public string Filter
        {
            get
            {
                if (this.CurrentItemProducerTree == this)
                {
                    return this.Root.Filter;
                }
                else
                {
                    return this.CurrentItemProducerTree.Filter;
                }
            }

            set
            {
                if (this.CurrentItemProducerTree == this)
                {
                    this.Root.Filter = value;
                }
                else
                {
                    this.CurrentItemProducerTree.Filter = value;
                }
            }
        }

        /// <summary>
        /// Gets the current (highest priority) document producer tree from all subtrees.
        /// </summary>
        public ItemProducerTree CurrentItemProducerTree
        {
            get
            {
                if (this.HasSplit && !this.Root.HasMoreResults)
                {
                    // If the partition has split and there are no more results in the parent buffer
                    // then just pull from the highest priority child (with recursive decent).

                    return this.children.Peek().CurrentItemProducerTree;
                }
                else
                {
                    // The partition has not split or there are still documents buffered,
                    // so keep trying to read from it.
                    return this;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the document producer tree is at the beginning of the page for the current document producer.
        /// </summary>
        public bool IsAtBeginningOfPage
        {
            get
            {
                if (this.CurrentItemProducerTree == this)
                {
                    return this.Root.IsAtBeginningOfPage;
                }
                else
                {
                    return this.CurrentItemProducerTree.IsAtBeginningOfPage;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the document producer tree has more results.
        /// </summary>
        public bool HasMoreResults => this.Root.HasMoreResults
                    || (this.HasSplit && this.children.Peek().HasMoreResults);

        /// <summary>
        /// Gets a value indicating whether the document producer tree has more backend results.
        /// </summary>
        public bool HasMoreBackendResults => this.Root.HasMoreBackendResults
                    || (this.HasSplit && this.children.Peek().HasMoreBackendResults);

        /// <summary>
        /// Gets whether there are items left in the current page of the document producer tree.
        /// </summary>
        public int ItemsLeftInCurrentPage
        {
            get
            {
                if (this.CurrentItemProducerTree == this)
                {
                    return this.Root.ItemsLeftInCurrentPage;
                }
                else
                {
                    return this.CurrentItemProducerTree.ItemsLeftInCurrentPage;
                }
            }
        }

        /// <summary>
        /// Gets the buffered item count in the current document producer tree.
        /// </summary>
        public int BufferedItemCount
        {
            get
            {
                if (this.CurrentItemProducerTree == this)
                {
                    return this.Root.BufferedItemCount;
                }
                else
                {
                    return this.CurrentItemProducerTree.BufferedItemCount;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the document producer tree is active.
        /// </summary>
        public bool IsActive => this.Root.IsActive || this.children.Any((child) => child.IsActive);

        /// <summary>
        /// Gets or sets the page size for this document producer tree.
        /// </summary>
        public long PageSize
        {
            get
            {
                if (this.CurrentItemProducerTree == this)
                {
                    return this.Root.PageSize;
                }
                else
                {
                    return this.CurrentItemProducerTree.PageSize;
                }
            }

            set
            {
                if (this.CurrentItemProducerTree == this)
                {
                    this.Root.PageSize = value;
                }
                else
                {
                    this.CurrentItemProducerTree.PageSize = value;
                }
            }
        }

        /// <summary>
        /// Gets the activity id from the current document producer tree.
        /// </summary>
        public Guid ActivityId
        {
            get
            {
                if (this.CurrentItemProducerTree == this)
                {
                    return this.Root.ActivityId;
                }
                else
                {
                    return this.CurrentItemProducerTree.ActivityId;
                }
            }
        }

        /// <summary>
        /// Gets the current item from the document producer tree.
        /// </summary>
        public CosmosElement Current
        {
            get
            {
                if (this.CurrentItemProducerTree == this)
                {
                    return this.Root.Current;
                }
                else
                {
                    return this.CurrentItemProducerTree.Current;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the document producer tree has split.
        /// </summary>
        private bool HasSplit => this.children.Count != 0;

        /// <summary>
        /// Moves to the next item in the document producer tree.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on that returns whether we successfully moved next.</returns>
        /// <remarks>This function is split proofed.</remarks>
        public async Task<(bool successfullyMovedNext, QueryResponseCore? failureResponse)> MoveNextAsync(CancellationToken token)
        {
            return await this.ExecuteWithSplitProofingAsync(
                function: this.TryMoveNextAsyncImplementationAsync,
                functionNeedsBeReexecuted: false,
                cancellationToken: token);
        }

        /// <summary>
        /// Moves next only if the producer has not split.
        /// This is used to avoid calling move next twice during splits.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on which in turn returns whether or not we moved next.</returns>
        public async Task<(bool successfullyMovedNext, QueryResponseCore? failureResponse)> MoveNextIfNotSplitAsync(CancellationToken token)
        {
            return await this.ExecuteWithSplitProofingAsync(
                function: this.TryMoveNextIfNotSplitAsyncImplementationAsync,
                functionNeedsBeReexecuted: false,
                cancellationToken: token);
        }

        /// <summary>
        /// Buffers more documents in a split proof manner.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on.</returns>
        public Task<(bool successfullyMovedNext, QueryResponseCore? failureResponse)> BufferMoreDocumentsAsync(CancellationToken token)
        {
            return this.ExecuteWithSplitProofingAsync(
                function: this.BufferMoreDocumentsImplementationAsync,
                functionNeedsBeReexecuted: true,
                cancellationToken: token);
        }

        /// <summary>
        /// Gets the document producers that need their continuation token return to the user.
        /// </summary>
        /// <returns>The document producers that need their continuation token return to the user.</returns>
        public IEnumerable<ItemProducer> GetActiveItemProducers()
        {
            if (!this.HasSplit)
            {
                if (this.Root.IsActive)
                {
                    yield return this.Root;
                }
            }
            else
            {
                // A document producer is "active" if it resumed from a continuation token and has more buffered results.
                if (this.Root.IsActive && this.Root.BufferedItemCount != 0)
                {
                    // has split but need to check if parent is fully drained
                    yield return this.Root;
                }
                else
                {
                    foreach (ItemProducerTree child in this.children)
                    {
                        foreach (ItemProducer activeItemProducer in child.GetActiveItemProducers())
                        {
                            yield return activeItemProducer;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the enumerator for all the leaf level document producers.
        /// </summary>
        /// <returns>The enumerator for all the leaf level document producers.</returns>
        public IEnumerator<ItemProducerTree> GetEnumerator()
        {
            if (this.children.Count == 0)
            {
                yield return this;
            }

            foreach (ItemProducerTree child in this.children)
            {
                foreach (ItemProducerTree itemProducer in child)
                {
                    yield return itemProducer;
                }
            }
        }

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>
        /// Callback to create a child document producer tree based on the partition key range.
        /// </summary>
        /// <param name="queryContext">request context</param>
        /// <param name="querySpecForInit">query spec for initialization</param>
        /// <param name="produceAsyncCompleteCallback">Callback to invoke once a fetch finishes.</param>
        /// <param name="itemProducerTreeComparer">Comparer to determine, which tree to produce from.</param>
        /// <param name="equalityComparer">Comparer to see if we need to return the continuation token for a partition.</param>
        /// <param name="deferFirstPage">Whether or not to defer fetching the first page.</param>
        /// <param name="collectionRid">The collection to drain from.</param>
        /// <param name="initialPageSize">The initial page size.</param>
        /// <returns>A function that given a partition key range and continuation token will create a document producer.</returns>
        private static Func<Documents.PartitionKeyRange, string, ItemProducerTree> CreateItemProducerTreeCallback(
            CosmosQueryContext queryContext,
            SqlQuerySpec querySpecForInit,
            ProduceAsyncCompleteDelegate produceAsyncCompleteCallback,
            IComparer<ItemProducerTree> itemProducerTreeComparer,
            IEqualityComparer<CosmosElement> equalityComparer,
            bool deferFirstPage,
            string collectionRid,
            long initialPageSize = 50)
        {
            return (partitionKeyRange, continuationToken) =>
            {
                return new ItemProducerTree(
                    queryContext,
                    querySpecForInit,
                    partitionKeyRange,
                    produceAsyncCompleteCallback,
                    itemProducerTreeComparer,
                    equalityComparer,
                    deferFirstPage,
                    collectionRid,
                    initialPageSize,
                    continuationToken);
            };
        }

        /// <summary>
        /// Given a document client exception this function determines whether it was caused due to a split.
        /// </summary>
        /// <param name="ex">The document client exception</param>
        /// <returns>Whether or not the exception was due to a split.</returns>
        private static bool IsSplitException(QueryResponseCore ex)
        {
            return ex.StatusCode == HttpStatusCode.Gone && ex.SubStatusCode == Documents.SubStatusCodes.PartitionKeyRangeGone;
        }

        /// <summary>
        /// Implementation for moving to the next item in the document producer tree.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task with whether or not move next succeeded.</returns>
        private async Task<(bool successfullyMovedNext, QueryResponseCore? failureResponse)> TryMoveNextAsyncImplementationAsync(CancellationToken token)
        {
            if (!this.HasMoreResults)
            {
                return ItemProducer.IsDoneResponse;
            }

            if (this.CurrentItemProducerTree == this)
            {
                return await this.Root.MoveNextAsync(token);
            }
            else
            {
                // Keep track of the current tree
                ItemProducerTree itemProducerTree = this.CurrentItemProducerTree;
                (bool successfullyMovedNext, QueryResponseCore? failureResponse) response = await itemProducerTree.MoveNextAsync(token);

                // Update the priority queue for the new values
                this.children.Enqueue(this.children.Dequeue());

                // If the current tree is done, but other trees still have a result
                // then return true.
                if (!response.successfullyMovedNext &&
                    response.failureResponse == null &&
                    this.HasMoreResults)
                {
                    return ItemProducer.IsSuccessResponse;
                }

                return response;
            }
        }

        /// <summary>
        /// Implementation for moving next if the tree has not split.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on which in turn return whether we successfully moved next.</returns>
        private async Task<(bool successfullyMovedNext, QueryResponseCore? failureResponse)> TryMoveNextIfNotSplitAsyncImplementationAsync(CancellationToken token)
        {
            if (this.HasSplit)
            {
                return ItemProducer.IsDoneResponse;
            }

            return await this.TryMoveNextAsyncImplementationAsync(token);
        }

        /// <summary>
        /// Implementation for buffering more documents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on.</returns>
        private async Task<(bool successfullyMovedNext, QueryResponseCore? failureResponse)> BufferMoreDocumentsImplementationAsync(CancellationToken token)
        {
            if (this.CurrentItemProducerTree == this)
            {
                if (!this.HasMoreBackendResults || this.HasSplit)
                {
                    // Just no-op, since this method might be called by the scheduler, which doesn't know of the reconfiguration yet.
                    return ItemProducer.IsSuccessResponse;
                }

                await this.Root.BufferMoreDocumentsAsync(token);
            }
            else
            {
                await this.CurrentItemProducerTree.BufferMoreDocumentsAsync(token);
            }

            return ItemProducer.IsSuccessResponse;
        }

        /// <summary>
        /// This function will execute any function in a split proof manner.
        /// What it does is it will try to execute the supplied function and catch any gone exceptions do to a split.
        /// If a split happens when this function will 
        /// </summary>
        /// <param name="function">The function to execute in a split proof manner.</param>
        /// <param name="functionNeedsBeReexecuted">If the function needs to be re-executed after split.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <remarks>
        /// <para>
        /// This function is thread safe meaning that if multiple functions want to execute in a split proof manner,
        /// then they will need to go one after another.
        /// This is required since you could have the follow scenario:
        /// Time    | CurrentItemProducer   | Thread 1      | Thread2
        /// 0       | 0                         | MoveNextAsync | BufferMore
        /// 1       | 0                         | Split         | Split
        /// </para>
        /// <para>
        /// Therefore thread 1 and thread 2 both think that document producer 0 got split and they both try to repair the execution context,
        /// which is a race condition.
        /// Note that this thread safety / serial behavior is only scoped to a single document producer tree
        /// meaning this should not have a performance hit on the scheduler that is prefetching from other partitions.
        /// </para>
        /// </remarks>
        /// <returns>The result of the function would have returned as if there were no splits.</returns>
        private async Task<(bool successfullyMovedNext, QueryResponseCore? failureResponse)> ExecuteWithSplitProofingAsync(
            Func<CancellationToken, Task<(bool successfullyMovedNext, QueryResponseCore? failureResponse)>> function,
            bool functionNeedsBeReexecuted,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            while (true)
            {
                try
                {
                    await this.executeWithSplitProofingSemaphore.WaitAsync();
                    (bool successfullyMovedNext, QueryResponseCore? failureResponse) response = await function(cancellationToken);
                    if (response.failureResponse == null || !ItemProducerTree.IsSplitException(response.failureResponse.Value))
                    {
                        return response;
                    }

                    // Split just happened
                    ItemProducerTree splitItemProducerTree = this.CurrentItemProducerTree;

                    if (!functionNeedsBeReexecuted)
                    {
                        // If we run into a split for MoveNextAsync then that means we failed to buffer more meaning the document producer has no more results
                        splitItemProducerTree.Root.Shutdown();
                    }

                    // Repair the execution context: Get the replacement document producers and add them to the tree.
                    IReadOnlyList<Documents.PartitionKeyRange> replacementRanges = await this.GetReplacementRangesAsync(splitItemProducerTree.PartitionKeyRange, this.collectionRid);
                    foreach (Documents.PartitionKeyRange replacementRange in replacementRanges)
                    {
                        ItemProducerTree replacementItemProducerTree = this.createItemProducerTreeCallback(replacementRange, splitItemProducerTree.Root.BackendContinuationToken);

                        if (!this.deferFirstPage)
                        {
                            await replacementItemProducerTree.MoveNextAsync(cancellationToken);
                        }

                        replacementItemProducerTree.Filter = splitItemProducerTree.Root.Filter;
                        if (replacementItemProducerTree.HasMoreResults)
                        {
                            if (!splitItemProducerTree.children.TryAdd(replacementItemProducerTree))
                            {
                                throw new InvalidOperationException("Unable to add child document producer tree");
                            }
                        }
                    }

                    if (!functionNeedsBeReexecuted)
                    {
                        // We don't want to call move next async again, since we already did when creating the document producers
                        return ItemProducer.IsSuccessResponse;
                    }
                }
                finally
                {
                    this.executeWithSplitProofingSemaphore.Release();
                }
            }
        }

        /// <summary>
        /// Gets the replacement ranges for the target range that got split.
        /// </summary>
        /// <param name="targetRange">The target range that got split.</param>
        /// <param name="collectionRid">The collection rid.</param>
        /// <returns>The replacement ranges for the target range that got split.</returns>
        private async Task<IReadOnlyList<Documents.PartitionKeyRange>> GetReplacementRangesAsync(Documents.PartitionKeyRange targetRange, string collectionRid)
        {
            IReadOnlyList<Documents.PartitionKeyRange> replacementRanges = await this.queryClient.TryGetOverlappingRangesAsync(
                collectionRid,
                targetRange.ToRange(),
                true);

            string replaceMinInclusive = replacementRanges.First().MinInclusive;
            string replaceMaxExclusive = replacementRanges.Last().MaxExclusive;
            if (!replaceMinInclusive.Equals(targetRange.MinInclusive, StringComparison.Ordinal) || !replaceMaxExclusive.Equals(targetRange.MaxExclusive, StringComparison.Ordinal))
            {
                throw new Documents.InternalServerErrorException(string.Format(
                    CultureInfo.InvariantCulture,
                    "Target range and Replacement range has mismatched min/max. Target range: [{0}, {1}). Replacement range: [{2}, {3}).",
                    targetRange.MinInclusive,
                    targetRange.MaxExclusive,
                    replaceMinInclusive,
                    replaceMaxExclusive));
            }

            return replacementRanges;
        }
    }
}