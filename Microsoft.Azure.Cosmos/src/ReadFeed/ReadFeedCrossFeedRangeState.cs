// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;

    internal readonly struct ReadFeedCrossFeedRangeState
    {
        // TODO: this class can be auto generated. 

        private static class FullRangeStatesSingletons
        {
            public static readonly ReadFeedCrossFeedRangeState Beginning = new ReadFeedCrossFeedRangeState(
                new List<FeedRangeState<ReadFeedState>>()
                {
                    new FeedRangeState<ReadFeedState>(FeedRangeEpk.FullRange, ReadFeedState.Beginning())
                });
        }

        public ReadFeedCrossFeedRangeState(IReadOnlyList<FeedRangeState<ReadFeedState>> feedRangeStates)
            : this(feedRangeStates.ToArray().AsMemory())
        {
            // This constructor is just to provide the user with an easy interface
            // We make a copy of the array, since we don't want user side effects
            // And call on the private constructor.
            // All the split and merge methods will call that constructor to avoid additional allocations.
        }

        internal ReadFeedCrossFeedRangeState(ReadOnlyMemory<FeedRangeState<ReadFeedState>> feedRangeStates)
        {
            if (feedRangeStates.IsEmpty)
            {
                throw new ArgumentException($"Expected {nameof(feedRangeStates)} to be non empty.");
            }

            this.FeedRangeStates = feedRangeStates;
        }

        internal ReadOnlyMemory<FeedRangeState<ReadFeedState>> FeedRangeStates { get; }

        public ReadFeedCrossFeedRangeState Merge(ReadFeedCrossFeedRangeState first)
        {
            Memory<FeedRangeState<ReadFeedState>> mergedRange = CrossFeedRangeStateSplitterAndMerger.Merge<ReadFeedState>(
                this.FeedRangeStates,
                first.FeedRangeStates);

            return new ReadFeedCrossFeedRangeState(mergedRange);
        }

        public ReadFeedCrossFeedRangeState Merge(ReadFeedCrossFeedRangeState first, ReadFeedCrossFeedRangeState second)
        {
            Memory<FeedRangeState<ReadFeedState>> mergedRange = CrossFeedRangeStateSplitterAndMerger.Merge<ReadFeedState>(
                this.FeedRangeStates,
                first.FeedRangeStates,
                second.FeedRangeStates);

            return new ReadFeedCrossFeedRangeState(mergedRange);
        }

        // TODO: Add more varadic merge methods.

        public ReadFeedCrossFeedRangeState Merge(params ReadFeedCrossFeedRangeState[] readFeedCrossFeedRangeStates)
        {
            return this.Merge(readFeedCrossFeedRangeStates.ToList());
        }

        public ReadFeedCrossFeedRangeState Merge(IReadOnlyList<ReadFeedCrossFeedRangeState> readFeedCrossFeedRangeStates)
        {
            List<ReadOnlyMemory<FeedRangeState<ReadFeedState>>> varArgs = new List<ReadOnlyMemory<FeedRangeState<ReadFeedState>>>(1 + readFeedCrossFeedRangeStates.Count)
            {
                this.FeedRangeStates
            };

            foreach (ReadFeedCrossFeedRangeState readFeedCrossFeedRangeState in readFeedCrossFeedRangeStates)
            {
                varArgs.Add(readFeedCrossFeedRangeState.FeedRangeStates);
            }

            Memory<FeedRangeState<ReadFeedState>> mergedRange = CrossFeedRangeStateSplitterAndMerger.Merge<ReadFeedState>(varArgs);

            return new ReadFeedCrossFeedRangeState(mergedRange);
        }

        public bool TrySplit(out ReadFeedCrossFeedRangeState first, out ReadFeedCrossFeedRangeState second)
        {
            if (!CrossFeedRangeStateSplitterAndMerger.TrySplit(
                this.FeedRangeStates,
                out ReadOnlyMemory<FeedRangeState<ReadFeedState>> firstRange,
                out ReadOnlyMemory<FeedRangeState<ReadFeedState>> secondRange))
            {
                first = default;
                second = default;
                return false;
            }

            first = new ReadFeedCrossFeedRangeState(firstRange);
            second = new ReadFeedCrossFeedRangeState(secondRange);
            return true;
        }

        public bool TrySplit(
            out ReadFeedCrossFeedRangeState first, 
            out ReadFeedCrossFeedRangeState second, 
            out ReadFeedCrossFeedRangeState third)
        {
            if (!CrossFeedRangeStateSplitterAndMerger.TrySplit(
                this.FeedRangeStates,
                out ReadOnlyMemory<FeedRangeState<ReadFeedState>> firstRange,
                out ReadOnlyMemory<FeedRangeState<ReadFeedState>> secondRange,
                out ReadOnlyMemory<FeedRangeState<ReadFeedState>> thirdRange))
            {
                first = default;
                second = default;
                third = default;
                return false;
            }

            first = new ReadFeedCrossFeedRangeState(firstRange);
            second = new ReadFeedCrossFeedRangeState(secondRange);
            third = new ReadFeedCrossFeedRangeState(thirdRange);
            return true;
        }

        // TODO: Add more varadic split methods.

        public bool TrySplit(
            int numberOfPartitions,
            out List<ReadFeedCrossFeedRangeState> partitions)
        {
            if (!CrossFeedRangeStateSplitterAndMerger.TrySplit(
                this.FeedRangeStates,
                numberOfPartitions,
                out List<ReadOnlyMemory<FeedRangeState<ReadFeedState>>> partitionsAfterSplit))
            {
                partitions = default;
                return false;
            }

            partitions = new List<ReadFeedCrossFeedRangeState>(partitionsAfterSplit.Count);
            foreach (ReadOnlyMemory<FeedRangeState<ReadFeedState>> partition in partitionsAfterSplit)
            {
                partitions.Add(new ReadFeedCrossFeedRangeState(partition));
            }

            return true;
        }

        public CosmosElement ToCosmosElement()
        {
            List<CosmosElement> elements = new List<CosmosElement>();
            foreach (FeedRangeState<ReadFeedState> readFeedFeedRangeState in this.FeedRangeStates.Span)
            {
                elements.Add(ReadFeedFeedRangeStateSerializer.ToCosmosElement(readFeedFeedRangeState));
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

        public static ReadFeedCrossFeedRangeState Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            TryCatch<ReadFeedCrossFeedRangeState> monadicParse = Monadic.Parse(text);
            monadicParse.ThrowIfFailed();

            return monadicParse.Result;
        }

        public static bool TryParse(string text, out ReadFeedCrossFeedRangeState state)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            TryCatch<ReadFeedCrossFeedRangeState> monadicParse = Monadic.Parse(text);
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
            public static TryCatch<ReadFeedCrossFeedRangeState> Parse(string text)
            {
                if (text == null)
                {
                    throw new ArgumentNullException(nameof(text));
                }

                TryCatch<CosmosElement> monadicCosmosElement = CosmosElement.Monadic.Parse(text);
                if (monadicCosmosElement.Failed)
                {
                    return TryCatch<ReadFeedCrossFeedRangeState>.FromException(monadicCosmosElement.Exception);
                }

                return CreateFromCosmosElement(monadicCosmosElement.Result);
            }

            internal static TryCatch<ReadFeedCrossFeedRangeState> CreateFromCosmosElement(CosmosElement cosmosElement)
            {
                if (cosmosElement == null)
                {
                    throw new ArgumentNullException(nameof(cosmosElement));
                }

                if (!(cosmosElement is CosmosArray cosmosArray))
                {
                    return TryCatch<ReadFeedCrossFeedRangeState>.FromException(
                        new FormatException(
                            $"Expected array: {cosmosElement}"));
                }

                FeedRangeState<ReadFeedState>[] feedRangeStates = new FeedRangeState<ReadFeedState>[cosmosArray.Count];
                int i = 0;
                foreach (CosmosElement arrayItem in cosmosArray)
                {
                    TryCatch<FeedRangeState<ReadFeedState>> monadicFeedRangeState = ReadFeedFeedRangeStateSerializer.Monadic.CreateFromCosmosElement(arrayItem);
                    if (monadicFeedRangeState.Failed)
                    {
                        return TryCatch<ReadFeedCrossFeedRangeState>.FromException(monadicFeedRangeState.Exception);
                    }

                    feedRangeStates[i++] = monadicFeedRangeState.Result;
                }

                ReadFeedCrossFeedRangeState crossFeedRangeState = new ReadFeedCrossFeedRangeState(feedRangeStates.AsMemory());
                return TryCatch<ReadFeedCrossFeedRangeState>.FromResult(crossFeedRangeState);
            }
        }

        public static ReadFeedCrossFeedRangeState CreateFromBeginning()
        {
            return CreateFromBeginning(FeedRangeEpk.FullRange);
        }

        public static ReadFeedCrossFeedRangeState CreateFromBeginning(FeedRange feedRange)
        {
            if (!(feedRange is FeedRangeInternal feedRangeInternal))
            {
                throw new ArgumentException($"{nameof(feedRange)} needs to be a {nameof(FeedRangeInternal)}.");
            }

            if (feedRange.Equals(FeedRangeEpk.FullRange))
            {
                return FullRangeStatesSingletons.Beginning;
            }

            return new ReadFeedCrossFeedRangeState(
                new List<FeedRangeState<ReadFeedState>>()
                {
                    new FeedRangeState<ReadFeedState>(feedRangeInternal, ReadFeedState.Beginning())
                });
        }

        public static ReadFeedCrossFeedRangeState CreateFromContinuation(CosmosElement continuation)
        {
            return CreateFromContinuation(continuation, FeedRangeEpk.FullRange);
        }

        public static ReadFeedCrossFeedRangeState CreateFromContinuation(CosmosElement continuation, FeedRange feedRange)
        {
            if (!(feedRange is FeedRangeInternal feedRangeInternal))
            {
                throw new ArgumentException($"{nameof(feedRange)} needs to be a {nameof(FeedRangeInternal)}.");
            }

            return new ReadFeedCrossFeedRangeState(
                new List<FeedRangeState<ReadFeedState>>()
                {
                    new FeedRangeState<ReadFeedState>(feedRangeInternal, ReadFeedState.Continuation(continuation))
                });
        }
    }
}
