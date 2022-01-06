//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.SystemUsage
{
    using System.Collections.Generic;
    using HdrHistogram;
    using Microsoft.Azure.Documents.Rntbd;

    internal class CpuUsage : ISystemUsage
    {
        public CpuUsage(LongConcurrentHistogram systemUsageHistogram, IReadOnlyCollection<SystemUsageLoad> systemUsageCollection)
            : base(systemUsageHistogram, systemUsageCollection)
        { 
        }

        public override int AggregationAdjustment => ClientTelemetryOptions.HistogramPrecisionFactor;

        public override string MetricName => ClientTelemetryOptions.CpuName;

        public override string MetricUnit => ClientTelemetryOptions.CpuUnit;

        public override long? ValueToRecord(SystemUsageLoad systemUsage)
        {
            return (long?)systemUsage.CpuUsage * ClientTelemetryOptions.HistogramPrecisionFactor;
        }
    }
}
