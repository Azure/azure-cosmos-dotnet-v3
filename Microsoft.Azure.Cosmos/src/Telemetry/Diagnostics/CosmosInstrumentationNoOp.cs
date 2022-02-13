// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;

    internal class CosmosInstrumentationNoOp : ICosmosInstrumentation
    {
        public void MarkFailed(Exception ex)
        {
            // NoOp
        }

        public void AddAttribute(string key, object value)
        {
            // NoOp
        }

        public void Dispose()
        {
            // NoOp
        }
    }
}
