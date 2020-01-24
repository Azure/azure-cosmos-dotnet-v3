//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Newtonsoft.Json;

    /// <summary>
    /// This represents the core diagnostics object used in the SDK.
    /// This object gets created on the initial request and passed down
    /// through the pipeline appending information as it goes into a list
    /// where it is lazily converted to a JSON string.
    /// </summary>
    internal sealed class CosmosDiagnosticsContext : CosmosDiagnosticsInternal
    {
        private bool isOverallScopeSet;

        public CosmosDiagnosticsContext()
        {
            this.Summary = new CosmosDiagnosticSummary(DateTime.UtcNow);
            this.ContextList = new CosmosDiagnosticsContextList();
        }

        /// <summary>
        /// Contains the high level overview of operations like start time, retries, and other aggregated information
        /// </summary>
        public CosmosDiagnosticSummary Summary { get; }

        /// <summary>
        /// Detailed view of all the operations.
        /// </summary>
        public CosmosDiagnosticsContextList ContextList { get; }

        public CosmosDiagnosticScope CreateOverallScope(string name)
        {
            CosmosDiagnosticScope scope;
            // If overall is already set then let the original set the elapsed time.
            if (this.isOverallScopeSet)
            {
                scope = new CosmosDiagnosticScope(name);
            }
            else
            {
                scope = new CosmosDiagnosticScope(name, this.Summary.SetElapsedTime);
                this.isOverallScopeSet = true;
            }

            this.ContextList.AddDiagnostics(scope);
            return scope;
        }

        internal CosmosDiagnosticScope CreateScope(string name)
        {
            CosmosDiagnosticScope scope = new CosmosDiagnosticScope(name);

            this.ContextList.AddDiagnostics(scope);
            return scope;
        }

        internal void AddDiagnosticsInternal(CosmosDiagnosticsInternal diagnosticsInternal)
        {
            if (diagnosticsInternal == null)
            {
                throw new ArgumentNullException(nameof(diagnosticsInternal));
            }

            this.ContextList.AddDiagnostics(diagnosticsInternal);
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