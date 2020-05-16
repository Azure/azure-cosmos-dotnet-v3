//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Diagnostics;

    internal class CosmosDiagnosticScopeEnd : CosmosDiagnosticsInternal
    {
        public static readonly CosmosDiagnosticScopeEnd Singleton = new CosmosDiagnosticScopeEnd();

        private CosmosDiagnosticScopeEnd()
        {
        }

        public override void Accept(CosmosDiagnosticsInternalVisitor cosmosDiagnosticsInternalVisitor)
        {
            cosmosDiagnosticsInternalVisitor.Visit(this);
        }

        public override TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
