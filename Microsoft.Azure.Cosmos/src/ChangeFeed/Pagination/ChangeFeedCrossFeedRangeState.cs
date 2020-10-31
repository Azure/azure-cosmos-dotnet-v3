// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class ChangeFeedCrossFeedRangeState
    {
        public static readonly ChangeFeedCrossFeedRangeState FullRangeStartFromBeginning = new ChangeFeedCrossFeedRangeState(
            new List<ChangeFeedFeedRangeState>()
            {
                new ChangeFeedFeedRangeState(FeedRangeEpk.FullRange, ChangeFeedState.Beginning())
            }.ToImmutableArray());

        public ChangeFeedCrossFeedRangeState(ImmutableArray<ChangeFeedFeedRangeState> feedRangeStates)
        {
            if (feedRangeStates.IsEmpty)
            {
                throw new ArgumentException($"{nameof(feedRangeStates)} is empty.");
            }

            this.FeedRangeStates = feedRangeStates;
        }

        public ImmutableArray<ChangeFeedFeedRangeState> FeedRangeStates { get; }

        public ChangeFeedCrossFeedRangeState Merge(ChangeFeedCrossFeedRangeState other)
        {
            ImmutableArray<ChangeFeedFeedRangeState> mergedStates = this.FeedRangeStates.Concat(other.FeedRangeStates).ToImmutableArray();
            return new ChangeFeedCrossFeedRangeState(mergedStates);
        }

        public bool TrySplit(out (ChangeFeedCrossFeedRangeState left, ChangeFeedCrossFeedRangeState right) children)
        {
            if (this.FeedRangeStates.Length <= 1)
            {
                // TODO: in the future we should be able to split a single feed range state as long as it's not a single point epk range.
                children = default;
                return false;
            }

            ImmutableArray<ChangeFeedFeedRangeState> leftRanges = this.FeedRangeStates.AsMemory().Slice(start: 0, this.FeedRangeStates.Length / 2).ToArray().ToImmutableArray();
            ImmutableArray<ChangeFeedFeedRangeState> rightRanges = this.FeedRangeStates.AsMemory().Slice(start: this.FeedRangeStates.Length / 2).ToArray().ToImmutableArray();
            ChangeFeedCrossFeedRangeState leftCrossFeedRangeStates = new ChangeFeedCrossFeedRangeState(leftRanges);
            ChangeFeedCrossFeedRangeState rightCrossFeedRangeStates = new ChangeFeedCrossFeedRangeState(rightRanges);

            children = (leftCrossFeedRangeStates, rightCrossFeedRangeStates);
            return true;
        }

        public CosmosElement ToCosmosElement()
        {
            List<CosmosElement> elements = new List<CosmosElement>();
            foreach (ChangeFeedFeedRangeState changeFeedFeedRangeState in this.FeedRangeStates)
            {
                elements.Add(changeFeedFeedRangeState.ToCosmosElement());
            }

            return CosmosArray.Create(elements);
        }

        public static class Monadic
        {
            public static TryCatch<ChangeFeedCrossFeedRangeState> Parse(string text)
            {
                if (text == null)
                {
                    throw new ArgumentNullException(nameof(text));
                }

                TryCatch<CosmosElement> monadicCosmosElement = CosmosElement.Monadic.Parse(text);
                if (monadicCosmosElement.Failed)
                {
                    return TryCatch<ChangeFeedCrossFeedRangeState>.FromException(monadicCosmosElement.Exception);
                }

                return CreateFromCosmosElement(monadicCosmosElement.Result);
            }

            private static TryCatch<ChangeFeedCrossFeedRangeState> CreateFromCosmosElement(CosmosElement cosmosElement)
            {
                if (cosmosElement == null)
                {
                    throw new ArgumentNullException(nameof(cosmosElement));
                }

                if (!(cosmosElement is CosmosArray cosmosArray))
                {
                    return TryCatch<ChangeFeedCrossFeedRangeState>.FromException(
                        new FormatException(
                            $"Expected array: {cosmosElement}"));
                }

                List<ChangeFeedFeedRangeState> changeFeedFeedRangeStates = new List<ChangeFeedFeedRangeState>();
                foreach (CosmosElement arrayItem in cosmosArray)
                {
                    TryCatch<ChangeFeedFeedRangeState> monadicChangeFeedFeedRangeState = ChangeFeedFeedRangeState.Monadic.CreateFromCosmosElement(arrayItem);
                    if (monadicChangeFeedFeedRangeState.Failed)
                    {
                        return TryCatch<ChangeFeedCrossFeedRangeState>.FromException(monadicChangeFeedFeedRangeState.Exception);
                    }

                    changeFeedFeedRangeStates.Add(monadicChangeFeedFeedRangeState.Result);
                }

                ImmutableArray<ChangeFeedFeedRangeState> feedRangeStates = changeFeedFeedRangeStates.ToImmutableArray();
                ChangeFeedCrossFeedRangeState changeFeedCrossFeedRangeState = new ChangeFeedCrossFeedRangeState(feedRangeStates);
                return TryCatch<ChangeFeedCrossFeedRangeState>.FromResult(changeFeedCrossFeedRangeState);
            }
        }
    }
}
