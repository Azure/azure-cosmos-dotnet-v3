// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.IO;

    internal sealed class ChangeFeedSuccessPage : ChangeFeedPage
    {
        public ChangeFeedSuccessPage(
            Stream content,
            double requestCharge,
            string activityId,
            ChangeFeedState state)
            : base(requestCharge, activityId, state)
        {
            this.Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public Stream Content { get; }
    }
}
