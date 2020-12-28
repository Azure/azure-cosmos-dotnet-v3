// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;

    internal sealed class CpuHistoryTraceDatum : TraceDatum
    {
        public CpuHistoryTraceDatum(Documents.Rntbd.CpuLoadHistory cpuLoadHistory)
        {
            this.Value = cpuLoadHistory ?? throw new ArgumentNullException(nameof(cpuLoadHistory));
        }

        public Documents.Rntbd.CpuLoadHistory Value { get; }

        internal override void Accept(ITraceDatumVisitor traceDatumVisitor)
        {
            traceDatumVisitor.Visit(this);
        }
    }
}