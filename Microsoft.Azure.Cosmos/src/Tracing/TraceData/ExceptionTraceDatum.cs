// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;

    internal sealed class ExceptionTraceDatum : TraceDatum
    {
        public ExceptionTraceDatum(Exception exception)
        {
            this.Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public Exception Exception { get; }

        internal override void Accept(ITraceDatumVisitor traceDatumVisitor)
        {
            traceDatumVisitor.Visit(this);
        }
    }
}