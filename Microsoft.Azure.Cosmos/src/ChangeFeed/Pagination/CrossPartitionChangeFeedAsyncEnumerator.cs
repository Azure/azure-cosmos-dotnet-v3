// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class CrossPartitionChangeFeedAsyncEnumerator : IAsyncEnumerator<TryCatch<CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>>>
    {
        private readonly CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> crossPartitionEnumerator;
        private readonly CancellationToken cancellationToken;
        private TryCatch<CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>>? bufferedException;

        private CrossPartitionChangeFeedAsyncEnumerator(
            CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> crossPartitionEnumerator,
            CancellationToken cancellationToken)
        {
            this.crossPartitionEnumerator = crossPartitionEnumerator ?? throw new ArgumentNullException(nameof(crossPartitionEnumerator));
            this.cancellationToken = cancellationToken;
        }

        public TryCatch<CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>> Current { get; private set; }

        public ValueTask DisposeAsync() => this.crossPartitionEnumerator.DisposeAsync();

        public ValueTask<bool> MoveNextAsync()
        {
            return this.MoveNextAsync(NoOpTrace.Singleton);
        }

        public async ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

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

                if (!await this.crossPartitionEnumerator.MoveNextAsync(changeFeedMoveNextTrace))
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
                if (backendPage is ChangeFeedNotModifiedPage)
                {
                    // Keep draining the cross partition enumerator until
                    // We get a non 304 page or we loop back to the same range or run into an exception
                    FeedRangeInternal originalRange = this.crossPartitionEnumerator.CurrentRange;
                    // No point on draining when the state has 1 range
                    if (!IsNextRangeEqualToOriginal(this.crossPartitionEnumerator, originalRange))
                    {
                        using (ITrace drainNotModifedPages = changeFeedMoveNextTrace.StartChild("Drain NotModified Pages", TraceComponent.ChangeFeed, TraceLevel.Info))
                        {
                            double totalRequestCharge = backendPage.RequestCharge;
                            do
                            {
                                if (!await this.crossPartitionEnumerator.MoveNextAsync(drainNotModifedPages))
                                {
                                    throw new InvalidOperationException("ChangeFeed should always have a next page.");
                                }

                                monadicCrossPartitionPage = this.crossPartitionEnumerator.Current;
                                if (monadicCrossPartitionPage.Failed)
                                {
                                    // Buffer the exception, since we need to return the request charge so far.
                                    this.bufferedException = TryCatch<CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>>.FromException(monadicCrossPartitionPage.Exception);
                                }
                                else
                                {
                                    crossFeedRangePage = monadicCrossPartitionPage.Result;
                                    backendPage = crossFeedRangePage.Page;
                                    totalRequestCharge += backendPage.RequestCharge;
                                }
                            }
                            while (!(backendPage is ChangeFeedSuccessPage
                                || IsNextRangeEqualToOriginal(this.crossPartitionEnumerator, originalRange)
                                || this.bufferedException.HasValue));

                            // Create a page with the aggregated request charge
                            if (backendPage is ChangeFeedSuccessPage changeFeedSuccessPage)
                            {
                                backendPage = new ChangeFeedSuccessPage(
                                    changeFeedSuccessPage.Content,
                                    totalRequestCharge,
                                    changeFeedSuccessPage.ActivityId,
                                    changeFeedSuccessPage.AdditionalHeaders,
                                    changeFeedSuccessPage.State);
                            }
                            else
                            {
                                backendPage = new ChangeFeedNotModifiedPage(
                                    totalRequestCharge,
                                    backendPage.ActivityId,
                                    backendPage.AdditionalHeaders,
                                    backendPage.State);
                            }
                        }
                    }
                }

                crossFeedRangePage = new CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>(
                    backendPage,
                    crossFeedRangePage.State);

                this.Current = TryCatch<CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>>.FromResult(crossFeedRangePage);
                return true;
            }
        }

        public static CrossPartitionChangeFeedAsyncEnumerator Create(
            IDocumentContainer documentContainer,
            CrossFeedRangeState<ChangeFeedState> state,
            ChangeFeedPaginationOptions changeFeedPaginationOptions,
            CancellationToken cancellationToken)
        {
            changeFeedPaginationOptions ??= ChangeFeedPaginationOptions.Default;

            if (documentContainer == null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> crossPartitionEnumerator = new CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState>(
                documentContainer,
                CrossPartitionChangeFeedAsyncEnumerator.MakeCreateFunction(
                    documentContainer,
                    changeFeedPaginationOptions,
                    cancellationToken),
                comparer: default /* this uses a regular queue instead of prioirty queue */,
                maxConcurrency: default,
                cancellationToken,
                state);

            CrossPartitionChangeFeedAsyncEnumerator enumerator = new CrossPartitionChangeFeedAsyncEnumerator(
                crossPartitionEnumerator,
                cancellationToken);

            return enumerator;
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
            ChangeFeedPaginationOptions changeFeedPaginationOptions,
            CancellationToken cancellationToken) => (FeedRangeState<ChangeFeedState> feedRangeState) => new ChangeFeedPartitionRangePageAsyncEnumerator(
                changeFeedDataSource,
                feedRangeState,
                changeFeedPaginationOptions,
                cancellationToken);
    }
}
