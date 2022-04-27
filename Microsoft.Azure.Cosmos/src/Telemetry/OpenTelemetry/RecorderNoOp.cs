// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using Microsoft.Azure.Cosmos.Tracing;

    internal class RecorderNoOp : IRecorder
    {
        public override void Dispose()
        {
            // NoOp
        }

        public override void MarkFailed(Exception exception)
        {
            // NoOp
        }

        public override void Record(string attributeKey, object attributeValue)
        {
            // NoOp
        }

        public override void Record(CosmosDiagnostics diagnostics)
        {
            // NoOp
        }
    }
}
