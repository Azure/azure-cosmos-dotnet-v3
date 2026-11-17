// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class CrossPartitionChangeFeedAsyncEnumerator : ITracingAsyncEnumerator<TryCatch<CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>>>
    {
        private static readonly byte[] EmptyFeedContent = Encoding.UTF8.GetBytes("{\"Documents\":[],\"_count\":0}");

        private readonly CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> crossPartitionEnumerator;
        private readonly bool enableContinuationTokenOnly;
        private TryCatch<CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>>? bufferedException;

        private CrossPartitionChangeFeedAsyncEnumerator(
            CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> crossPartitionEnumerator,
            bool enableContinuationTokenOnly)
        {
            this.crossPartitionEnumerator = crossPartitionEnumerator ?? throw new ArgumentNullException(nameof(crossPartitionEnumerator));
            this.enableContinuationTokenOnly = enableContinuationTokenOnly;
        }

        public TryCatch<CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>> Current { get; private set; }

        public bool IsCycleComplete { get; private set; }

        public ValueTask DisposeAsync() => this.crossPartitionEnumerator.DisposeAsync();

        public async ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken)
        {
            this.IsCycleComplete = false;
            cancellationToken.ThrowIfCancellationRequested();

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            using (ITrace changeFeedMoveNextTrace = trace.StartChild("ChangeFeed MoveNextAsync", TraceComponent.ChangeFeed, TraceLevel.Info))
            {
                if (this.bufferedException.HasValue)
                {
                    this.Current = this.bufferedException.Value;
                    this.bufferedException = null;
                    return true;
                }

                if (!await this.crossPartitionEnumerator.MoveNextAsync(changeFeedMoveNextTrace, cancellationToken))
                {
                    throw new InvalidOperationException("ChangeFeed should always have a next page.");
                }

                TryCatch<CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>> monadicCrossPartitionPage = this.crossPartitionEnumerator.Current;
                if (monadicCrossPartitionPage.Failed)
                {
                    this.Current = TryCatch<CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>>.FromException(monadicCrossPartitionPage.Exception);
                    return true;
                }

                CrossFeedRangePage<ChangeFeedPage, ChangeFeedState> crossFeedRangePage = monadicCrossPartitionPage.Result;
                ChangeFeedPage backendPage = crossFeedRangePage.Page;
                bool pageReturned = false;
                try
                {
                    if (this.enableContinuationTokenOnly)
                    {
                        backendPage = SuppressDocuments(backendPage);
                    }

                    if (this.enableContinuationTokenOnly || backendPage is ChangeFeedNotModifiedPage)
                    {
                        FeedRangeInternal originalRange = this.crossPartitionEnumerator.CurrentRange;
                        HashSet<FeedRangeInternal> visitedRanges = this.enableContinuationTokenOnly
                            ? new HashSet<FeedRangeInternal>() { originalRange }
                            : null;
                        this.IsCycleComplete = this.enableContinuationTokenOnly
                            && HasVisitedAllRanges(crossFeedRangePage.State, visitedRanges);
                        if (!(this.enableContinuationTokenOnly
                            ? this.IsCycleComplete
                            : IsNextRangeEqualToOriginal(this.crossPartitionEnumerator, originalRange)))
                        {
                            using (ITrace drainPages = changeFeedMoveNextTrace.StartChild(
                                this.enableContinuationTokenOnly ? "Drain TokenOnly Pages" : "Drain NotModified Pages",
                                TraceComponent.ChangeFeed,
                                TraceLevel.Info))
                            {
                                double totalRequestCharge = backendPage.RequestCharge;
                                do
                                {
                                    if (!await this.crossPartitionEnumerator.MoveNextAsync(drainPages, cancellationToken))
                                    {
                                        throw new InvalidOperationException("ChangeFeed should always have a next page.");
                                    }

                                    monadicCrossPartitionPage = this.crossPartitionEnumerator.Current;
                                    if (monadicCrossPartitionPage.Failed)
                                    {
                                        // Return the request charge so far before surfacing the error.
                                        this.bufferedException = TryCatch<CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>>.FromException(monadicCrossPartitionPage.Exception);
                                    }
                                    else
                                    {
                                        if (backendPage is ChangeFeedSuccessPage supersededPage)
                                        {
                                            supersededPage.Content.Dispose();
                                        }

                                        crossFeedRangePage = monadicCrossPartitionPage.Result;
                                        backendPage = crossFeedRangePage.Page;
                                        totalRequestCharge += backendPage.RequestCharge;
                                        if (this.enableContinuationTokenOnly)
                                        {
                                            backendPage = SuppressDocuments(backendPage);
                                            visitedRanges.Add(this.crossPartitionEnumerator.CurrentRange);
                                            this.IsCycleComplete = HasVisitedAllRanges(crossFeedRangePage.State, visitedRanges);
                                        }
                                    }
                                }
                                while (!this.bufferedException.HasValue && !(this.enableContinuationTokenOnly
                                    ? this.IsCycleComplete
                                    : backendPage is ChangeFeedSuccessPage
                                        || IsNextRangeEqualToOriginal(this.crossPartitionEnumerator, originalRange)));

                                backendPage = backendPage is ChangeFeedSuccessPage successPage
                                    ? new ChangeFeedSuccessPage(
                                        successPage.Content,
                                        totalRequestCharge,
                                        successPage.ItemCount,
                                        successPage.ActivityId,
                                        successPage.AdditionalHeaders,
                                        successPage.State)
                                    : new ChangeFeedNotModifiedPage(
                                        totalRequestCharge,
                                        backendPage.ActivityId,
                                        backendPage.AdditionalHeaders,
                                        backendPage.State);
                            }
                        }
                    }

                    crossFeedRangePage = new CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>(
                        backendPage,
                        crossFeedRangePage.State);

                    this.Current = TryCatch<CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>>.FromResult(crossFeedRangePage);
                    pageReturned = true;
                    return true;
                }
                finally
                {
                    if (!pageReturned && backendPage is ChangeFeedSuccessPage discardedPage)
                    {
                        discardedPage.Content.Dispose();
                    }
                }
            }
        }

        public static CrossPartitionChangeFeedAsyncEnumerator Create(
            IDocumentContainer documentContainer,
            CrossFeedRangeState<ChangeFeedState> state,
            ChangeFeedExecutionOptions changeFeedPaginationOptions)
        {
            changeFeedPaginationOptions ??= ChangeFeedExecutionOptions.Default;

            if (documentContainer == null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> crossPartitionEnumerator = new CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState>(
                documentContainer,
                CrossPartitionChangeFeedAsyncEnumerator.MakeCreateFunction(
                    documentContainer,
                    changeFeedPaginationOptions),
                comparer: default /* this uses a regular queue instead of priority queue */,
                maxConcurrency: default,
                prefetchPolicy: PrefetchPolicy.PrefetchSinglePage,
                state: state);

            CrossPartitionChangeFeedAsyncEnumerator enumerator = new CrossPartitionChangeFeedAsyncEnumerator(
                crossPartitionEnumerator,
                changeFeedPaginationOptions.EnableContinuationTokenOnly);

            return enumerator;
        }

        private static ChangeFeedPage SuppressDocuments(ChangeFeedPage page)
        {
            if (page is ChangeFeedSuccessPage successPage)
            {
                successPage.Content.Dispose();
                return new ChangeFeedSuccessPage(
                    new MemoryStream(EmptyFeedContent, writable: false),
                    successPage.RequestCharge,
                    itemCount: 0,
                    successPage.ActivityId,
                    successPage.AdditionalHeaders,
                    successPage.State);
            }

            return page;
        }

        private static bool HasVisitedAllRanges(
            CrossFeedRangeState<ChangeFeedState> state,
            HashSet<FeedRangeInternal> visitedRanges)
        {
            // A split may enqueue unvisited children behind an already visited range.
            foreach (FeedRangeState<ChangeFeedState> rangeState in state.Value.Span)
            {
                if (!visitedRanges.Contains(rangeState.FeedRange))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsNextRangeEqualToOriginal(
            CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> crossPartitionEnumerator,
            FeedRangeInternal originalRange)
        {
            return crossPartitionEnumerator.TryPeekNext(out FeedRangeState<ChangeFeedState> nextState)
                                        && originalRange.Equals(nextState.FeedRange);
        }

        private static CreatePartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> MakeCreateFunction(
            IChangeFeedDataSource changeFeedDataSource,
            ChangeFeedExecutionOptions changeFeedPaginationOptions) => (FeedRangeState<ChangeFeedState> feedRangeState) => new ChangeFeedPartitionRangePageAsyncEnumerator(
                changeFeedDataSource,
                feedRangeState,
                changeFeedPaginationOptions);
    }
}
