// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    internal sealed class CrossFeedRangePage<TBackendPage, TBackendState> : Page<CrossFeedRangeState<TBackendState>>
        where TBackendPage : Page<TBackendState>
        where TBackendState : State
    {
        public CrossFeedRangePage(TBackendPage backendEndPage, CrossFeedRangeState<TBackendState> state)
            : base(state)
        {
            this.Page = backendEndPage;
        }

        public TBackendPage Page { get; }
    }
}
