//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.SystemUsage
{
    using System.Collections.Generic;
    using HdrHistogram;
    using Microsoft.Azure.Documents.Rntbd;

    internal class AvailableThreads : ISystemUsage
    {
        public AvailableThreads(LongConcurrentHistogram systemUsageHistogram, IReadOnlyCollection<SystemUsageLoad> systemUsageCollection) 
            : base(systemUsageHistogram, systemUsageCollection)
        {
        }

        public override string MetricName => ClientTelemetryOptions.AvailableThreadsName;

        public override string MetricUnit => ClientTelemetryOptions.AvailableThreadsUnit;

        public override long? ValueToRecord(SystemUsageLoad systemUsage)
        {
            return (long?)systemUsage.ThreadInfo?.AvailableThreads;
        }
    }
}
