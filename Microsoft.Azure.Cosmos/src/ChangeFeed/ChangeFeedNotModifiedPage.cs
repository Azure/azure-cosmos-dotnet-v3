// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;

    internal sealed class ChangeFeedNotModifiedPage : ChangeFeedPage
    {
        public ChangeFeedNotModifiedPage(
            double requestCharge,
            string activityId,
            ChangeFeedCrossFeedRangeState state)
            : base(requestCharge, activityId, state)
        {
        }
    }
}
