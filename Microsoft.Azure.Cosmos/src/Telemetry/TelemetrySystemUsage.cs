//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
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
                    histogram.RecordValue(infoToRecord.Value);
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
                    histogram.RecordValue(infoToRecord.Value);
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
                    histogram.RecordValue(infoToRecord.Value);
                }
            }

            if (histogram.TotalCount > 0)
            {
                systemInfo.SetAggregators(histogram);
            }

            return systemInfo;
        }

        /// <summary>
        /// Collecting Thread Starvation Flags Count
        /// </summary>
        /// <param name="systemUsageCollection"></param>
        /// <returns>SystemInfo</returns>
        public static SystemInfo GetThreadStarvationSignalCount(IReadOnlyCollection<SystemUsageLoad> systemUsageCollection)
        {
            int counter = 0;
            foreach (SystemUsageLoad load in systemUsageCollection)
            {
                bool? infoToRecord = load.ThreadInfo?.IsThreadStarving;
                if (infoToRecord.HasValue && infoToRecord.Value)
                {
                    counter++;
                }
            }
            SystemInfo systemInfo = 
                new SystemInfo(
                    metricsName: ClientTelemetryOptions.IsThreadStarvingName, 
                    unitName: ClientTelemetryOptions.IsThreadStarvingUnit,
                    count: counter);

            return systemInfo;
        }

        /// <summary>
        /// Collecting Thread Wait Interval in Millisecond and aggregating using Histogram
        /// </summary>
        /// <param name="systemUsageCollection"></param>
        /// <returns>SystemInfo</returns>
        public static SystemInfo GetThreadWaitIntervalInMs(IReadOnlyCollection<SystemUsageLoad> systemUsageCollection)
        {
            LongConcurrentHistogram histogram = new LongConcurrentHistogram(ClientTelemetryOptions.ThreadWaitIntervalInMsMin,
                                                        ClientTelemetryOptions.ThreadWaitIntervalInMsMax,
                                                        ClientTelemetryOptions.ThreadWaitIntervalInMsPrecision);

            SystemInfo systemInfo = new SystemInfo(ClientTelemetryOptions.ThreadWaitIntervalInMsName, ClientTelemetryOptions.ThreadWaitIntervalInMsUnit);
            foreach (SystemUsageLoad load in systemUsageCollection)
            {
                double? infoToRecord = load.ThreadInfo?.ThreadWaitIntervalInMs;
                if (infoToRecord.HasValue)
                {
                    histogram.RecordValue(TimeSpan.FromMilliseconds(infoToRecord.Value).Ticks);
                }
            }

            if (histogram.TotalCount > 0)
            {
                systemInfo.SetAggregators(histogram, ClientTelemetryOptions.TicksToMsFactor);
            }

            return systemInfo;
        }

        /// <summary>
        /// Collecting TCP Connection Count and aggregating using Histogram
        /// </summary>
        /// <param name="systemUsageCollection"></param>
        /// <returns>SystemInfo</returns>
        public static SystemInfo GetTcpConnectionCount(IReadOnlyCollection<SystemUsageLoad> systemUsageCollection)
        {
            LongConcurrentHistogram histogram = new LongConcurrentHistogram(ClientTelemetryOptions.NumberOfTcpConnectionMin,
                                                        ClientTelemetryOptions.NumberOfTcpConnectionMax,
                                                        ClientTelemetryOptions.NumberOfTcpConnectionPrecision);

            SystemInfo systemInfo = new SystemInfo(ClientTelemetryOptions.NumberOfTcpConnectionName, ClientTelemetryOptions.NumberOfTcpConnectionUnit);
            foreach (SystemUsageLoad load in systemUsageCollection)
            {
                int? infoToRecord = load.NumberOfOpenTcpConnections;

                // If anyhow, there are more than 70000 TCP connections, just fallback to 69999
                if (infoToRecord.HasValue && infoToRecord.Value >= ClientTelemetryOptions.NumberOfTcpConnectionMax)
                {
                    infoToRecord = (int)(ClientTelemetryOptions.NumberOfTcpConnectionMax - 1);
                }

                if (infoToRecord.HasValue)
                {
                    histogram.RecordValue(infoToRecord.Value);
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
