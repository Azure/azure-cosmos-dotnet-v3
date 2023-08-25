// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif 
        readonly struct ChangeFeedCrossFeedRangeState
    {
        // TODO: this class be auto generated. 

        private static class FullRangeStatesSingletons
        {
            public static readonly ChangeFeedCrossFeedRangeState Beginning = new ChangeFeedCrossFeedRangeState(
                new List<FeedRangeState<ChangeFeedState>>()
                {
                    new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Beginning())
                });

            public static readonly ChangeFeedCrossFeedRangeState Now = new ChangeFeedCrossFeedRangeState(
                new List<FeedRangeState<ChangeFeedState>>()
                {
                    new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Now())
                });
        }

        public ChangeFeedCrossFeedRangeState(IReadOnlyList<FeedRangeState<ChangeFeedState>> feedRangeStates)
            : this(feedRangeStates.ToArray().AsMemory())
        {
            // This constructor is just to provide the user with an easy interface
            // We make a copy of the array, since we don't want user side effects
            // And call on the private constructor.
            // All the split and merge methods will call that constructor to avoid additional allocations.
        }

        internal ChangeFeedCrossFeedRangeState(ReadOnlyMemory<FeedRangeState<ChangeFeedState>> feedRangeStates)
        {
            if (feedRangeStates.IsEmpty)
            {
                throw new ArgumentException($"Expected {nameof(feedRangeStates)} to be non empty.");
            }

            this.FeedRangeStates = feedRangeStates;
        }

        internal ReadOnlyMemory<FeedRangeState<ChangeFeedState>> FeedRangeStates { get; }

        public ChangeFeedCrossFeedRangeState Merge(ChangeFeedCrossFeedRangeState first)
        {
            Memory<FeedRangeState<ChangeFeedState>> mergedRange = CrossFeedRangeStateSplitterAndMerger.Merge<ChangeFeedState>(
                this.FeedRangeStates,
                first.FeedRangeStates);

            return new ChangeFeedCrossFeedRangeState(mergedRange);
        }

        public ChangeFeedCrossFeedRangeState Merge(ChangeFeedCrossFeedRangeState first, ChangeFeedCrossFeedRangeState second)
        {
            Memory<FeedRangeState<ChangeFeedState>> mergedRange = CrossFeedRangeStateSplitterAndMerger.Merge<ChangeFeedState>(
                this.FeedRangeStates,
                first.FeedRangeStates,
                second.FeedRangeStates);

            return new ChangeFeedCrossFeedRangeState(mergedRange);
        }

        // TODO: Add more varadic merge methods.

        public ChangeFeedCrossFeedRangeState Merge(params ChangeFeedCrossFeedRangeState[] changeFeedCrossFeedRangeStates)
        {
            return this.Merge(changeFeedCrossFeedRangeStates.ToList());
        }

        public ChangeFeedCrossFeedRangeState Merge(IReadOnlyList<ChangeFeedCrossFeedRangeState> changeFeedCrossFeedRangeStates)
        {
            List<ReadOnlyMemory<FeedRangeState<ChangeFeedState>>> varArgs = new List<ReadOnlyMemory<FeedRangeState<ChangeFeedState>>>(1 + changeFeedCrossFeedRangeStates.Count)
            {
                this.FeedRangeStates
            };

            foreach (ChangeFeedCrossFeedRangeState changeFeedCrossFeedRangeState in changeFeedCrossFeedRangeStates)
            {
                varArgs.Add(changeFeedCrossFeedRangeState.FeedRangeStates);
            }

            Memory<FeedRangeState<ChangeFeedState>> mergedRange = CrossFeedRangeStateSplitterAndMerger.Merge<ChangeFeedState>(varArgs);

            return new ChangeFeedCrossFeedRangeState(mergedRange);
        }

        public bool TrySplit(out ChangeFeedCrossFeedRangeState first, out ChangeFeedCrossFeedRangeState second)
        {
            if (!CrossFeedRangeStateSplitterAndMerger.TrySplit(
                this.FeedRangeStates,
                out ReadOnlyMemory<FeedRangeState<ChangeFeedState>> firstRange,
                out ReadOnlyMemory<FeedRangeState<ChangeFeedState>> secondRange))
            {
                first = default;
                second = default;
                return false;
            }

            first = new ChangeFeedCrossFeedRangeState(firstRange);
            second = new ChangeFeedCrossFeedRangeState(secondRange);
            return true;
        }

        public bool TrySplit(
            out ChangeFeedCrossFeedRangeState first, 
            out ChangeFeedCrossFeedRangeState second, 
            out ChangeFeedCrossFeedRangeState third)
        {
            if (!CrossFeedRangeStateSplitterAndMerger.TrySplit(
                this.FeedRangeStates,
                out ReadOnlyMemory<FeedRangeState<ChangeFeedState>> firstRange,
                out ReadOnlyMemory<FeedRangeState<ChangeFeedState>> secondRange,
                out ReadOnlyMemory<FeedRangeState<ChangeFeedState>> thirdRange))
            {
                first = default;
                second = default;
                third = default;
                return false;
            }

            first = new ChangeFeedCrossFeedRangeState(firstRange);
            second = new ChangeFeedCrossFeedRangeState(secondRange);
            third = new ChangeFeedCrossFeedRangeState(thirdRange);
            return true;
        }

        // TODO: Add more varadic split methods.

        public bool TrySplit(
            int numberOfPartitions,
            out List<ChangeFeedCrossFeedRangeState> partitions)
        {
            if (!CrossFeedRangeStateSplitterAndMerger.TrySplit(
                this.FeedRangeStates,
                numberOfPartitions,
                out List<ReadOnlyMemory<FeedRangeState<ChangeFeedState>>> partitionsAfterSplit))
            {
                partitions = default;
                return false;
            }

            partitions = new List<ChangeFeedCrossFeedRangeState>(partitionsAfterSplit.Count);
            foreach (ReadOnlyMemory<FeedRangeState<ChangeFeedState>> partition in partitionsAfterSplit)
            {
                partitions.Add(new ChangeFeedCrossFeedRangeState(partition));
            }

            return true;
        }

        public CosmosElement ToCosmosElement()
        {
            List<CosmosElement> elements = new List<CosmosElement>();
            foreach (FeedRangeState<ChangeFeedState> changeFeedFeedRangeState in this.FeedRangeStates.Span)
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

                List<FeedRangeState<ChangeFeedState>> changeFeedFeedRangeStates = new List<FeedRangeState<ChangeFeedState>>(capacity: cosmosArray.Count);
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
                });
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
                });
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
                });
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
                });
        }
    }
}
