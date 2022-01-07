//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.SystemUsage
{
    using System.Collections.Generic;
    using HdrHistogram;
    using Microsoft.Azure.Documents.Rntbd;

    internal class MinThreads : SystemUsageBase
    {
        public MinThreads(LongConcurrentHistogram systemUsageHistogram, IReadOnlyCollection<SystemUsageLoad> systemUsageCollection) 
            : base(systemUsageHistogram, systemUsageCollection)
        {
        }

        public override string MetricName => ClientTelemetryOptions.MinThreadsName;

        public override string MetricUnit => ClientTelemetryOptions.MinThreadsUnit;

        public override long? ValueToRecord(SystemUsageLoad systemUsage)
        {
            return (long?)systemUsage.ThreadInfo?.MinThreads;
        }
    }
}
