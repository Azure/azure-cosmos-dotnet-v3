// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Collections.Generic;
    using System.Collections.Immutable;

    internal sealed class CrossFeedRangePage<TBackendPage, TBackendState> : Page<CrossFeedRangeState<TBackendState>>
        where TBackendPage : Page<TBackendState>
        where TBackendState : State
    {
        private static readonly ImmutableHashSet<string> bannedHeaders = new HashSet<string>().ToImmutableHashSet();

        public CrossFeedRangePage(TBackendPage backendEndPage, CrossFeedRangeState<TBackendState> state)
            : base(backendEndPage.RequestCharge, backendEndPage.ActivityId, backendEndPage.AdditionalHeaders, state)
        {
            this.Page = backendEndPage;
        }

        public TBackendPage Page { get; }

        protected override ImmutableHashSet<string> DerivedClassBannedHeaders => bannedHeaders;
    }
}
