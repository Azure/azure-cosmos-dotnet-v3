//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.SystemUsage
{
    using System.Collections.Generic;
    using HdrHistogram;
    using Microsoft.Azure.Documents.Rntbd;

    internal class MemoryRemaining : SystemUsageBase
    {
        public MemoryRemaining(LongConcurrentHistogram systemUsageHistogram, IReadOnlyCollection<SystemUsageLoad> systemUsageCollection) 
            : base(systemUsageHistogram, systemUsageCollection)
        {
        }

        public override int AggregationAdjustment => ClientTelemetryOptions.KbToMbFactor;

        public override string MetricName => ClientTelemetryOptions.MemoryName;

        public override string MetricUnit => ClientTelemetryOptions.MemoryUnit;

        public override long? ValueToRecord(SystemUsageLoad systemUsage)
        {
            return (long?)systemUsage.MemoryAvailable;
        }
    }
}
