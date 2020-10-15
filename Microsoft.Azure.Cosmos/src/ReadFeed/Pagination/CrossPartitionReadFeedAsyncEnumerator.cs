// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tests.Pagination;

    internal sealed class CrossPartitionReadFeedAsyncEnumerator : IAsyncEnumerator<TryCatch<ReadFeedPage>>
    {
        private readonly CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> crossPartitionEnumerator;
        private CancellationToken cancellationToken;

        private CrossPartitionReadFeedAsyncEnumerator(
            CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> crossPartitionEnumerator,
            CancellationToken cancellationToken)
        {
            this.crossPartitionEnumerator = crossPartitionEnumerator ?? throw new ArgumentNullException(nameof(crossPartitionEnumerator));
            this.cancellationToken = cancellationToken;
        }

        public TryCatch<ReadFeedPage> Current { get; set; }

        public async ValueTask<bool> MoveNextAsync()
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (!await this.crossPartitionEnumerator.MoveNextAsync())
            {
                this.Current = default;
                return false;
            }

            TryCatch<CrossPartitionPage<ReadFeedPage, ReadFeedState>> monadicCrossPartitionPage = this.crossPartitionEnumerator.Current;
            if (monadicCrossPartitionPage.Failed)
            {
                this.Current = TryCatch<ReadFeedPage>.FromException(monadicCrossPartitionPage.Exception);
                return true;
            }

            CrossPartitionPage<ReadFeedPage, ReadFeedState> crossPartitionPage = monadicCrossPartitionPage.Result;
            ReadFeedPage backendPage = crossPartitionPage.Page;
            CrossPartitionState<ReadFeedState> crossPartitionState = crossPartitionPage.State;

            List<CosmosElement> changeFeedContinuationTokens = new List<CosmosElement>();
            foreach ((FeedRangeInternal range, ReadFeedState state) rangeAndState in crossPartitionState.Value)
            {
                ReadFeedContinuationToken readFeedContinuationToken = new ReadFeedContinuationToken(
                    rangeAndState.range,
                    rangeAndState.state);
                CosmosElement cosmosElementChangeFeedContinuationToken = ReadFeedContinuationToken.ToCosmosElement(readFeedContinuationToken);
                changeFeedContinuationTokens.Add(cosmosElementChangeFeedContinuationToken);
            }

            CosmosArray cosmosElementTokens = CosmosArray.Create(changeFeedContinuationTokens);
            ReadFeedState state = new ReadFeedState(cosmosElementTokens);
            ReadFeedPage compositePage = new ReadFeedPage(backendPage.Content, backendPage.RequestCharge, backendPage.ActivityId, state);

            this.Current = TryCatch<ReadFeedPage>.FromResult(compositePage);
            return true;
        }

        public ValueTask DisposeAsync() => this.enumerator.DisposeAsync();

        public static TryCatch<CrossPartitionReadFeedAsyncEnumerator> MonadicCreate(
             IDocumentContainer documentContainer,
             string continuationToken,
             int pageSize,
             CancellationToken cancellationToken)
        {
            if (documentContainer == null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            TryCatch<CrossPartitionState<ReadFeedState>> monadicCrossPartitionState = MonadicParseCrossPartitionState(continuationToken);
            if (monadicCrossPartitionState.Failed)
            {
                return TryCatch<CrossPartitionReadFeedAsyncEnumerator>.FromException(monadicCrossPartitionState.Exception);
            }

            CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> crossPartitionEnumerator = new CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                documentContainer,
                CrossPartitionReadFeedAsyncEnumerator.MakeCreateFunction(
                    documentContainer,
                    pageSize,
                    cancellationToken),
                comparer: PartitionRangePageAsyncEnumeratorComparer.Singleton,
                maxConcurrency: default,
                cancellationToken,
                monadicCrossPartitionState.Result);

            CrossPartitionReadFeedAsyncEnumerator enumerator = new CrossPartitionReadFeedAsyncEnumerator(
                crossPartitionEnumerator,
                cancellationToken);

            return TryCatch<CrossPartitionReadFeedAsyncEnumerator>.FromResult(enumerator);
        }

        private static TryCatch<CrossPartitionState<ReadFeedState>> MonadicParseCrossPartitionState(string continuation)
        {
            if (continuation == default)
            {
                return TryCatch<CrossPartitionState<ReadFeedState>>.FromResult(default);
            }

            TryCatch<CosmosArray> monadicCosmosArray = CosmosArray.Monadic.Parse(continuation);
            if (monadicCosmosArray.Failed)
            {
                return TryCatch<CrossPartitionState<ReadFeedState>>.FromException(
                    new FormatException($"Expected array for {nameof(CrossPartitionState<ReadFeedState>)}: {continuation}",
                    monadicCosmosArray.Exception));
            }

            List<ReadFeedContinuationToken> readFeedContinuationTokens = new List<ReadFeedContinuationToken>();
            foreach (CosmosElement arrayItem in monadicCosmosArray.Result)
            {
                TryCatch<ReadFeedContinuationToken> monadicReadFeedContinuationToken = ReadFeedContinuationToken.MonadicConvertFromCosmosElement(arrayItem);
                if (monadicReadFeedContinuationToken.Failed)
                {
                    return TryCatch<CrossPartitionState<ReadFeedState>>.FromException(
                        new FormatException($"failed to parse array item for {nameof(CrossPartitionState<ReadFeedState>)}: {continuation}",
                        monadicReadFeedContinuationToken.Exception));
                }

                readFeedContinuationTokens.Add(monadicReadFeedContinuationToken.Result);
            }

            List<(FeedRangeInternal, ReadFeedState)> crossPartitionStateElements = new List<(FeedRangeInternal, ReadFeedState)>();
            foreach (ReadFeedContinuationToken token in readFeedContinuationTokens)
            {
                crossPartitionStateElements.Add((token.Range, token.State));
            }

            CrossPartitionState<ReadFeedState> crossPartitionState = new CrossPartitionState<ReadFeedState>(crossPartitionStateElements);
            return TryCatch<CrossPartitionState<ReadFeedState>>.FromResult(crossPartitionState);
        }

        private static CreatePartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> MakeCreateFunction(
            IReadFeedDataSource readFeedDataSource,
            int pageSize,
            CancellationToken cancellationToken) => (FeedRangeInternal range, ReadFeedState state) => new ReadFeedPartitionRangeEnumerator(
                readFeedDataSource,
                range,
                pageSize,
                state,
                cancellationToken);


        internal readonly struct ReadFeedContinuationToken
        {
            private static class PropertyNames
            {
                public const string FeedRange = "FeedRange";
                public const string State = "State";
            }

            public ReadFeedContinuationToken(FeedRangeInternal feedRange, ReadFeedState readFeedState)
            {
                this.Range = feedRange ?? throw new ArgumentNullException(nameof(feedRange));
                this.State = readFeedState ?? throw new ArgumentNullException(nameof(readFeedState));
            }

            public FeedRangeInternal Range { get; }
            public ReadFeedState State { get; }

            public static CosmosElement ToCosmosElement(ReadFeedContinuationToken readFeedContinuationToken)
            {
                return CosmosObject.Create(new Dictionary<string, CosmosElement>()
            {
                {
                    PropertyNames.FeedRange,
                    FeedRangeCosmosElementSerializer.ToCosmosElement(readFeedContinuationToken.Range)
                },
                {
                    PropertyNames.State,
                    ChangeFeedStateCosmosElementSerializer.ToCosmosElement(readFeedContinuationToken.State)
                }
            });
            }

            public static TryCatch<ReadFeedContinuationToken> MonadicConvertFromCosmosElement(CosmosElement cosmosElement)
            {
                if (cosmosElement == null)
                {
                    throw new ArgumentNullException(nameof(cosmosElement));
                }

                if (!(cosmosElement is CosmosObject cosmosObject))
                {
                    return TryCatch<ReadFeedContinuationToken>.FromException(
                        new FormatException(
                            $"Expected object for ChangeFeed Continuation: {cosmosElement}."));
                }

                if (!cosmosObject.TryGetValue(PropertyNames.FeedRange, out CosmosElement feedRangeCosmosElement))
                {
                    return TryCatch<ReadFeedContinuationToken>.FromException(
                        new FormatException(
                            $"Expected '{PropertyNames.FeedRange}' for '{nameof(ReadFeedContinuationToken)}': {cosmosElement}."));
                }

                if (!cosmosObject.TryGetValue(PropertyNames.State, out CosmosElement stateCosmosElement))
                {
                    return TryCatch<ReadFeedContinuationToken>.FromException(
                        new FormatException(
                            $"Expected '{PropertyNames.State}' for '{nameof(ReadFeedContinuationToken)}': {cosmosElement}."));
                }

                TryCatch<FeedRangeInternal> monadicFeedRange = FeedRangeCosmosElementSerializer.MonadicCreateFromCosmosElement(feedRangeCosmosElement);
                if (monadicFeedRange.Failed)
                {
                    return TryCatch<ReadFeedContinuationToken>.FromException(
                        new FormatException(
                            $"Failed to parse '{PropertyNames.FeedRange}' for '{nameof(ReadFeedContinuationToken)}': {cosmosElement}.",
                            innerException: monadicFeedRange.Exception));
                }

                TryCatch<ReadFeedState> monadicReadFeedState;
                if (stateCosmosElement is CosmosNull)
                {
                    monadicReadFeedState = TryCatch<ReadFeedState>.FromResult(null);
                }
                else if (stateCosmosElement is CosmosString cosmosString)
                {
                    monadicReadFeedState = TryCatch<ReadFeedState>.FromResult(new ReadFeedState(cosmosString.Value));
                }
                else
                {
                    monadicReadFeedState = TryCatch<ReadFeedState>.FromException(
                        new FormatException(
                            "Expected state to either be null or a string."));
                }

                if (monadicReadFeedState.Failed)
                {
                    return TryCatch<ReadFeedContinuationToken>.FromException(
                        new FormatException(
                            $"Failed to parse '{PropertyNames.State}' for '{nameof(ReadFeedContinuationToken)}': {cosmosElement}.",
                            innerException: monadicReadFeedState.Exception));
                }

                return TryCatch<ReadFeedContinuationToken>.FromResult(
                    new ReadFeedContinuationToken(
                        monadicFeedRange.Result,
                        monadicReadFeedState.Result));
            }
        }

        private sealed class PartitionRangePageAsyncEnumeratorComparer : IComparer<PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>>
        {
            public static readonly PartitionRangePageAsyncEnumeratorComparer Singleton = new PartitionRangePageAsyncEnumeratorComparer();

            public int Compare(
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> partitionRangePageEnumerator1,
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> partitionRangePageEnumerator2)
            {
                if (object.ReferenceEquals(partitionRangePageEnumerator1, partitionRangePageEnumerator2))
                {
                    return 0;
                }

                // Either both don't have results or both do.
                return string.CompareOrdinal(
                    ((FeedRangeEpk)partitionRangePageEnumerator1.Range).Range.Min,
                    ((FeedRangeEpk)partitionRangePageEnumerator2.Range).Range.Min);
            }
        }
    }
}
