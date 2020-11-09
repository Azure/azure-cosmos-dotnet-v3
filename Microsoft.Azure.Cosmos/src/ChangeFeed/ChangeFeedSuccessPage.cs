// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class ChangeFeedSuccessPage : ChangeFeedPage
    {
        public ChangeFeedSuccessPage(
            CosmosArray documents,
            double requestCharge,
            string activityId,
            ChangeFeedCrossFeedRangeState state)
            : base(requestCharge, activityId, state)
        {
            this.Documents = documents ?? throw new ArgumentNullException(nameof(documents));
        }

        public CosmosArray Documents { get; }
    }
}
