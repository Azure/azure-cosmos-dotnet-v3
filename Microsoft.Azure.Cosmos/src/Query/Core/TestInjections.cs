// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    internal sealed class TestInjections
    {
        public TestInjections(bool simulate429s, bool simulateEmptyPages)
        {
            this.SimulateThrottles = simulate429s;
            this.SimulateEmptyPages = simulateEmptyPages;
        }

        public bool SimulateThrottles { get; }

        public bool SimulateEmptyPages { get; }
    }
}
