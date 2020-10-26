// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    internal sealed class ChangeFeedNotModifiedPage : ChangeFeedPage
    {
        public ChangeFeedNotModifiedPage(
            double requestCharge,
            string activityId,
            ChangeFeedState state)
            : base(requestCharge, activityId, state)
        {
        }
    }
}
