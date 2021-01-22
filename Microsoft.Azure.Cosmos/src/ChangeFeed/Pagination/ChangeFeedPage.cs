// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Pagination;

    internal abstract class ChangeFeedPage : Page<ChangeFeedState>
    {
        protected ChangeFeedPage(
            double requestCharge,
            string activityId,
            IReadOnlyDictionary<string, string> additionalHeaders,
            ChangeFeedState state)
            : base(requestCharge, activityId, additionalHeaders, state)
        {
        }
    }
}
