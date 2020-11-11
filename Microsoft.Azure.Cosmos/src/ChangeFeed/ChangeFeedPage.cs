// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class ChangeFeedPage
    {
        private ChangeFeedPage(
            CosmosArray documents,
            bool notModified,
            double requestCharge,
            string activityId,
            ChangeFeedCrossFeedRangeState state)
        {
            this.Documents = documents ?? throw new ArgumentOutOfRangeException(nameof(documents));
            this.NotModified = notModified;
            this.RequestCharge = requestCharge < 0 ? throw new ArgumentOutOfRangeException(nameof(requestCharge)) : requestCharge;
            this.ActivityId = activityId ?? throw new ArgumentNullException(nameof(activityId));
            this.State = state;
        }

        public CosmosArray Documents { get; }

        public bool NotModified { get; }

        public double RequestCharge { get; }

        public string ActivityId { get; }

        public ChangeFeedCrossFeedRangeState State { get; }

        public static ChangeFeedPage CreateNotModifiedPage(double requestCharge, string activityId, ChangeFeedCrossFeedRangeState state)
        {
            return new ChangeFeedPage(CosmosArray.Empty, notModified: true, requestCharge, activityId, state);
        }

        public static ChangeFeedPage CreatePageWithChanges(CosmosArray documents, double requestCharge, string activityId, ChangeFeedCrossFeedRangeState state)
        {
            return new ChangeFeedPage(documents, notModified: false, requestCharge, activityId, state);
        }
    }
}
