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

    internal sealed class ChangeFeedFeedRangeState : FeedRangeState<ChangeFeedState>
    {
        public ChangeFeedFeedRangeState(
            FeedRangeInternal feedRange,
            ChangeFeedState changeFeedState)
            : base(feedRange, changeFeedState)
        {
        }

        public CosmosElement ToCosmosElement()
        {
            return CosmosObject.Create(new Dictionary<string, CosmosElement>()
            {
                {
                    PropertyNames.FeedRange,
                    FeedRangeCosmosElementSerializer.ToCosmosElement(this.FeedRange)
                },
                {
                    PropertyNames.State,
                    ChangeFeedStateCosmosElementSerializer.ToCosmosElement(this.State)
                }
            });
        }

        public static class Monadic
        {
            public static TryCatch<ChangeFeedFeedRangeState> CreateFromCosmosElement(CosmosElement cosmosElement)
            {
                if (cosmosElement == null)
                {
                    throw new ArgumentNullException(nameof(cosmosElement));
                }

                if (!(cosmosElement is CosmosObject cosmosObject))
                {
                    return TryCatch<ChangeFeedFeedRangeState>.FromException(
                        new FormatException(
                            $"Expected object for ChangeFeed Continuation: {cosmosElement}."));
                }

                if (!cosmosObject.TryGetValue(PropertyNames.FeedRange, out CosmosElement feedRangeCosmosElement))
                {
                    return TryCatch<ChangeFeedFeedRangeState>.FromException(
                        new FormatException(
                            $"Expected '{PropertyNames.FeedRange}' for '{nameof(ChangeFeedFeedRangeState)}': {cosmosElement}."));
                }

                if (!cosmosObject.TryGetValue(PropertyNames.State, out CosmosElement stateCosmosElement))
                {
                    return TryCatch<ChangeFeedFeedRangeState>.FromException(
                        new FormatException(
                            $"Expected '{PropertyNames.State}' for '{nameof(ChangeFeedFeedRangeState)}': {cosmosElement}."));
                }

                TryCatch<FeedRangeInternal> monadicFeedRange = FeedRangeCosmosElementSerializer.MonadicCreateFromCosmosElement(feedRangeCosmosElement);
                if (monadicFeedRange.Failed)
                {
                    return TryCatch<ChangeFeedFeedRangeState>.FromException(
                        new FormatException(
                            $"Failed to parse '{PropertyNames.FeedRange}' for '{nameof(ChangeFeedFeedRangeState)}': {cosmosElement}.",
                            innerException: monadicFeedRange.Exception));
                }

                TryCatch<ChangeFeedState> monadicChangeFeedState = ChangeFeedStateCosmosElementSerializer.MonadicFromCosmosElement(stateCosmosElement);
                if (monadicChangeFeedState.Failed)
                {
                    return TryCatch<ChangeFeedFeedRangeState>.FromException(
                        new FormatException(
                            $"Failed to parse '{PropertyNames.State}' for '{nameof(ChangeFeedFeedRangeState)}': {cosmosElement}.",
                            innerException: monadicChangeFeedState.Exception));
                }

                return TryCatch<ChangeFeedFeedRangeState>.FromResult(
                    new ChangeFeedFeedRangeState(
                        monadicFeedRange.Result,
                        monadicChangeFeedState.Result));
            }
        }
    }
}
