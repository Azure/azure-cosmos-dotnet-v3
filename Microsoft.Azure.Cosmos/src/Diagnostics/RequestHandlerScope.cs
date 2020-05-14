//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;

    internal sealed class RequestHandlerScope : CosmosDiagnosticsInternal, IDisposable
    {
        private readonly Func<TimeSpan> getContextElapsedTime;
        private bool isDisposed = false;
        private TimeSpan? TotalElapsedTime = null;

        public RequestHandlerScope(
            RequestHandler handler,
            Func<TimeSpan> getContextElapsedTime)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            this.Id = handler.GetType().FullName;
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
