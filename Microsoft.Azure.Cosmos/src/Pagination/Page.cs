// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    internal abstract class Page
    {
        protected Page(State state)
        {
            this.State = state;
        }

        public State State { get; }
    }
}
