//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System.Diagnostics;
    using System.Threading.Tasks;

    internal class CosmosDiagnosticSource : DiagnosticListener
    {
        public readonly static string DiagnosticSourceName = "Azure.Cosmos";

        public CosmosDiagnosticSource()
            : base(DiagnosticSourceName)
        {
        }

        public void Write<T>(string name, Response<T> response)
        {
            if (base.IsEnabled())
            {
                base.Write(name, response.Diagnostics);
            }
        }
    }
}
