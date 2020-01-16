//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// This represents the core diagnostics object used in the SDK.
    /// This object gets created on the initial request and passed down
    /// through the pipeline appending information as it goes into a list
    /// where it is lazily converted to a JSON string.
    /// </summary>
    internal sealed class CosmosDiagnosticsContext : CosmosDiagnostics
    {
        // Summary contains the high level overview of operations
        // like start time, retries, and other aggregated information
        internal CosmosDiagnosticSummary Summary { get; }

        // Context list is detailed view of all the operations
        internal CosmosDiagnosticsContextList ContextList { get; }

        private bool isFirstScope = true;

        internal CosmosDiagnosticsContext()
        {
            this.Summary = new CosmosDiagnosticSummary(DateTime.UtcNow);
            this.ContextList = new CosmosDiagnosticsContextList();
        }

        internal CosmosDiagnosticScope CreateScope(string name)
        {
            CosmosDiagnosticScope scope = this.isFirstScope ?
                new CosmosDiagnosticScope(name, this.Summary.SetElapsedTime)
                : new CosmosDiagnosticScope(name);

            this.ContextList.AddWriter(scope);
            this.isFirstScope = false;
            return scope;
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("{");
            this.Summary.WriteJsonProperty(stringBuilder);
            stringBuilder.Append(",\"Context\":[");
            this.ContextList.WriteJsonObject(stringBuilder);
            stringBuilder.Append("]}");
            return stringBuilder.ToString();
        }

        internal void AddSummaryWriter(CosmosDiagnosticWriter diagnosticWriter)
        {
            this.Summary.AddWriter(diagnosticWriter);
        }

        internal void AddContextWriter(CosmosDiagnosticWriter diagnosticWriter)
        {
            this.ContextList.AddWriter(diagnosticWriter);
        }

        internal void Append(CosmosDiagnosticsContext newContext)
        {
            if (Object.ReferenceEquals(this, newContext))
            {
                return;
            }

            this.Summary.Append(newContext.Summary);

            this.ContextList.Append(newContext.ContextList);
        }
    }
}