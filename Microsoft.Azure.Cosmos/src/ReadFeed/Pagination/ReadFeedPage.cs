// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Documents;

    internal sealed class ReadFeedPage : Page<ReadFeedState>
    {
        public static readonly ImmutableHashSet<string> BannedHeaders = new HashSet<string>()
        {
            HttpConstants.HttpHeaders.Continuation,
            HttpConstants.HttpHeaders.ContinuationToken,
        }.Concat(BannedHeadersBase).ToImmutableHashSet();

        public ReadFeedPage(
            Stream content,
            double requestCharge,
            string activityId,
            CosmosDiagnosticsContext diagnostics,
            IReadOnlyDictionary<string, string> additionalHeaders,
            ReadFeedState state)
            : base(requestCharge, activityId, additionalHeaders, state)
        {
            this.Content = content ?? throw new ArgumentNullException(nameof(content));
            this.Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public Stream Content { get; }

        public CosmosDiagnosticsContext Diagnostics { get; }

        protected override ImmutableHashSet<string> DerivedClassBannedHeaders => ReadFeedPage.BannedHeaders;
    }
}
