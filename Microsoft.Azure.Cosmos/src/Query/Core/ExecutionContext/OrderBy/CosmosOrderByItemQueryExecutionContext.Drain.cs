// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.OrderBy
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal sealed partial class CosmosOrderByItemQueryExecutionContext
    {
        /// <summary>
        /// Drains a page of documents from this context.
        /// </summary>
        /// <param name="maxElements">The maximum number of elements.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that when awaited on return a page of documents.</returns>
        public override async Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //// In order to maintain the continuation token for the user we must drain with a few constraints
            //// 1) We always drain from the partition, which has the highest priority item first
            //// 2) If multiple partitions have the same priority item then we drain from the left most first
            ////   otherwise we would need to keep track of how many of each item we drained from each partition
            ////   (just like parallel queries).
            //// Visually that look the following case where we have three partitions that are numbered and store letters.
            //// For teaching purposes I have made each item a tuple of the following form:
            ////      <item stored in partition, partition number>
            //// So that duplicates across partitions are distinct, but duplicates within partitions are indistinguishable.
            ////      |-------|   |-------|   |-------|
            ////      | <a,1> |   | <a,2> |   | <a,3> |
            ////      | <a,1> |   | <b,2> |   | <c,3> |
            ////      | <a,1> |   | <b,2> |   | <c,3> |
            ////      | <d,1> |   | <c,2> |   | <c,3> |
            ////      | <d,1> |   | <e,2> |   | <f,3> |
            ////      | <e,1> |   | <h,2> |   | <j,3> |
            ////      | <f,1> |   | <i,2> |   | <k,3> |
            ////      |-------|   |-------|   |-------|
            //// Now the correct drain order in this case is:
            ////  <a,1>,<a,1>,<a,1>,<a,2>,<a,3>,<b,2>,<b,2>,<c,2>,<c,3>,<c,3>,<c,3>,
            ////  <d,1>,<d,1>,<e,1>,<e,2>,<f,1>,<f,3>,<h,2>,<i,2>,<j,3>,<k,3>
            //// In more mathematical terms
            ////  1) <x, y> always comes before <z, y> where x < z
            ////  2) <i, j> always come before <i, k> where j < k

            List<CosmosElement> results = new List<CosmosElement>();
            while (results.Count < maxElements)
            {
                // Only drain from the highest priority document producer 
                // We need to pop and push back the document producer tree, since the priority changes according to the sort order.
                ItemProducerTree currentItemProducerTree = this.PopCurrentItemProducerTree();
                try
                {
                    if (!currentItemProducerTree.HasMoreResults)
                    {
                        // This means there are no more items to drain
                        break;
                    }

                    OrderByQueryResult orderByQueryResult = new OrderByQueryResult(currentItemProducerTree.Current);

                    // Only add the payload, since other stuff is garbage from the caller's perspective.
                    results.Add(orderByQueryResult.Payload);

                    // If we are at the beginning of the page and seeing an rid from the previous page we should increment the skip count
                    // due to the fact that JOINs can make a document appear multiple times and across continuations, so we don't want to
                    // surface this more than needed. More information can be found in the continuation token docs.
                    if (this.ShouldIncrementSkipCount(currentItemProducerTree.CurrentItemProducerTree.Root))
                    {
                        ++this.skipCount;
                    }
                    else
                    {
                        this.skipCount = 0;
                    }

                    this.previousRid = orderByQueryResult.Rid;
                    this.previousOrderByItems = orderByQueryResult.OrderByItems;

                    if (!currentItemProducerTree.TryMoveNextDocumentWithinPage())
                    {
                        while (true)
                        {
                            (bool movedToNextPage, QueryResponseCore? failureResponse) = await currentItemProducerTree.TryMoveNextPageAsync(cancellationToken);
                            if (!movedToNextPage)
                            {
                                if (failureResponse.HasValue)
                                {
                                    // TODO: We can buffer this failure so that the user can still get the pages we already got.
                                    return failureResponse.Value;
                                }

                                break;
                            }

                            if (currentItemProducerTree.IsAtBeginningOfPage)
                            {
                                break;
                            }

                            if (currentItemProducerTree.TryMoveNextDocumentWithinPage())
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    this.PushCurrentItemProducerTree(currentItemProducerTree);
                }
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
        /// Gets whether or not we should increment the skip count based on the rid of the document.
        /// </summary>
        /// <param name="currentItemProducer">The current document producer.</param>
        /// <returns>Whether or not we should increment the skip count.</returns>
        private bool ShouldIncrementSkipCount(ItemProducer currentItemProducer)
        {
            // If we are not at the beginning of the page and we saw the same rid again.
            return !currentItemProducer.IsAtBeginningOfPage &&
                string.Equals(
                    this.previousRid,
                    new OrderByQueryResult(currentItemProducer.Current).Rid,
                    StringComparison.Ordinal);
        }
    }
}
