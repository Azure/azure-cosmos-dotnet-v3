// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System;
    using System.IO;
    using Microsoft.Azure.Cosmos.Pagination;

    internal sealed class ReadFeedPage : Page<ReadFeedState>
    {
        public ReadFeedPage(
            Stream content,
            double requestCharge,
            string activityId,
            CosmosDiagnosticsContext diagnostics,
            ReadFeedState state)
            : base(state)
        {
            this.Content = content ?? throw new ArgumentNullException(nameof(content));
            this.RequestCharge = requestCharge < 0 ? throw new ArgumentOutOfRangeException(nameof(requestCharge)) : requestCharge;
            this.Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            this.ActivityId = activityId;
        }

        public Stream Content { get; }

        public double RequestCharge { get; }

        public string ActivityId { get; }

        public CosmosDiagnosticsContext Diagnostics { get; }
    }
}
