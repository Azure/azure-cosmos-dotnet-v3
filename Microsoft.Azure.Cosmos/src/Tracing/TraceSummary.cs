// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Telemetry.Diagnostics;

    internal class TraceSummary
    {
        internal TraceSummary(DiagnosticAttributes diagnosticAttributes)
        {
            this.DiagnosticAttributes = diagnosticAttributes;
        }

        internal DiagnosticAttributes DiagnosticAttributes { get; }
    }
}
