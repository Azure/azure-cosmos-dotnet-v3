// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Documents;

    internal abstract class ChangeFeedPage : Page<ChangeFeedState>
    {
        public static readonly ImmutableHashSet<string> BannedHeaders = new HashSet<string>()
        {
            HttpConstants.HttpHeaders.ETag,
        }
        .Concat(Page<ChangeFeedState>.BannedHeadersBase)
        .ToImmutableHashSet();

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
