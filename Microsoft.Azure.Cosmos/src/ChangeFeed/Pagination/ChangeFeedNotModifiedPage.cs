// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System.Collections.Generic;
    using System.Collections.Immutable;

    internal sealed class ChangeFeedNotModifiedPage : ChangeFeedPage
    {
        private static readonly ImmutableHashSet<string> bannedHeaders = new HashSet<string>().ToImmutableHashSet();

        public ChangeFeedNotModifiedPage(
            double requestCharge,
            string activityId,
            IReadOnlyDictionary<string, string> additionalHeaders,
            ChangeFeedState state)
            : base(requestCharge, activityId, additionalHeaders, state)
        {
        }

        public override int ItemCount => 0;

        protected override ImmutableHashSet<string> DerivedClassBannedHeaders => bannedHeaders;
    }
}
