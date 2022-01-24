//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.DiagnosticSource
{
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;

    internal class CosmosDiagnosticSource : DiagnosticListener
    {
        public readonly static string DiagnosticSourceName = "Azure.Cosmos";

        public CosmosDiagnosticSource()
            : base(DiagnosticSourceName)
        {
        }

        public void Emit(string name, ITrace trace)
        {
            if (base.IsEnabled())
            {
                base.Write(name, new CosmosTraceDiagnostics(trace));
            }
        }

    }
}
