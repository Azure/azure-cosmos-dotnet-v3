//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using Newtonsoft.Json;

    /// <summary>
    /// This represents a single scope in the diagnostics.
    /// A scope is a section of code that is important to track.
    /// For example there is a scope for serialization, retry handlers, etc..
    /// </summary>
    internal sealed class CosmosDiagnosticScope : CosmosDiagnosticsInternal, IDisposable
    {
        private readonly Stopwatch ElapsedTimeStopWatch;
        private bool isDisposed = false;

        public CosmosDiagnosticScope(
            string name,
            Stopwatch stopwatch = null)
        {
            this.Id = name;
            this.ElapsedTimeStopWatch = stopwatch ?? Stopwatch.StartNew();
        }

        public string Id { get; }

        public bool TryGetElapsedTime(out TimeSpan elapsedTime)
        {
            if (!this.isDisposed)
            {
                return false;
            }

            elapsedTime = this.ElapsedTimeStopWatch.Elapsed;
            return true;
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.ElapsedTimeStopWatch.Stop();
            this.isDisposed = true;
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
