//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal readonly struct QueryPipelineDiagnosticsBuilder
    {
        private readonly List<CosmosDiagnosticsInternal> Diagnostics;

        private QueryPipelineDiagnosticsBuilder(List<CosmosDiagnosticsInternal> diagnostics)
        {
            this.Diagnostics = diagnostics;
        }

        internal IDisposable CreateScope(string name)
        {
            CosmosDiagnosticScope scope = new CosmosDiagnosticScope(name);
            this.Diagnostics.Add(scope);
            return scope;
        }

        internal void AddDiagnosticContext(CosmosDiagnosticsContext diagnosticsContext)
        {
            this.Diagnostics.Add(diagnosticsContext);
        }

        internal IReadOnlyCollection<CosmosDiagnosticsInternal> Build()
        {
            return this.Diagnostics.AsReadOnly();
        }

        internal static QueryPipelineDiagnosticsBuilder Create()
        {
            return new QueryPipelineDiagnosticsBuilder(new List<CosmosDiagnosticsInternal>());
        }
    }
}
