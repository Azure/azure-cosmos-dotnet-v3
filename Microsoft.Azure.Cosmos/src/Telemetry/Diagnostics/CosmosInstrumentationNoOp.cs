namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Tracing;

    internal class CosmosInstrumentationNoOp : ICosmosInstrumentation
    {
        public void MarkDone(ITrace trace, DiagnosticAttributes attributes)
    }
}
