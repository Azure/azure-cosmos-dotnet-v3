// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class CrossPartitionChangeFeedAsyncEnumerator : IAsyncEnumerator<TryCatch<ChangeFeedPage>>
    {
        private readonly CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> crossPartitionEnumerator;
        private readonly CancellationToken cancellationToken;
        private TryCatch<ChangeFeedPage>? bufferedException;

        private CrossPartitionChangeFeedAsyncEnumerator(
            CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> crossPartitionEnumerator,
            CancellationToken cancellationToken)
        {
            this.crossPartitionEnumerator = crossPartitionEnumerator ?? throw new ArgumentNullException(nameof(crossPartitionEnumerator));
            this.cancellationToken = cancellationToken;
        }

        public TryCatch<ChangeFeedPage> Current { get; private set; }

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

            TryCatch<CrossPartitionPage<ChangeFeedPage, ChangeFeedState>> monadicCrossPartitionPage = this.crossPartitionEnumerator.Current;
            if (monadicCrossPartitionPage.Failed)
            {
                this.Current = TryCatch<ChangeFeedPage>.FromException(monadicCrossPartitionPage.Exception);
                return true;
            }

            CrossPartitionPage<ChangeFeedPage, ChangeFeedState> crossPartitionPage = monadicCrossPartitionPage.Result;
            ChangeFeedPage backendPage = crossPartitionPage.Page;
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
                        this.bufferedException = TryCatch<ChangeFeedPage>.FromException(monadicCrossPartitionPage.Exception);
                    }
                    else
                    {
                        crossPartitionPage = monadicCrossPartitionPage.Result;
                        backendPage = crossPartitionPage.Page;
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

            CrossPartitionState<ChangeFeedState> crossPartitionState = crossPartitionPage.State;

            List<CosmosElement> changeFeedContinuationTokens = new List<CosmosElement>();
            foreach ((FeedRangeInternal range, ChangeFeedState state) rangeAndState in crossPartitionState.Value)
            {
                ChangeFeedContinuationToken changeFeedContinuationToken = new ChangeFeedContinuationToken(
                    rangeAndState.range,
                    rangeAndState.state);
                CosmosElement cosmosElementChangeFeedContinuationToken = ChangeFeedContinuationToken.ToCosmosElement(changeFeedContinuationToken);
                changeFeedContinuationTokens.Add(cosmosElementChangeFeedContinuationToken);
            }

            CosmosArray cosmosElementTokens = CosmosArray.Create(changeFeedContinuationTokens);
            ChangeFeedState state = ChangeFeedState.Continuation(cosmosElementTokens);
            ChangeFeedPage compositePage;
            if (backendPage is ChangeFeedSuccessPage successPage)
            {
                compositePage = new ChangeFeedSuccessPage(
                    successPage.Content,
                    successPage.RequestCharge,
                    successPage.ActivityId,
                    state);
            }
            else
            {
                compositePage = new ChangeFeedNotModifiedPage(
                    backendPage.RequestCharge,
                    backendPage.ActivityId,
                    state);
            }

            this.Current = TryCatch<ChangeFeedPage>.FromResult(compositePage);
            return true;
        }

        public static TryCatch<CrossPartitionChangeFeedAsyncEnumerator> MonadicCreate(
            IDocumentContainer documentContainer,
            ChangeFeedRequestOptions changeFeedRequestOptions,
            ChangeFeedStartFrom changeFeedStartFrom,
            CancellationToken cancellationToken)
        {
            changeFeedRequestOptions ??= new ChangeFeedRequestOptions();

            if (documentContainer == null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            if (changeFeedStartFrom == null)
            {
                throw new ArgumentNullException(nameof(changeFeedStartFrom));
            }

            TryCatch<CrossPartitionState<ChangeFeedState>> monadicCrossPartitionState = changeFeedStartFrom.Accept(CrossPartitionStateExtractor.Singleton);
            if (monadicCrossPartitionState.Failed)
            {
                return TryCatch<CrossPartitionChangeFeedAsyncEnumerator>.FromException(monadicCrossPartitionState.Exception);
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
                monadicCrossPartitionState.Result);

            CrossPartitionChangeFeedAsyncEnumerator enumerator = new CrossPartitionChangeFeedAsyncEnumerator(
                crossPartitionEnumerator,
                cancellationToken);

            return TryCatch<CrossPartitionChangeFeedAsyncEnumerator>.FromResult(enumerator);
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

        private sealed class CrossPartitionStateExtractor : ChangeFeedStartFromVisitor<TryCatch<CrossPartitionState<ChangeFeedState>>>
        {
            public static readonly CrossPartitionStateExtractor Singleton = new CrossPartitionStateExtractor();

            private CrossPartitionStateExtractor()
            {
            }

            public override TryCatch<CrossPartitionState<ChangeFeedState>> Visit(ChangeFeedStartFromNow startFromNow)
            {
                ChangeFeedState state = ChangeFeedState.Now();
                List<(FeedRangeInternal, ChangeFeedState)> rangesAndStates = new List<(FeedRangeInternal, ChangeFeedState)>()
                {
                    (startFromNow.FeedRange, state)
                };

                CrossPartitionState<ChangeFeedState> crossPartitionState = new CrossPartitionState<ChangeFeedState>(rangesAndStates);
                return TryCatch<CrossPartitionState<ChangeFeedState>>.FromResult(crossPartitionState);
            }

            public override TryCatch<CrossPartitionState<ChangeFeedState>> Visit(ChangeFeedStartFromTime startFromTime)
            {
                ChangeFeedState state = ChangeFeedState.Time(startFromTime.StartTime);
                List<(FeedRangeInternal, ChangeFeedState)> rangesAndStates = new List<(FeedRangeInternal, ChangeFeedState)>()
                {
                    (startFromTime.FeedRange, state)
                };

                CrossPartitionState<ChangeFeedState> crossPartitionState = new CrossPartitionState<ChangeFeedState>(rangesAndStates);
                return TryCatch<CrossPartitionState<ChangeFeedState>>.FromResult(crossPartitionState);
            }

            public override TryCatch<CrossPartitionState<ChangeFeedState>> Visit(ChangeFeedStartFromContinuation startFromContinuation)
            {
                string continuationToken = startFromContinuation.Continuation;
                TryCatch<CosmosArray> monadicCosmosArray = CosmosArray.Monadic.Parse(continuationToken);
                if (monadicCosmosArray.Failed)
                {
                    return TryCatch<CrossPartitionState<ChangeFeedState>>.FromException(
                        new MalformedChangeFeedContinuationTokenException(
                            message: $"Array expected for change feed continuation token: {continuationToken}.",
                            innerException: monadicCosmosArray.Exception));
                }

                CosmosArray cosmosArray = monadicCosmosArray.Result;
                if (cosmosArray.Count == 0)
                {
                    return TryCatch<CrossPartitionState<ChangeFeedState>>.FromException(
                        new MalformedChangeFeedContinuationTokenException(
                            message: $"non empty array expected for change feed continuation token: {continuationToken}."));
                }

                List<(FeedRangeInternal, ChangeFeedState)> rangeAndStates = new List<(FeedRangeInternal, ChangeFeedState)>();
                foreach (CosmosElement arrayItem in cosmosArray)
                {
                    TryCatch<ChangeFeedContinuationToken> monadicChangeFeedContinuationToken = ChangeFeedContinuationToken.MonadicConvertFromCosmosElement(arrayItem);
                    if (monadicChangeFeedContinuationToken.Failed)
                    {
                        return TryCatch<CrossPartitionState<ChangeFeedState>>.FromException(
                            new MalformedChangeFeedContinuationTokenException(
                                message: $"Failed to parse change feed continuation token: {continuationToken}.",
                                innerException: monadicChangeFeedContinuationToken.Exception));
                    }

                    ChangeFeedContinuationToken changeFeedContinuationToken = monadicChangeFeedContinuationToken.Result;
                    rangeAndStates.Add((changeFeedContinuationToken.Range, changeFeedContinuationToken.State));
                }

                CrossPartitionState<ChangeFeedState> crossPartitionState = new CrossPartitionState<ChangeFeedState>(rangeAndStates);
                return TryCatch<CrossPartitionState<ChangeFeedState>>.FromResult(crossPartitionState);
            }

            public override TryCatch<CrossPartitionState<ChangeFeedState>> Visit(ChangeFeedStartFromBeginning startFromBeginning)
            {
                ChangeFeedState state = ChangeFeedState.Beginning();
                List<(FeedRangeInternal, ChangeFeedState)> rangesAndStates = new List<(FeedRangeInternal, ChangeFeedState)>()
                {
                    (startFromBeginning.FeedRange, state)
                };

                CrossPartitionState<ChangeFeedState> crossPartitionState = new CrossPartitionState<ChangeFeedState>(rangesAndStates);
                return TryCatch<CrossPartitionState<ChangeFeedState>>.FromResult(crossPartitionState);
            }

            public override TryCatch<CrossPartitionState<ChangeFeedState>> Visit(ChangeFeedStartFromContinuationAndFeedRange startFromContinuationAndFeedRange)
            {
                ChangeFeedState state = ChangeFeedState.Continuation(CosmosString.Create(startFromContinuationAndFeedRange.Etag));
                List<(FeedRangeInternal, ChangeFeedState)> rangesAndStates = new List<(FeedRangeInternal, ChangeFeedState)>()
                {
                    (startFromContinuationAndFeedRange.FeedRange, state)
                };

                CrossPartitionState<ChangeFeedState> crossPartitionState = new CrossPartitionState<ChangeFeedState>(rangesAndStates);
                return TryCatch<CrossPartitionState<ChangeFeedState>>.FromResult(crossPartitionState);
            }
        }
    }
}
