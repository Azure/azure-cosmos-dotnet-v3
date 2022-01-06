//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.SystemUsage
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using HdrHistogram;
    using Microsoft.Azure.Documents.Rntbd;

    internal abstract class ISystemUsage
    {
        private readonly LongConcurrentHistogram systemUsageHistogram;
        private readonly IReadOnlyCollection<SystemUsageLoad> systemUsageCollection;

        public ISystemUsage(LongConcurrentHistogram systemUsageHistogram, IReadOnlyCollection<SystemUsageLoad> systemUsageCollection)
        {
            this.systemUsageHistogram = systemUsageHistogram;
            this.systemUsageCollection = systemUsageCollection;
        }

        /// <summary>
        /// It will return what value should be recorded in Client Telemetry for overrided metric
        /// </summary>
        /// <returns>Vlaue to recorded in Client Telemetry</returns>
        public abstract long? ValueToRecord(SystemUsageLoad systemUsage);

        public abstract string MetricName { get; }

        public abstract string MetricUnit { get; }

        /// <summary>
        /// Asjustment made while aggregating values. By default is 1, which means aggregation is not required
        /// </summary>
        public virtual int AggregationAdjustment => 1;

        public virtual SystemInfo GetSystemInfo()
        {
            SystemInfo systemInfo = new SystemInfo(this.MetricName, this.MetricUnit);
            foreach (SystemUsageLoad load in this.systemUsageCollection)
            {
                long? infoToRecord = this.ValueToRecord(load);
                if (infoToRecord.HasValue)
                {
                    this.systemUsageHistogram.RecordValue((long)infoToRecord);
                }
            }
            systemInfo.SetAggregators(this.systemUsageHistogram, this.AggregationAdjustment);

            this.systemUsageHistogram.Reset();

            return systemInfo;
        }
    }
}
