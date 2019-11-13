// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    internal sealed class TestSettings
    {
        public TestSettings(bool simulate429s)
        {
            this.Simulate429s = simulate429s;
        }

        public bool Simulate429s { get; }
    }
}
