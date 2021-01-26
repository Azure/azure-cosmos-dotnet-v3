//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System.IO;
    using System.Text;

    /// <summary>
    /// Extends <see cref="CosmosDiagnostics"/> to expose internal APIs.
    /// </summary>
#pragma warning disable SA1302 // Interface names should begin with I
    internal interface CosmosDiagnosticsInternal
#pragma warning restore SA1302 // Interface names should begin with I
    {
        public void Accept(CosmosDiagnosticsInternalVisitor visitor);

        public TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor);
    }
}
