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
            if (!await this.crossPartitionEnumerator.MoveNextAsync())
            {
                this.Current = default;
                return false;
            }

            TryCatch<CrossPartitionPage<ChangeFeedPage, ChangeFeedState>> monadicCrossPartitionPage = this.crossPartitionEnumerator.Current;
            if (monadicCrossPartitionPage.Failed)
            {
                this.Current = TryCatch<ChangeFeedPage>.FromException(monadicCrossPartitionPage.Exception);
                return true;
            }

            CrossPartitionPage<ChangeFeedPage, ChangeFeedState> crossPartitionPage = monadicCrossPartitionPage.Result;
            ChangeFeedPage backendPage = crossPartitionPage.Page;
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
            string continuationToken = cosmosElementTokens.ToString();
            ChangeFeedState state = ChangeFeedState.Continuation(continuationToken);
            ChangeFeedPage compositePage = new ChangeFeedPage(
                backendPage.Content,
                backendPage.RequestCharge,
                backendPage.ActivityId,
                state);

            this.Current = TryCatch<ChangeFeedPage>.FromResult(compositePage);
            return true;
        }

        public static async Task<TryCatch<CrossPartitionChangeFeedAsyncEnumerator>> MonadicCreateAsync(
            IDocumentContainer documentContainer,
            ChangeFeedRequestOptions changeFeedRequestOptions,
            ChangeFeedStartFrom changeFeedStartFrom,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (documentContainer == null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            if (changeFeedRequestOptions == null)
            {
                throw new ArgumentNullException(nameof(changeFeedRequestOptions));
            }

            if (changeFeedStartFrom == null)
            {
                throw new ArgumentNullException(nameof(changeFeedStartFrom));
            }

            TryCatch<CrossPartitionState<ChangeFeedState>> monadicCrossPartitionState = await changeFeedStartFrom.AcceptAsync(
                CrossPartitionStateAsyncExtractor.Singleton,
                documentContainer,
                cancellationToken);
            if (monadicCrossPartitionState.Failed)
            {
                return TryCatch<CrossPartitionChangeFeedAsyncEnumerator>.FromException(monadicCrossPartitionState.Exception);
            }

            CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> crossPartitionEnumerator = new CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState>(
                documentContainer,
                CrossPartitionChangeFeedAsyncEnumerator.MakeCreateFunction(
                    documentContainer,
                    changeFeedRequestOptions.PageSizeHint.GetValueOrDefault(100),
                    cancellationToken),
                Comparer.Singleton,
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

        private sealed class CrossPartitionStateAsyncExtractor : ChangeFeedStartFromAsyncVisitor<IDocumentContainer, TryCatch<CrossPartitionState<ChangeFeedState>>>
        {
            public static readonly CrossPartitionStateAsyncExtractor Singleton = new CrossPartitionStateAsyncExtractor();

            private CrossPartitionStateAsyncExtractor()
            {
            }

            public override async Task<TryCatch<CrossPartitionState<ChangeFeedState>>> VisitAsync(
                ChangeFeedStartFromNow startFromNow,
                IDocumentContainer documentContainer,
                CancellationToken cancellationToken)
            {
                ChangeFeedState state = ChangeFeedState.Now();
                List<(FeedRangeInternal, ChangeFeedState)> rangesAndStates = (await documentContainer
                    .GetChildRangeAsync(
                        startFromNow.FeedRange,
                        cancellationToken))
                    .Select(range => ((FeedRangeInternal)range, state))
                    .ToList();

                CrossPartitionState<ChangeFeedState> crossPartitionState = new CrossPartitionState<ChangeFeedState>(rangesAndStates);
                return TryCatch<CrossPartitionState<ChangeFeedState>>.FromResult(crossPartitionState);
            }

            public override async Task<TryCatch<CrossPartitionState<ChangeFeedState>>> VisitAsync(
                ChangeFeedStartFromTime startFromTime,
                IDocumentContainer documentContainer,
                CancellationToken cancellationToken)
            {
                ChangeFeedState state = ChangeFeedState.Time(startFromTime.StartTime);
                List<(FeedRangeInternal, ChangeFeedState)> rangesAndStates = (await documentContainer
                    .GetChildRangeAsync(
                        startFromTime.FeedRange,
                        cancellationToken))
                    .Select(range => ((FeedRangeInternal)range, state))
                    .ToList();

                CrossPartitionState<ChangeFeedState> crossPartitionState = new CrossPartitionState<ChangeFeedState>(rangesAndStates);
                return TryCatch<CrossPartitionState<ChangeFeedState>>.FromResult(crossPartitionState);
            }

            public override Task<TryCatch<CrossPartitionState<ChangeFeedState>>> VisitAsync(
                ChangeFeedStartFromContinuation startFromContinuation,
                IDocumentContainer documentContainer,
                CancellationToken cancellationToken)
            {
                string continuationToken = startFromContinuation.Continuation;
                TryCatch<CosmosArray> monadicCosmosArray = CosmosArray.Monadic.Parse(continuationToken);
                if (monadicCosmosArray.Failed)
                {
                    return Task.FromResult(
                        TryCatch<CrossPartitionState<ChangeFeedState>>.FromException(
                            new MalformedChangeFeedContinuationTokenException(
                                message: $"Array expected for change feed continuation token: {continuationToken}.",
                                innerException: monadicCosmosArray.Exception)));
                }

                CosmosArray cosmosArray = monadicCosmosArray.Result;
                if (cosmosArray.Count == 0)
                {
                    return Task.FromResult(
                        TryCatch<CrossPartitionState<ChangeFeedState>>.FromException(
                            new MalformedChangeFeedContinuationTokenException(
                                message: $"non empty array expected for change feed continuation token: {continuationToken}.")));
                }

                List<(FeedRangeInternal, ChangeFeedState)> rangeAndStates = new List<(FeedRangeInternal, ChangeFeedState)>();
                foreach (CosmosElement arrayItem in cosmosArray)
                {
                    TryCatch<ChangeFeedContinuationToken> monadicChangeFeedContinuationToken = ChangeFeedContinuationToken.MonadicConvertFromCosmosElement(arrayItem);
                    if (monadicChangeFeedContinuationToken.Failed)
                    {
                        return Task.FromResult(
                            TryCatch<CrossPartitionState<ChangeFeedState>>.FromException(
                                new MalformedChangeFeedContinuationTokenException(
                                    message: $"Failed to parse change feed continuation token: {continuationToken}.",
                                    innerException: monadicChangeFeedContinuationToken.Exception)));
                    }

                    ChangeFeedContinuationToken changeFeedContinuationToken = monadicChangeFeedContinuationToken.Result;
                    rangeAndStates.Add((changeFeedContinuationToken.Range, changeFeedContinuationToken.State));
                }

                CrossPartitionState<ChangeFeedState> crossPartitionState = new CrossPartitionState<ChangeFeedState>(rangeAndStates);
                return Task.FromResult(TryCatch<CrossPartitionState<ChangeFeedState>>.FromResult(crossPartitionState));
            }

            public override async Task<TryCatch<CrossPartitionState<ChangeFeedState>>> VisitAsync(
                ChangeFeedStartFromBeginning startFromBeginning,
                IDocumentContainer documentContainer,
                CancellationToken cancellationToken)
            {
                ChangeFeedState state = ChangeFeedState.Beginning();
                List<(FeedRangeInternal, ChangeFeedState)> rangesAndStates = (await documentContainer
                    .GetChildRangeAsync(
                        startFromBeginning.FeedRange,
                        cancellationToken))
                    .Select(range => ((FeedRangeInternal)range, state))
                    .ToList();

                CrossPartitionState<ChangeFeedState> crossPartitionState = new CrossPartitionState<ChangeFeedState>(rangesAndStates);
                return TryCatch<CrossPartitionState<ChangeFeedState>>.FromResult(crossPartitionState);
            }

            public override Task<TryCatch<CrossPartitionState<ChangeFeedState>>> VisitAsync(
                ChangeFeedStartFromContinuationAndFeedRange startFromContinuationAndFeedRange,
                IDocumentContainer documentContainer,
                CancellationToken cancellationToken)
            {
                ChangeFeedState state = ChangeFeedState.Continuation(startFromContinuationAndFeedRange.Etag);
                List<(FeedRangeInternal, ChangeFeedState)> rangesAndStates = new List<(FeedRangeInternal, ChangeFeedState)>()
                {
                    (startFromContinuationAndFeedRange.FeedRange, state)
                };

                CrossPartitionState<ChangeFeedState> crossPartitionState = new CrossPartitionState<ChangeFeedState>(rangesAndStates);
                return Task.FromResult(TryCatch<CrossPartitionState<ChangeFeedState>>.FromResult(crossPartitionState));
            }
        }

        private readonly struct ChangeFeedContinuationToken
        {
            private static class PropertyNames
            {
                public const string FeedRange = "FeedRange";
                public const string State = "State";
            }

            public ChangeFeedContinuationToken(FeedRangeInternal feedRange, ChangeFeedState changeFeedState)
            {
                this.Range = feedRange ?? throw new ArgumentNullException(nameof(feedRange));
                this.State = changeFeedState ?? throw new ArgumentNullException(nameof(changeFeedState));
            }

            public FeedRangeInternal Range { get; }
            public ChangeFeedState State { get; }

            public static CosmosElement ToCosmosElement(ChangeFeedContinuationToken changeFeedContinuationToken)
            {
                return CosmosObject.Create(new Dictionary<string, CosmosElement>()
                {
                    {
                        PropertyNames.FeedRange,
                        FeedRangeCosmosElementSerializer.ToCosmosElement(changeFeedContinuationToken.Range)
                    },
                    {
                        PropertyNames.State,
                        ChangeFeedStateCosmosElementSerializer.ToCosmosElement(changeFeedContinuationToken.State)
                    }
                });
            }

            public static TryCatch<ChangeFeedContinuationToken> MonadicConvertFromCosmosElement(CosmosElement cosmosElement)
            {
                if (cosmosElement == null)
                {
                    throw new ArgumentNullException(nameof(cosmosElement));
                }

                if (!(cosmosElement is CosmosObject cosmosObject))
                {
                    return TryCatch<ChangeFeedContinuationToken>.FromException(
                        new FormatException(
                            $"Expected object for ChangeFeed Continuation: {cosmosElement}."));
                }

                if (!cosmosObject.TryGetValue(PropertyNames.FeedRange, out CosmosElement feedRangeCosmosElement))
                {
                    return TryCatch<ChangeFeedContinuationToken>.FromException(
                        new FormatException(
                            $"Expected '{PropertyNames.FeedRange}' for '{nameof(ChangeFeedContinuationToken)}': {cosmosElement}."));
                }

                if (!cosmosObject.TryGetValue(PropertyNames.State, out CosmosElement stateCosmosElement))
                {
                    return TryCatch<ChangeFeedContinuationToken>.FromException(
                        new FormatException(
                            $"Expected '{PropertyNames.State}' for '{nameof(ChangeFeedContinuationToken)}': {cosmosElement}."));
                }

                TryCatch<FeedRangeInternal> monadicFeedRange = FeedRangeCosmosElementSerializer.MonadicCreateFromCosmosElement(feedRangeCosmosElement);
                if (monadicFeedRange.Failed)
                {
                    return TryCatch<ChangeFeedContinuationToken>.FromException(
                        new FormatException(
                            $"Failed to parse '{PropertyNames.FeedRange}' for '{nameof(ChangeFeedContinuationToken)}': {cosmosElement}.",
                            innerException: monadicFeedRange.Exception));
                }

                TryCatch<ChangeFeedState> monadicChangeFeedState = ChangeFeedStateCosmosElementSerializer.MonadicFromCosmosElement(stateCosmosElement);
                if (monadicChangeFeedState.Failed)
                {
                    return TryCatch<ChangeFeedContinuationToken>.FromException(
                        new FormatException(
                            $"Failed to parse '{PropertyNames.State}' for '{nameof(ChangeFeedContinuationToken)}': {cosmosElement}.",
                            innerException: monadicChangeFeedState.Exception));
                }

                return TryCatch<ChangeFeedContinuationToken>.FromResult(
                    new ChangeFeedContinuationToken(
                        monadicFeedRange.Result,
                        monadicChangeFeedState.Result));
            }
        }

        private sealed class Comparer : IComparer<PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState>>
        {
            public static readonly Comparer Singleton = new Comparer();

            private Comparer()
            {
            }

            public int Compare(
                PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> x,
                PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> y)
            {
                // Picking 1 so that the new job always moves to the end (Round Robin scheduler)
                return 1;
            }
        }
    }
}
