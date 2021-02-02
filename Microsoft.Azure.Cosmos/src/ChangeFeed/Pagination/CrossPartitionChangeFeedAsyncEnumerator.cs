// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
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
                    using (ITrace drainNotModifedPages = changeFeedMoveNextTrace.StartChild("Drain NotModified Pages", TraceComponent.ChangeFeed, TraceLevel.Info))
                    {
                        // Keep draining the cross partition enumerator until
                        // We get a non 304 page or we loop back to the same range or run into an exception
                        FeedRangeInternal originalRange = this.crossPartitionEnumerator.CurrentRange;
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
                            || this.crossPartitionEnumerator.CurrentRange.Equals(originalRange)
                            || this.bufferedException.HasValue));

                        // Create a page with the aggregated request charge
                        if (backendPage is ChangeFeedSuccessPage changeFeedSuccessPage)
                        {
                            backendPage = new ChangeFeedSuccessPage(
                                changeFeedSuccessPage.Content,
                                totalRequestCharge,
                                changeFeedSuccessPage.ActivityId,
                                changeFeedSuccessPage.State);
                        }
                        else
                        {
                            backendPage = new ChangeFeedNotModifiedPage(
                                totalRequestCharge,
                                backendPage.ActivityId,
                                backendPage.State);
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
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions,
            CrossFeedRangeState<ChangeFeedState> state,
            CancellationToken cancellationToken)
        {
            changeFeedRequestOptions ??= new ChangeFeedRequestOptions();

            if (documentContainer == null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            if (changeFeedMode == null)
            {
                throw new ArgumentNullException(nameof(changeFeedMode));
            }

            CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> crossPartitionEnumerator = new CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState>(
                documentContainer,
                CrossPartitionChangeFeedAsyncEnumerator.MakeCreateFunction(
                    documentContainer,
                    changeFeedRequestOptions.PageSizeHint.GetValueOrDefault(int.MaxValue),
                    changeFeedMode,
                    changeFeedRequestOptions?.JsonSerializationFormatOptions?.JsonSerializationFormat,
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

        private static CreatePartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> MakeCreateFunction(
            IChangeFeedDataSource changeFeedDataSource,
            int pageSize,
            ChangeFeedMode changeFeedMode,
            JsonSerializationFormat? jsonSerializationFormat,
            CancellationToken cancellationToken) => (FeedRangeInternal range, ChangeFeedState state) => new ChangeFeedPartitionRangePageAsyncEnumerator(
                changeFeedDataSource,
                range,
                pageSize,
                changeFeedMode,
                jsonSerializationFormat,
                state,
                cancellationToken);
    }
}
