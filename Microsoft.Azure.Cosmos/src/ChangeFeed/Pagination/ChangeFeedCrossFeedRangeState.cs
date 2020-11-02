// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal readonly struct ChangeFeedCrossFeedRangeState
    {
        private static class FullRangeStatesSingletons
        {
            public static readonly ChangeFeedCrossFeedRangeState Beginning = new ChangeFeedCrossFeedRangeState(
                new List<FeedRangeState<ChangeFeedState>>()
                {
                    new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Beginning())
                }.ToImmutableArray());

            public static readonly ChangeFeedCrossFeedRangeState Now = new ChangeFeedCrossFeedRangeState(
                new List<FeedRangeState<ChangeFeedState>>()
                {
                    new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Now())
                }.ToImmutableArray());
        }

        private readonly CrossFeedRangeState<ChangeFeedState> crossFeedRangeState;

        public ChangeFeedCrossFeedRangeState(ImmutableArray<FeedRangeState<ChangeFeedState>> feedRangeStates)
        {
            this.crossFeedRangeState = new CrossFeedRangeState<ChangeFeedState>(feedRangeStates);
        }

        internal ImmutableArray<FeedRangeState<ChangeFeedState>> FeedRangeStates => this.crossFeedRangeState.Value;

        public ChangeFeedCrossFeedRangeState Merge(ChangeFeedCrossFeedRangeState other)
        {
            return new ChangeFeedCrossFeedRangeState(this.crossFeedRangeState.Merge(other.crossFeedRangeState).Value);
        }

        public bool TrySplit(out (ChangeFeedCrossFeedRangeState left, ChangeFeedCrossFeedRangeState right) children)
        {
            if (!this.crossFeedRangeState.TrySplit(out (CrossFeedRangeState<ChangeFeedState> left, CrossFeedRangeState<ChangeFeedState> right) result))
            {
                children = default;
                return false;
            }

            children = (new ChangeFeedCrossFeedRangeState(result.left.Value), new ChangeFeedCrossFeedRangeState(result.right.Value));
            return true;
        }

        public CosmosElement ToCosmosElement()
        {
            List<CosmosElement> elements = new List<CosmosElement>();
            foreach (FeedRangeState<ChangeFeedState> changeFeedFeedRangeState in this.FeedRangeStates)
            {
                elements.Add(ChangeFeedFeedRangeStateSerializer.ToCosmosElement(changeFeedFeedRangeState));
            }

            return CosmosArray.Create(elements);
        }

        public override string ToString()
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            this.ToCosmosElement().WriteTo(jsonWriter);
            ReadOnlyMemory<byte> result = jsonWriter.GetResult();
            return Encoding.UTF8.GetString(result.Span);
        }

        public static ChangeFeedCrossFeedRangeState Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            TryCatch<ChangeFeedCrossFeedRangeState> monadicParse = Monadic.Parse(text);
            monadicParse.ThrowIfFailed();

            return monadicParse.Result;
        }

        public static bool TryParse(string text, out ChangeFeedCrossFeedRangeState state)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            TryCatch<ChangeFeedCrossFeedRangeState> monadicParse = Monadic.Parse(text);
            if (monadicParse.Failed)
            {
                state = default;
                return false;
            }

            state = monadicParse.Result;
            return true;
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

            internal static TryCatch<ChangeFeedCrossFeedRangeState> CreateFromCosmosElement(CosmosElement cosmosElement)
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

                List<FeedRangeState<ChangeFeedState>> changeFeedFeedRangeStates = new List<FeedRangeState<ChangeFeedState>>();
                foreach (CosmosElement arrayItem in cosmosArray)
                {
                    TryCatch<FeedRangeState<ChangeFeedState>> monadicChangeFeedFeedRangeState = ChangeFeedFeedRangeStateSerializer.Monadic.CreateFromCosmosElement(arrayItem);
                    if (monadicChangeFeedFeedRangeState.Failed)
                    {
                        return TryCatch<ChangeFeedCrossFeedRangeState>.FromException(monadicChangeFeedFeedRangeState.Exception);
                    }

                    changeFeedFeedRangeStates.Add(monadicChangeFeedFeedRangeState.Result);
                }

                ImmutableArray<FeedRangeState<ChangeFeedState>> feedRangeStates = changeFeedFeedRangeStates.ToImmutableArray();
                ChangeFeedCrossFeedRangeState changeFeedCrossFeedRangeState = new ChangeFeedCrossFeedRangeState(feedRangeStates);
                return TryCatch<ChangeFeedCrossFeedRangeState>.FromResult(changeFeedCrossFeedRangeState);
            }
        }

        public static ChangeFeedCrossFeedRangeState CreateFromBeginning()
        {
            return CreateFromBeginning(FeedRangeEpk.FullRange);
        }

        public static ChangeFeedCrossFeedRangeState CreateFromBeginning(FeedRange feedRange)
        {
            if (!(feedRange is FeedRangeInternal feedRangeInternal))
            {
                throw new ArgumentException($"{nameof(feedRange)} needs to be a {nameof(FeedRangeInternal)}.");
            }

            if (feedRange.Equals(FeedRangeEpk.FullRange))
            {
                return FullRangeStatesSingletons.Beginning;
            }

            return new ChangeFeedCrossFeedRangeState(
                new List<FeedRangeState<ChangeFeedState>>()
                {
                    new FeedRangeState<ChangeFeedState>(feedRangeInternal, ChangeFeedState.Beginning())
                }.ToImmutableArray());
        }

        public static ChangeFeedCrossFeedRangeState CreateFromNow()
        {
            return CreateFromNow(FeedRangeEpk.FullRange);
        }

        public static ChangeFeedCrossFeedRangeState CreateFromNow(FeedRange feedRange)
        {
            if (!(feedRange is FeedRangeInternal feedRangeInternal))
            {
                throw new ArgumentException($"{nameof(feedRange)} needs to be a {nameof(FeedRangeInternal)}.");
            }

            if (feedRange.Equals(FeedRangeEpk.FullRange))
            {
                return FullRangeStatesSingletons.Now;
            }

            return new ChangeFeedCrossFeedRangeState(
                new List<FeedRangeState<ChangeFeedState>>()
                {
                    new FeedRangeState<ChangeFeedState>(feedRangeInternal, ChangeFeedState.Now())
                }.ToImmutableArray());
        }

        public static ChangeFeedCrossFeedRangeState CreateFromTime(DateTime dateTimeUtc)
        {
            return CreateFromTime(dateTimeUtc, FeedRangeEpk.FullRange);
        }

        public static ChangeFeedCrossFeedRangeState CreateFromTime(DateTime dateTimeUtc, FeedRange feedRange)
        {
            if (!(feedRange is FeedRangeInternal feedRangeInternal))
            {
                throw new ArgumentException($"{nameof(feedRange)} needs to be a {nameof(FeedRangeInternal)}.");
            }

            return new ChangeFeedCrossFeedRangeState(
                new List<FeedRangeState<ChangeFeedState>>()
                {
                    new FeedRangeState<ChangeFeedState>(feedRangeInternal, ChangeFeedState.Time(dateTimeUtc))
                }.ToImmutableArray());
        }

        public static ChangeFeedCrossFeedRangeState CreateFromContinuation(CosmosElement continuation)
        {
            return CreateFromContinuation(continuation, FeedRangeEpk.FullRange);
        }

        public static ChangeFeedCrossFeedRangeState CreateFromContinuation(CosmosElement continuation, FeedRange feedRange)
        {
            if (!(feedRange is FeedRangeInternal feedRangeInternal))
            {
                throw new ArgumentException($"{nameof(feedRange)} needs to be a {nameof(FeedRangeInternal)}.");
            }

            return new ChangeFeedCrossFeedRangeState(
                new List<FeedRangeState<ChangeFeedState>>()
                {
                    new FeedRangeState<ChangeFeedState>(feedRangeInternal, ChangeFeedState.Continuation(continuation))
                }.ToImmutableArray());
        }
    }
}
