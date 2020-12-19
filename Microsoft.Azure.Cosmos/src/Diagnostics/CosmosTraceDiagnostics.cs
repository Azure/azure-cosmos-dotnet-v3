// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class CosmosTraceDiagnostics : CosmosDiagnostics
    {
        public CosmosTraceDiagnostics(ITrace trace)
        {
            this.Value = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        public ITrace Value { get; }

        public override string ToString()
        {
            return TraceWriter.TraceToText(this.Value);
        }
    }
}
