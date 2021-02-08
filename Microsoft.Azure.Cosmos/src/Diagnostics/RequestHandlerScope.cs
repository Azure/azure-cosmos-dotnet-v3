//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Diagnostics;

    internal sealed class RequestHandlerScope : CosmosDiagnosticsInternal, IDisposable
    {
        private static readonly Stopwatch SingletonTimer = Stopwatch.StartNew();
        private readonly TimeSpan startTimeSpan = RequestHandlerScope.SingletonTimer.Elapsed;
        private TimeSpan? elapsedTimeSpan = null;

        private bool isDisposed = false;

        public RequestHandlerScope(RequestHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            this.Id = handler.FullHandlerName;
        }

        public string Id { get; }

        public bool TryGetTotalElapsedTime(out TimeSpan elapsedTime)
        {
            if (!this.isDisposed || !this.elapsedTimeSpan.HasValue)
            {
                return false;
            }

            elapsedTime = this.elapsedTimeSpan.Value;
            return true;
        }

        internal TimeSpan GetCurrentElapsedTime()
        {
            return RequestHandlerScope.SingletonTimer.Elapsed - this.startTimeSpan;
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

            this.elapsedTimeSpan = RequestHandlerScope.SingletonTimer.Elapsed - this.startTimeSpan;
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
