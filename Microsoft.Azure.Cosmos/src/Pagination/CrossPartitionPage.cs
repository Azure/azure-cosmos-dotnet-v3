// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    internal sealed class CrossPartitionPage<TBackendPage, TBackendState> : Page<CrossPartitionState<TBackendState>>
        where TBackendPage : Page<TBackendState>
        where TBackendState : State
    {
        public CrossPartitionPage(TBackendPage backendEndPage, CrossPartitionState<TBackendState> state)
            : base(state)
        {
            this.Page = backendEndPage;
        }

        public TBackendPage Page { get; }
    }
}
