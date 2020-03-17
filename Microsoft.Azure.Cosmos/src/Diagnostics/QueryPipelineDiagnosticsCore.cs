//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class QueryPipelineDiagnosticsCore : QueryPipelineDiagnostics
    {
        private readonly CosmosDiagnosticsContext diagnosticsContext;

        internal QueryPipelineDiagnosticsCore(CosmosDiagnosticsContext diagnosticsContext)
        {
            this.diagnosticsContext = diagnosticsContext ?? throw new ArgumentNullException(nameof(diagnosticsContext));
        }

        internal override IDisposable CreateScope(string name)
        {
            return this.diagnosticsContext.CreateScope(name);
        }
    }
}
