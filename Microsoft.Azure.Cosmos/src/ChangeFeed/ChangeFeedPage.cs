// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;

    internal abstract class ChangeFeedPage
    {
        protected ChangeFeedPage(
            double requestCharge,
            string activityId,
            ChangeFeedCrossFeedRangeState state)
        {
            this.RequestCharge = requestCharge < 0 ? throw new ArgumentOutOfRangeException(nameof(requestCharge)) : requestCharge;
            this.ActivityId = activityId ?? throw new ArgumentNullException(nameof(activityId));
            this.State = state;
        }

        public double RequestCharge { get; }

        public string ActivityId { get; }

        public ChangeFeedCrossFeedRangeState State { get; }
    }
}
