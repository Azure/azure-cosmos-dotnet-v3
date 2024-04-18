// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;

    internal sealed class ChangeFeedSuccessPage : ChangeFeedPage
    {
        private static readonly ImmutableHashSet<string> bannedHeaders = new HashSet<string>().ToImmutableHashSet();

        public ChangeFeedSuccessPage(
            Stream content,
            double requestCharge,
            int itemCount,
            string activityId,
            IReadOnlyDictionary<string, string> additionalHeaders,
            ChangeFeedState state)
            : base(requestCharge, activityId, additionalHeaders, state)
        {
            this.Content = content ?? throw new ArgumentNullException(nameof(content));
            this.ItemCount = itemCount;
        }

        public Stream Content { get; }

        public override int ItemCount { get; }

        protected override ImmutableHashSet<string> DerivedClassBannedHeaders => bannedHeaders;
    }
}
