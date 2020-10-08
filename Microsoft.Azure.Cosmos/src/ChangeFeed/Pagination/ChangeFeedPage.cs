// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.IO;
    using Microsoft.Azure.Cosmos.Pagination;

    internal sealed class ChangeFeedPage : Page<ChangeFeedState>
    {
        public ChangeFeedPage(
            bool contentWasModified,
            Stream content,
            double requestCharge,
            string activityId,
            ChangeFeedState state)
            : base(state)
        {
            this.ContentWasModified = contentWasModified;
            this.Content = content ?? throw new ArgumentNullException(nameof(content));
            this.RequestCharge = requestCharge < 0 ? throw new ArgumentOutOfRangeException(nameof(requestCharge)) : requestCharge;
            this.ActivityId = activityId ?? throw new ArgumentNullException(nameof(content));
        }

        public bool ContentWasModified { get; }

        public Stream Content { get; }

        public double RequestCharge { get; }

        public string ActivityId { get; }
    }
}
