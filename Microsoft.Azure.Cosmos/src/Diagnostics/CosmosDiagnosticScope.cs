//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// This represents a single scope in the diagnostics.
    /// A scope is a section of code that is important to track.
    /// For example there is a scope for serialization, retry handlers, etc..
    /// </summary>
    internal sealed class CosmosDiagnosticScope : CosmosDiagnosticsInternal, IDisposable
    {
        private static readonly Stopwatch SingletonTimer = Stopwatch.StartNew();
        private readonly TimeSpan startTimeSpan = CosmosDiagnosticScope.SingletonTimer.Elapsed;
        private TimeSpan? elapsedTimeSpan = null;

        private bool isDisposed = false;

        public CosmosDiagnosticScope(
            string name)
        {
            this.Id = name;
        }

        public string Id { get; }

        public bool TryGetElapsedTime(out TimeSpan elapsedTime)
        {
            if (!this.isDisposed || !this.elapsedTimeSpan.HasValue)
            {
                return false;
            }

            elapsedTime = this.elapsedTimeSpan.Value;
            return true;
        }

        internal TimeSpan GetElapsedTime()
        {
            if (this.elapsedTimeSpan.HasValue)
            {
                return this.elapsedTimeSpan.Value;
            }

            return CosmosDiagnosticScope.SingletonTimer.Elapsed - this.startTimeSpan;
        }

        internal bool IsComplete()
        {
            return this.elapsedTimeSpan.HasValue;
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.elapsedTimeSpan = CosmosDiagnosticScope.SingletonTimer.Elapsed - this.startTimeSpan;
            this.isDisposed = true;
        }

        public void Accept(CosmosDiagnosticsInternalVisitor cosmosDiagnosticsInternalVisitor)
        {
            cosmosDiagnosticsInternalVisitor.Visit(this);
        }

        public TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
