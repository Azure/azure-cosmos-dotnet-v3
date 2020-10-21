// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal readonly struct ChangeFeedContinuationToken
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
}
