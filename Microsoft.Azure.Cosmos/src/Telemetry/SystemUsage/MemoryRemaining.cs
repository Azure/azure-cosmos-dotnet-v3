//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.SystemUsage
{
    using Microsoft.Azure.Documents.Rntbd;

    internal class MemoryRemaining : ISystemUsage
    {
        private readonly SystemUsageLoad systemUsage;

        public MemoryRemaining(SystemUsageLoad systemUsage)
        {
            this.systemUsage = systemUsage;
        }

        public override int AggregationAdjustment => ClientTelemetryOptions.KbToMbFactor;

        public override long? ValueToRecord()
        {
            return (long?)this.systemUsage.MemoryAvailable;
        }
    }
}
