// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal static class ReadFeedFeedRangeStateSerializer
    {
        private static class PropertyNames
        {
            public const string FeedRange = "FeedRange";
            public const string State = "State";
        }

        public static CosmosElement ToCosmosElement(FeedRangeState<ReadFeedState> feedRangeState)
        {
            Dictionary<string, CosmosElement> dictionary = new Dictionary<string, CosmosElement>()
            {
                {
                    PropertyNames.FeedRange,
                    FeedRangeCosmosElementSerializer.ToCosmosElement(feedRangeState.FeedRange)
                }
            };

            if (feedRangeState.State is ReadFeedBeginningState)
            {
                dictionary[PropertyNames.State] = CosmosNull.Create();
            }
            else if (feedRangeState.State is ReadFeedContinuationState readFeedContinuationState)
            {
                dictionary[PropertyNames.State] = readFeedContinuationState.ContinuationToken;
            }
            else
            {
                throw new InvalidOperationException("Unknown FeedRange State.");
            }

            return CosmosObject.Create(dictionary);
        }
        
        public static class Monadic
        {
            public static TryCatch<FeedRangeState<ReadFeedState>> CreateFromCosmosElement(CosmosElement cosmosElement)
            {
                if (cosmosElement == null)
                {
                    throw new ArgumentNullException(nameof(cosmosElement));
                }

                if (!(cosmosElement is CosmosObject cosmosObject))
                {
                    return TryCatch<FeedRangeState<ReadFeedState>>.FromException(
                        new FormatException(
                            $"Expected object for ChangeFeed Continuation: {cosmosElement}."));
                }

                if (!cosmosObject.TryGetValue(PropertyNames.FeedRange, out CosmosElement feedRangeCosmosElement))
                {
                    return TryCatch<FeedRangeState<ReadFeedState>>.FromException(
                        new FormatException(
                            $"Expected '{PropertyNames.FeedRange}' for '{nameof(FeedRangeState<ReadFeedState>)}': {cosmosElement}."));
                }

                if (!cosmosObject.TryGetValue(PropertyNames.State, out CosmosElement stateCosmosElement))
                {
                    return TryCatch<FeedRangeState<ReadFeedState>>.FromException(
                        new FormatException(
                            $"Expected '{PropertyNames.State}' for '{nameof(FeedRangeState<ReadFeedState>)}': {cosmosElement}."));
                }

                TryCatch<FeedRangeInternal> monadicFeedRange = FeedRangeCosmosElementSerializer.MonadicCreateFromCosmosElement(feedRangeCosmosElement);
                if (monadicFeedRange.Failed)
                {
                    return TryCatch<FeedRangeState<ReadFeedState>>.FromException(
                        new FormatException(
                            $"Failed to parse '{PropertyNames.FeedRange}' for '{nameof(FeedRangeState<ReadFeedState>)}': {cosmosElement}.",
                            innerException: monadicFeedRange.Exception));
                }

                ReadFeedState readFeedState;
                if (stateCosmosElement is CosmosNull)
                {
                    readFeedState = ReadFeedState.Beginning();
                }
                else
                {
                    readFeedState = ReadFeedState.Continuation(stateCosmosElement);
                }

                return TryCatch<FeedRangeState<ReadFeedState>>.FromResult(
                    new FeedRangeState<ReadFeedState>(
                        monadicFeedRange.Result,
                        readFeedState));
            }
        }
    }
}
