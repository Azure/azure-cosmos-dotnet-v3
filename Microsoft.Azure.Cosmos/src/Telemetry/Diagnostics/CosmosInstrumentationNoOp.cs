// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using Microsoft.Azure.Cosmos.Tracing;

    internal class CosmosInstrumentationNoOp : ICosmosInstrumentation
    {
        public void Dispose()
        {
            // NoOp
        }

        public void MarkFailed(Exception exception)
        {
            // NoOp
        }

        public void Record(string attributeKey, object attributeValue)
        {
            // NoOp
        }
    }
}
