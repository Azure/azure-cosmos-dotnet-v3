//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System.Collections.Generic;
    using HdrHistogram;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// This Class responsibility is to process System Usage information and convert them into a Client Telemetry Property, SystemInfo with more information
    /// </summary>
    internal static class TelemetrySystemUsage
    {
        /// <summary>
        /// Collecting CPU usage information and aggregating that data using Histogram
        /// </summary>
        /// <param name="systemUsageCollection"></param>
        /// <returns>SystemInfo</returns>
        public static SystemInfo GetCpuInfo(IReadOnlyCollection<SystemUsageLoad> systemUsageCollection)
        {
            LongConcurrentHistogram histogram = new LongConcurrentHistogram(ClientTelemetryOptions.CpuMin,
                                                        ClientTelemetryOptions.CpuMax,
                                                        ClientTelemetryOptions.CpuPrecision);

            SystemInfo systemInfo = new SystemInfo(ClientTelemetryOptions.CpuName, ClientTelemetryOptions.CpuUnit);
            foreach (SystemUsageLoad load in systemUsageCollection)
            {
                if (float.IsNaN(load.CpuUsage.Value))
                {
                    continue;
                }

                long? infoToRecord = (long?)load.CpuUsage * ClientTelemetryOptions.HistogramPrecisionFactor;
                if (infoToRecord.HasValue)
                {
                    histogram.RecordValue((long)infoToRecord);
                }
            }

            if (histogram.TotalCount > 0)
            {
                systemInfo.SetAggregators(histogram, ClientTelemetryOptions.HistogramPrecisionFactor);
            }
            
            return systemInfo;
        }

        /// <summary>
        /// Collecting Memory Remaining information and aggregating that data using Histogram
        /// </summary>
        /// <param name="systemUsageCollection"></param>
        /// <returns>SystemInfo</returns>
        public static SystemInfo GetMemoryRemainingInfo(IReadOnlyCollection<SystemUsageLoad> systemUsageCollection)
        {
            LongConcurrentHistogram histogram = new LongConcurrentHistogram(ClientTelemetryOptions.MemoryMin,
                                                        ClientTelemetryOptions.MemoryMax,
                                                        ClientTelemetryOptions.MemoryPrecision);

            SystemInfo systemInfo = new SystemInfo(ClientTelemetryOptions.MemoryName, ClientTelemetryOptions.MemoryUnit);
            foreach (SystemUsageLoad load in systemUsageCollection)
            {
                long? infoToRecord = (long?)load.MemoryAvailable;
                if (infoToRecord.HasValue)
                {
                    histogram.RecordValue((long)infoToRecord);
                }
            }

            if (histogram.TotalCount > 0)
            {
                systemInfo.SetAggregators(histogram, ClientTelemetryOptions.KbToMbFactor);
            }

            return systemInfo;
        }

        /// <summary>
        /// Collecting Available Thread information and aggregating that data using Histogram
        /// </summary>
        /// <param name="systemUsageCollection"></param>
        /// <returns>SystemInfo</returns>
        public static SystemInfo GetAvailableThreadsInfo(IReadOnlyCollection<SystemUsageLoad> systemUsageCollection)
        {
            LongConcurrentHistogram histogram = new LongConcurrentHistogram(ClientTelemetryOptions.AvailableThreadsMin,
                                                        ClientTelemetryOptions.AvailableThreadsMax,
                                                        ClientTelemetryOptions.AvailableThreadsPrecision);

            SystemInfo systemInfo = new SystemInfo(ClientTelemetryOptions.AvailableThreadsName, ClientTelemetryOptions.AvailableThreadsUnit);
            foreach (SystemUsageLoad load in systemUsageCollection)
            {
                long? infoToRecord = (long?)load.ThreadInfo?.AvailableThreads;
                if (infoToRecord.HasValue)
                {
                    histogram.RecordValue((long)infoToRecord);
                }
            }

            if (histogram.TotalCount > 0)
            {
                systemInfo.SetAggregators(histogram);
            }

            return systemInfo;
        }

        /// <summary>
        /// Collecting Configured Max Thread and aggregating that data using Histogram
        /// </summary>
        /// <param name="systemUsageCollection"></param>
        /// <returns>SystemInfo</returns>
        public static SystemInfo GetMaxThreadsInfo(IReadOnlyCollection<SystemUsageLoad> systemUsageCollection)
        {
            LongConcurrentHistogram histogram = new LongConcurrentHistogram(ClientTelemetryOptions.MaxThreadsMin,
                                                        ClientTelemetryOptions.MaxThreadsMax,
                                                        ClientTelemetryOptions.MaxThreadsPrecision);

            SystemInfo systemInfo = new SystemInfo(ClientTelemetryOptions.MaxThreadsName, ClientTelemetryOptions.MaxThreadsUnit);
            foreach (SystemUsageLoad load in systemUsageCollection)
            {
                long? infoToRecord = (long?)load.ThreadInfo?.MaxThreads;
                if (infoToRecord.HasValue)
                {
                    histogram.RecordValue((long)infoToRecord);
                }
            }

            if (histogram.TotalCount > 0)
            {
                systemInfo.SetAggregators(histogram);
            }

            return systemInfo;
        }

        /// <summary>
        /// Collecting Configured Min Thread and aggregating that data using Histogram
        /// </summary>
        /// <param name="systemUsageCollection"></param>
        /// <returns>SystemInfo</returns>
        public static SystemInfo GetMinThreadsInfo(IReadOnlyCollection<SystemUsageLoad> systemUsageCollection)
        {
            LongConcurrentHistogram histogram = new LongConcurrentHistogram(ClientTelemetryOptions.MinThreadsMin,
                                                        ClientTelemetryOptions.MinThreadsMax,
                                                        ClientTelemetryOptions.MinThreadsPrecision);

            SystemInfo systemInfo = new SystemInfo(ClientTelemetryOptions.MinThreadsName, ClientTelemetryOptions.MinThreadsUnit);
            foreach (SystemUsageLoad load in systemUsageCollection)
            {
                long? infoToRecord = (long?)load.ThreadInfo?.MinThreads;
                if (infoToRecord.HasValue)
                {
                    histogram.RecordValue((long)infoToRecord);
                }
            }

            if (histogram.TotalCount > 0)
            {
                systemInfo.SetAggregators(histogram);
            }

            return systemInfo;
        }
    }
}
