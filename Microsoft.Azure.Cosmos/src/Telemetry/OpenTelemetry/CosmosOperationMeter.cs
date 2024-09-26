// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;

    internal static class CosmosOperationMeter
    {
        private static ConcurrentBag<Tuple<int, KeyValuePair<string, object>[]>> maxItemCounts = null;
        private static ConcurrentBag<Tuple<int, KeyValuePair<string, object>[]>> actualItemCounts = null;
        private static ConcurrentBag<Tuple<int, KeyValuePair<string, object>[]>> regionsContactedCounts = null;

        internal static Counter<int> NumberOfOperationCallCounter = null;
        internal static Histogram<double> RequestLatencyHistogram = null;
        internal static Histogram<double> RequestUnitsHistogram = null;
        internal static ObservableGauge<int> MaxItemGauge = null;
        internal static ObservableGauge<int> ActualItemGauge = null;
        internal static ObservableGauge<int> RegionsContactedGauge = null;

        private static Meter cosmosMeter;

        public static void Initialize()
        {
            cosmosMeter ??= new Meter(OpenTelemetryMetricsConstant.OperationMetricName, OpenTelemetryMetricsConstant.MetricVersion100);

            NumberOfOperationCallCounter = cosmosMeter.CreateCounter<int>(
                name: OpenTelemetryMetricsConstant.OperationMetrics.NumberOfCallsName, 
                unit: OpenTelemetryMetricsConstant.OperationMetrics.Count, 
                description: OpenTelemetryMetricsConstant.OperationMetrics.NumberOfCallsDesc);

            RequestLatencyHistogram = cosmosMeter.CreateHistogram<double>(name: OpenTelemetryMetricsConstant.OperationMetrics.LatencyName,
                unit: OpenTelemetryMetricsConstant.OperationMetrics.Count,
                description: OpenTelemetryMetricsConstant.OperationMetrics.LatencyDesc);

            RequestUnitsHistogram = cosmosMeter.CreateHistogram<double>(name: OpenTelemetryMetricsConstant.OperationMetrics.RUName,
                unit: OpenTelemetryMetricsConstant.OperationMetrics.RUUnit,
                description: OpenTelemetryMetricsConstant.OperationMetrics.RUDesc);

            MaxItemGauge = cosmosMeter.CreateObservableGauge<int>(name: "cosmos.client.op.maxItemCount", observeValues: () => CosmosOperationMeter.PullMaxItemCount(), unit: "#", description: "For feed operations (query, readAll, readMany, change feed) and batch operations this meter capture the requested maxItemCount per page/request");
            ActualItemGauge = cosmosMeter.CreateObservableGauge<int>(name: "cosmos.client.op.actualItemCount", observeValues: () => CosmosOperationMeter.PullActualItemCount(), unit: "#", description: "For feed operations (query, readAll, readMany, change feed) batch operations this meter capture the actual item count in responses from the service");
            RegionsContactedGauge = cosmosMeter.CreateObservableGauge<int>(name: "cosmos.client.op.regionsContacted", observeValues: () => CosmosOperationMeter.PullRegionContactedCount(), unit: "# regions", description: "Number of regions contacted when executing an operation");
        }

        public static void RecordMaxItemCount(int maxItemCount, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!MaxItemGauge.Enabled)
            {
                return;
            }

            maxItemCounts ??= new ConcurrentBag<Tuple<int, KeyValuePair<string, object>[]>>();
            maxItemCounts.Add(new Tuple<int, KeyValuePair<string, object>[]>(maxItemCount, dimensionsFunc()));
        }

        public static void RecordActualItemCount(int actualItemCount, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!ActualItemGauge.Enabled)
            {
                return;
            }

            actualItemCounts ??= new ConcurrentBag<Tuple<int, KeyValuePair<string, object>[]>>();
            actualItemCounts.Add(new Tuple<int, KeyValuePair<string, object>[]>(actualItemCount, dimensionsFunc()));
        }

        public static void RecordRegionContactedCount(int regionsContactedCount, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!RegionsContactedGauge.Enabled)
            {
                return;
            }

            regionsContactedCounts ??= new ConcurrentBag<Tuple<int, KeyValuePair<string, object>[]>>();
            regionsContactedCounts.Add(new Tuple<int, KeyValuePair<string, object>[]>(regionsContactedCount, dimensionsFunc()));
        }

        public static IEnumerable<Measurement<int>> PullMaxItemCount()
        {
            while (maxItemCounts.Count > 0)
            {
                if (maxItemCounts.TryTake(out Tuple<int, KeyValuePair<string, object>[]> maxItemCount))
                {
                    yield return new Measurement<int>(maxItemCount.Item1, maxItemCount.Item2);
                }
            }
        }

        public static IEnumerable<Measurement<int>> PullActualItemCount()
        {
            while (actualItemCounts.Count > 0)
            {
                if (actualItemCounts.TryTake(out Tuple<int, KeyValuePair<string, object>[]> actualItemCount))
                {
                    yield return new Measurement<int>(actualItemCount.Item1, actualItemCount.Item2);
                }
            }
        }

        public static IEnumerable<Measurement<int>> PullRegionContactedCount()
        {
            while (regionsContactedCounts.Count > 0)
            {
                if (regionsContactedCounts.TryTake(out Tuple<int, KeyValuePair<string, object>[]> regionsContactedCount))
                {
                    yield return new Measurement<int>(regionsContactedCount.Item1, regionsContactedCount.Item2);
                }
            }
        }

        internal static void RecordRequestUnit(double requestCharge, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!RequestUnitsHistogram.Enabled)
            {
                return;
            }

            RequestUnitsHistogram?.Record(requestCharge, dimensionsFunc());
        }

        internal static void RecordRequestLatency(TimeSpan? requestLatency, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!RequestLatencyHistogram.Enabled || !requestLatency.HasValue)
            {
                return;
            }

            RequestLatencyHistogram.Record(requestLatency.Value.Milliseconds, dimensionsFunc());
        }

        internal static void RecordOperationCallCount(Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!NumberOfOperationCallCounter.Enabled)
            {
                return;
            }

            NumberOfOperationCallCounter.Add(1, dimensionsFunc());
        }
    }
}
