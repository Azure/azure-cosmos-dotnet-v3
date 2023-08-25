// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    internal sealed class StartedState : State
    {
        public static readonly StartedState Singleton = new StartedState();

        private StartedState()
        {
        }
    }
}
