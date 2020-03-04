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
        private readonly Stopwatch ElapsedTimeStopWatch;
        private readonly Action<TimeSpan> ElapsedTimeCallback;
        private bool isDisposed = false;

        public CosmosDiagnosticScope(
            string name,
            Action<TimeSpan> elapsedTimeCallback = null)
        {
            this.Id = name;
            this.ElapsedTimeStopWatch = Stopwatch.StartNew();
            this.ElapsedTimeCallback = elapsedTimeCallback;
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
            this.ElapsedTimeCallback?.Invoke(this.ElapsedTimeStopWatch.Elapsed);
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
