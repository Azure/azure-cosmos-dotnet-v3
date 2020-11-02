// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal static class ChangeFeedFeedRangeStateSerializer
    {
        private static class PropertyNames
        {
            public const string FeedRange = "FeedRange";
            public const string State = "State";
        }

        public static CosmosElement ToCosmosElement(FeedRangeState<ChangeFeedState> feedRangeState)
        {
            return CosmosObject.Create(new Dictionary<string, CosmosElement>()
            {
                {
                    PropertyNames.FeedRange,
                    FeedRangeCosmosElementSerializer.ToCosmosElement(feedRangeState.FeedRange)
                },
                {
                    PropertyNames.State,
                    ChangeFeedStateCosmosElementSerializer.ToCosmosElement(feedRangeState.State)
                }
            });
        }

        public static class Monadic
        {
            public static TryCatch<FeedRangeState<ChangeFeedState>> CreateFromCosmosElement(CosmosElement cosmosElement)
            {
                if (cosmosElement == null)
                {
                    throw new ArgumentNullException(nameof(cosmosElement));
                }

                if (!(cosmosElement is CosmosObject cosmosObject))
                {
                    return TryCatch<FeedRangeState<ChangeFeedState>>.FromException(
                        new FormatException(
                            $"Expected object for ChangeFeed Continuation: {cosmosElement}."));
                }

                if (!cosmosObject.TryGetValue(PropertyNames.FeedRange, out CosmosElement feedRangeCosmosElement))
                {
                    return TryCatch<FeedRangeState<ChangeFeedState>>.FromException(
                        new FormatException(
                            $"Expected '{PropertyNames.FeedRange}' for '{nameof(ChangeFeedFeedRangeStateSerializer)}': {cosmosElement}."));
                }

                if (!cosmosObject.TryGetValue(PropertyNames.State, out CosmosElement stateCosmosElement))
                {
                    return TryCatch<FeedRangeState<ChangeFeedState>>.FromException(
                        new FormatException(
                            $"Expected '{PropertyNames.State}' for '{nameof(ChangeFeedFeedRangeStateSerializer)}': {cosmosElement}."));
                }

                TryCatch<FeedRangeInternal> monadicFeedRange = FeedRangeCosmosElementSerializer.MonadicCreateFromCosmosElement(feedRangeCosmosElement);
                if (monadicFeedRange.Failed)
                {
                    return TryCatch<FeedRangeState<ChangeFeedState>>.FromException(
                        new FormatException(
                            $"Failed to parse '{PropertyNames.FeedRange}' for '{nameof(ChangeFeedFeedRangeStateSerializer)}': {cosmosElement}.",
                            innerException: monadicFeedRange.Exception));
                }

                TryCatch<ChangeFeedState> monadicChangeFeedState = ChangeFeedStateCosmosElementSerializer.MonadicFromCosmosElement(stateCosmosElement);
                if (monadicChangeFeedState.Failed)
                {
                    return TryCatch<FeedRangeState<ChangeFeedState>>.FromException(
                        new FormatException(
                            $"Failed to parse '{PropertyNames.State}' for '{nameof(ChangeFeedFeedRangeStateSerializer)}': {cosmosElement}.",
                            innerException: monadicChangeFeedState.Exception));
                }

                return TryCatch<FeedRangeState<ChangeFeedState>>.FromResult(
                    new FeedRangeState<ChangeFeedState>(
                        monadicFeedRange.Result,
                        monadicChangeFeedState.Result));
            }
        }
    }
}
