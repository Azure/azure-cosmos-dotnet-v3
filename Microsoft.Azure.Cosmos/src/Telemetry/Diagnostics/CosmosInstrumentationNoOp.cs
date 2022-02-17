// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;

    internal class CosmosInstrumentationNoOp : ICosmosInstrumentation
    {
        public DiagnosticAttributes Attributes { get; }

        public CosmosInstrumentationNoOp()
        {
            this.Attributes = new DiagnosticAttributes();
        }

        public void MarkFailed(Exception ex)
        {
            // NoOp
        }

        public void AddAttributesToScope()
        {
            // NoOp
        }

        public void Dispose()
        {
            // NoOp
        }
    }
}
