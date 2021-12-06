//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Telemetry.SystemUsage;
    using Microsoft.Azure.Documents.Rntbd;

    internal static class SystemInfoUsageFactory
    {
        /// <summary>
        /// Returns System Usage Metric Info for a given metrics
        /// </summary>
        /// <param name="systemUsageHistory"></param>
        /// <param name="systemUsageHistogram"></param>
        /// <param name="metricName"></param>
        /// <param name="metricUnit"></param>
        internal static SystemInfo RecordSystemUsageMetricInfoAndResetHist(SystemUsageHistory systemUsageHistory,
            LongConcurrentHistogram systemUsageHistogram,
            string metricName,
            string metricUnit)
        {
            int aggregationAdjustment = 1;
            foreach (SystemUsageLoad systemUsage in systemUsageHistory.Values)
            {
                ISystemUsage systemUsageMetric = null;
               
                if (metricName.Equals(ClientTelemetryOptions.CpuName))
                {
                    systemUsageMetric = new CpuUsage(systemUsage);
                }
                else
                if (metricName.Equals(ClientTelemetryOptions.MemoryName))
                {
                    systemUsageMetric = new MemoryRemaining(systemUsage);
                }
                else
                if (metricName.Equals(ClientTelemetryOptions.AvailableThreadsName))
                {
                    systemUsageMetric = new AvailableThreads(systemUsage);
                }
                else
                if (metricName.Equals(ClientTelemetryOptions.MinThreadsName))
                {
                    systemUsageMetric = new MinThreads(systemUsage);
                }
                else
                if (metricName.Equals(ClientTelemetryOptions.MaxThreadsName))
                {
                    systemUsageMetric = new MaxThreads(systemUsage);
                }
                else
                {
                    DefaultTrace.TraceWarning("Invalid Metric Name is passed i.e. {0}", metricName);
                }

                aggregationAdjustment = systemUsageMetric.AggregationAdjustment;

                long? infoToRecord = systemUsageMetric.ValueToRecord();
                if (infoToRecord.HasValue)
                {
                    systemUsageHistogram.RecordValue((long)infoToRecord);
                }
            }

            SystemInfo systemInfoPayload = null;
            if (systemUsageHistogram.TotalCount > 0)
            {
                systemInfoPayload = new SystemInfo(metricName, metricUnit);
                systemInfoPayload.SetAggregators(systemUsageHistogram, aggregationAdjustment);
            }

            systemUsageHistogram.Reset();

            return systemInfoPayload;
        }

    }
}
