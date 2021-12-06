//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.SystemUsage
{
    using Microsoft.Azure.Documents.Rntbd;

    internal class CpuUsage : ISystemUsage
    {
        private readonly SystemUsageLoad systemUsage;

        public CpuUsage(SystemUsageLoad systemUsage)
        {
            this.systemUsage = systemUsage;
        }

        public override int AggregationAdjustment => ClientTelemetryOptions.HistogramPrecisionFactor;

        public override long? ValueToRecord()
        {
            return (long?)this.systemUsage.CpuUsage * ClientTelemetryOptions.HistogramPrecisionFactor;
        }
    }
}
