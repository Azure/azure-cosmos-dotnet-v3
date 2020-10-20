// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

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
                    readFeedContinuationToken.State.ContinuationToken
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
                monadicReadFeedState = TryCatch<ReadFeedState>.FromResult(new ReadFeedState(cosmosString));
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
}
