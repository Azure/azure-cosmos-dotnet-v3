// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    internal abstract class Page<TState>
        where TState : State
    {
        protected Page(TState state)
        {
            this.State = state;
        }

        public TState State { get; }
    }
}
