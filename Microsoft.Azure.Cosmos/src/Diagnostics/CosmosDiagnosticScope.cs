//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// This represents a single scope in the diagnostics.
    /// A scope is a section of code that is important to track.
    /// For example there is a scope for serialization, retry handlers, etc..
    /// </summary>
    internal sealed class CosmosDiagnosticScope : CosmosDiagnosticsInternal, IEnumerable<CosmosDiagnosticsInternal>, IDisposable
    {
        private readonly Stopwatch ElapsedTimeStopWatch;
        private readonly Action OnDisposeCallBack;
        private readonly List<CosmosDiagnosticsInternal> InnerDiagnostics;

        private bool isDisposed = false;

        public CosmosDiagnosticScope(
            string name,
            Action onDisposeCallBack)
        {
            this.Id = name;
            this.ElapsedTimeStopWatch = Stopwatch.StartNew();
            this.OnDisposeCallBack = onDisposeCallBack;
            this.InnerDiagnostics = new List<CosmosDiagnosticsInternal>();
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

        internal void AddDiagnosticsInternal(CosmosDiagnosticsInternal diagnosticsInternal)
        {
            if (diagnosticsInternal == null)
            {
                throw new ArgumentNullException(nameof(diagnosticsInternal));
            }

            this.InnerDiagnostics.Add(diagnosticsInternal);
        }

        internal TimeSpan GetElapsedTime()
        {
            return this.ElapsedTimeStopWatch.Elapsed;
        }

        internal bool IsComplete()
        {
            return !this.ElapsedTimeStopWatch.IsRunning;
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.OnDisposeCallBack();
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

        public IEnumerator<CosmosDiagnosticsInternal> GetEnumerator()
        {
            // Using a for loop with a yield prevents Issue #1467 which causes
            // ThrowInvalidOperationException if a new diagnostics is getting added
            // while the enumerator is being used.
            for (int i = 0; i < this.InnerDiagnostics.Count; i++)
            {
                yield return this.InnerDiagnostics[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
