//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;

    /// <summary>
    /// This represents a single scope in the diagnostics.
    /// A scope is a section of code that is important to track.
    /// For example there is a scope for serialization, retry handlers, etc..
    /// </summary>
    internal sealed class CosmosDiagnosticScope : CosmosDiagnosticsInternal, IDisposable
    {
        private readonly Func<TimeSpan> getContextElapsedTime;
        private bool isDisposed = false;
        private TimeSpan? TotalElapsedTime = null;

        public CosmosDiagnosticScope(
            string name,
            Func<TimeSpan> getContextElapsedTime)
        {
            this.Id = name;
            this.getContextElapsedTime = getContextElapsedTime;
            this.StartTime = getContextElapsedTime();
        }

        public string Id { get; }

        public TimeSpan StartTime { get; }

        public bool TryGetTotalElapsedTime(out TimeSpan timeSpan)
        {
            if (this.TotalElapsedTime.HasValue)
            {
                timeSpan = this.TotalElapsedTime.Value;
                return true;
            }

            return false;
        }

        public TimeSpan GetCurrentElapsedTime()
        {
            if (this.TotalElapsedTime.HasValue)
            {
                return this.TotalElapsedTime.Value;
            }

            return this.getContextElapsedTime() - this.StartTime;
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.TotalElapsedTime = this.getContextElapsedTime() - this.StartTime;
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
