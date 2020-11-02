// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

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

        public async ValueTask<bool> MoveNextAsync()
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            if (this.bufferedException.HasValue)
            {
                this.Current = this.bufferedException.Value;
                this.bufferedException = null;
                return true;
            }

            if (!await this.crossPartitionEnumerator.MoveNextAsync())
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
                double totalRequestCharge = backendPage.RequestCharge;
                do
                {
                    if (!await this.crossPartitionEnumerator.MoveNextAsync())
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

            crossFeedRangePage = new CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>(
                backendPage,
                crossFeedRangePage.State);

            this.Current = TryCatch<CrossFeedRangePage<ChangeFeedPage, ChangeFeedState>>.FromResult(crossFeedRangePage);
            return true;
        }

        public static CrossPartitionChangeFeedAsyncEnumerator Create(
            IDocumentContainer documentContainer,
            ChangeFeedRequestOptions changeFeedRequestOptions,
            CrossFeedRangeState<ChangeFeedState> state,
            CancellationToken cancellationToken)
        {
            changeFeedRequestOptions ??= new ChangeFeedRequestOptions();

            if (documentContainer == null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> crossPartitionEnumerator = new CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState>(
                documentContainer,
                CrossPartitionChangeFeedAsyncEnumerator.MakeCreateFunction(
                    documentContainer,
                    changeFeedRequestOptions.PageSizeHint.GetValueOrDefault(int.MaxValue),
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
            CancellationToken cancellationToken) => (FeedRangeInternal range, ChangeFeedState state) => new ChangeFeedPartitionRangePageAsyncEnumerator(
                changeFeedDataSource,
                range,
                pageSize,
                state,
                cancellationToken);

        private sealed class CrossPartitionStateExtractor : ChangeFeedStartFromVisitor<TryCatch<CrossFeedRangeState<ChangeFeedState>>>
        {
            public static readonly CrossPartitionStateExtractor Singleton = new CrossPartitionStateExtractor();

            private CrossPartitionStateExtractor()
            {
            }

            public override TryCatch<CrossFeedRangeState<ChangeFeedState>> Visit(ChangeFeedStartFromNow startFromNow)
            {
                ChangeFeedState state = ChangeFeedState.Now();
                ImmutableArray<FeedRangeState<ChangeFeedState>> feedRangeStates = new List<FeedRangeState<ChangeFeedState>>()
                {
                    new FeedRangeState<ChangeFeedState>(startFromNow.FeedRange, state)
                }.ToImmutableArray();

                CrossFeedRangeState<ChangeFeedState> feedRangeState = new CrossFeedRangeState<ChangeFeedState>(feedRangeStates);
                return TryCatch<CrossFeedRangeState<ChangeFeedState>>.FromResult(feedRangeState);
            }

            public override TryCatch<CrossFeedRangeState<ChangeFeedState>> Visit(ChangeFeedStartFromTime startFromTime)
            {
                ChangeFeedState state = ChangeFeedState.Time(startFromTime.StartTime);
                ImmutableArray<FeedRangeState<ChangeFeedState>> feedRangeAndStates = new List<FeedRangeState<ChangeFeedState>>()
                {
                    new FeedRangeState<ChangeFeedState>(startFromTime.FeedRange, state)
                }.ToImmutableArray();

                CrossFeedRangeState<ChangeFeedState> feedRangeState = new CrossFeedRangeState<ChangeFeedState>(feedRangeAndStates);
                return TryCatch<CrossFeedRangeState<ChangeFeedState>>.FromResult(feedRangeState);
            }

            public override TryCatch<CrossFeedRangeState<ChangeFeedState>> Visit(ChangeFeedStartFromContinuation startFromContinuation)
            {
                string continuationToken = startFromContinuation.Continuation;
                TryCatch<CosmosArray> monadicCosmosArray = CosmosArray.Monadic.Parse(continuationToken);
                if (monadicCosmosArray.Failed)
                {
                    return TryCatch<CrossFeedRangeState<ChangeFeedState>>.FromException(
                        new MalformedChangeFeedContinuationTokenException(
                            message: $"Array expected for change feed continuation token: {continuationToken}.",
                            innerException: monadicCosmosArray.Exception));
                }

                CosmosArray cosmosArray = monadicCosmosArray.Result;
                if (cosmosArray.Count == 0)
                {
                    return TryCatch<CrossFeedRangeState<ChangeFeedState>>.FromException(
                        new MalformedChangeFeedContinuationTokenException(
                            message: $"non empty array expected for change feed continuation token: {continuationToken}."));
                }

                List<FeedRangeState<ChangeFeedState>> feedRangeStates = new List<FeedRangeState<ChangeFeedState>>();
                foreach (CosmosElement arrayItem in cosmosArray)
                {
                    TryCatch<FeedRangeState<ChangeFeedState>> monadicChangeFeedContinuationToken = ChangeFeedFeedRangeStateSerializer.Monadic.CreateFromCosmosElement(arrayItem);
                    if (monadicChangeFeedContinuationToken.Failed)
                    {
                        return TryCatch<CrossFeedRangeState<ChangeFeedState>>.FromException(
                            new MalformedChangeFeedContinuationTokenException(
                                message: $"Failed to parse change feed continuation token: {continuationToken}.",
                                innerException: monadicChangeFeedContinuationToken.Exception));
                    }

                    FeedRangeState<ChangeFeedState> feedRangeState = monadicChangeFeedContinuationToken.Result;
                    feedRangeStates.Add(feedRangeState);
                }

                CrossFeedRangeState<ChangeFeedState> crossPartitionState = new CrossFeedRangeState<ChangeFeedState>(feedRangeStates.ToImmutableArray());
                return TryCatch<CrossFeedRangeState<ChangeFeedState>>.FromResult(crossPartitionState);
            }

            public override TryCatch<CrossFeedRangeState<ChangeFeedState>> Visit(ChangeFeedStartFromBeginning startFromBeginning)
            {
                ChangeFeedState state = ChangeFeedState.Beginning();
                ImmutableArray<FeedRangeState<ChangeFeedState>> rangesAndStates = new List<FeedRangeState<ChangeFeedState>>()
                {
                    new FeedRangeState<ChangeFeedState>(startFromBeginning.FeedRange, state)
                }.ToImmutableArray();

                CrossFeedRangeState<ChangeFeedState> crossPartitionState = new CrossFeedRangeState<ChangeFeedState>(rangesAndStates);
                return TryCatch<CrossFeedRangeState<ChangeFeedState>>.FromResult(crossPartitionState);
            }

            public override TryCatch<CrossFeedRangeState<ChangeFeedState>> Visit(ChangeFeedStartFromContinuationAndFeedRange startFromContinuationAndFeedRange)
            {
                ChangeFeedState state = ChangeFeedState.Continuation(CosmosString.Create(startFromContinuationAndFeedRange.Etag));
                ImmutableArray<FeedRangeState<ChangeFeedState>> rangesAndStates = new List<FeedRangeState<ChangeFeedState>>()
                {
                    new FeedRangeState<ChangeFeedState>(startFromContinuationAndFeedRange.FeedRange, state)
                }.ToImmutableArray();

                CrossFeedRangeState<ChangeFeedState> crossPartitionState = new CrossFeedRangeState<ChangeFeedState>(rangesAndStates);
                return TryCatch<CrossFeedRangeState<ChangeFeedState>>.FromResult(crossPartitionState);
            }
        }
    }
}
