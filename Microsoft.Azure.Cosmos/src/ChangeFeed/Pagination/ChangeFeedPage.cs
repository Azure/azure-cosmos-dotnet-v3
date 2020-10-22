// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using Microsoft.Azure.Cosmos.Pagination;

    internal abstract class ChangeFeedPage : Page<ChangeFeedState>
    {
        protected ChangeFeedPage(
            double requestCharge,
            string activityId,
            ChangeFeedState state)
            : base(state)
        {
            this.RequestCharge = requestCharge < 0 ? throw new ArgumentOutOfRangeException(nameof(requestCharge)) : requestCharge;
            this.ActivityId = activityId ?? throw new ArgumentNullException(nameof(activityId));
        }

        public double RequestCharge { get; }

        public string ActivityId { get; }
    }
}
