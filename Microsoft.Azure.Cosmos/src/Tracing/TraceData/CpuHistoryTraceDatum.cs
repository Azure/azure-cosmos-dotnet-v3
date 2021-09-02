// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;

    internal sealed class CpuHistoryTraceDatum : TraceDatum
    {
        public CpuHistoryTraceDatum(Documents.Rntbd.SystemUsageHistory cpuLoadHistory)
        {
            this.Value = cpuLoadHistory ?? throw new ArgumentNullException(nameof(cpuLoadHistory));
        }

        public Documents.Rntbd.SystemUsageHistory Value { get; }

        internal override void Accept(ITraceDatumVisitor traceDatumVisitor)
        {
            traceDatumVisitor.Visit(this);
        }
    }
}