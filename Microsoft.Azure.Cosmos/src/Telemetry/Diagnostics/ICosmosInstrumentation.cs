// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;

#if INTERNAL
    public
#else
    internal
#endif 
        interface ICosmosInstrumentation : IDisposable
        {
            public DiagnosticAttributes Attributes { get; }

            public void MarkFailed(Exception ex);

            public void AddAttributesToScope();
        }
}
