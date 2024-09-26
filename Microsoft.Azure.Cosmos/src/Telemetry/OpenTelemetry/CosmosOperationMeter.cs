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

            CosmosOperationMeter.NumberOfOperationCallCounter = cosmosMeter.CreateCounter<int>(
                name: OpenTelemetryMetricsConstant.OperationMetrics.NumberOfCallsName, 
                unit: OpenTelemetryMetricsConstant.OperationMetrics.Count, 
                description: OpenTelemetryMetricsConstant.OperationMetrics.NumberOfCallsDesc);

            CosmosOperationMeter.RequestLatencyHistogram = cosmosMeter.CreateHistogram<double>(name: OpenTelemetryMetricsConstant.OperationMetrics.LatencyName,
                unit: OpenTelemetryMetricsConstant.OperationMetrics.Ms,
                description: OpenTelemetryMetricsConstant.OperationMetrics.LatencyDesc);

            CosmosOperationMeter.RequestUnitsHistogram = cosmosMeter.CreateHistogram<double>(name: OpenTelemetryMetricsConstant.OperationMetrics.RUName,
                unit: OpenTelemetryMetricsConstant.OperationMetrics.RUUnit,
                description: OpenTelemetryMetricsConstant.OperationMetrics.RUDesc);

            CosmosOperationMeter.MaxItemGauge = cosmosMeter.CreateObservableGauge<int>(name: OpenTelemetryMetricsConstant.OperationMetrics.MaxItemName, 
                observeValues: () => CosmosOperationMeter.PullMaxItemCount(), 
                unit: OpenTelemetryMetricsConstant.OperationMetrics.Count,
                description: OpenTelemetryMetricsConstant.OperationMetrics.MaxItemDesc );

            CosmosOperationMeter.ActualItemGauge = cosmosMeter.CreateObservableGauge<int>(name: OpenTelemetryMetricsConstant.OperationMetrics.ActualItemName, 
                observeValues: () => CosmosOperationMeter.PullActualItemCount(), 
                unit: OpenTelemetryMetricsConstant.OperationMetrics.Count, 
                description: OpenTelemetryMetricsConstant.OperationMetrics.ActualItemDesc);

            CosmosOperationMeter.RegionsContactedGauge = cosmosMeter.CreateObservableGauge<int>(name: OpenTelemetryMetricsConstant.OperationMetrics.RegionContactedName, 
                observeValues: () => CosmosOperationMeter.PullRegionContactedCount(), 
                unit: OpenTelemetryMetricsConstant.OperationMetrics.Count, 
                description: OpenTelemetryMetricsConstant.OperationMetrics.RegionContactedDesc);
        }

        public static void RecordMaxItemCount(int maxItemCount, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!CosmosOperationMeter.MaxItemGauge.Enabled)
            {
                return;
            }

            CosmosOperationMeter.maxItemCounts ??= new ConcurrentBag<Tuple<int, KeyValuePair<string, object>[]>>();
            CosmosOperationMeter.maxItemCounts.Add(new Tuple<int, KeyValuePair<string, object>[]>(maxItemCount, dimensionsFunc()));
        }

        public static void RecordActualItemCount(int actualItemCount, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!CosmosOperationMeter.ActualItemGauge.Enabled)
            {
                return;
            }

            CosmosOperationMeter.actualItemCounts ??= new ConcurrentBag<Tuple<int, KeyValuePair<string, object>[]>>();
            CosmosOperationMeter.actualItemCounts.Add(new Tuple<int, KeyValuePair<string, object>[]>(actualItemCount, dimensionsFunc()));
        }

        public static void RecordRegionContactedCount(int regionsContactedCount, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!CosmosOperationMeter.RegionsContactedGauge.Enabled)
            {
                return;
            }

            CosmosOperationMeter.regionsContactedCounts ??= new ConcurrentBag<Tuple<int, KeyValuePair<string, object>[]>>();
            CosmosOperationMeter.regionsContactedCounts.Add(new Tuple<int, KeyValuePair<string, object>[]>(regionsContactedCount, dimensionsFunc()));
        }

        public static IEnumerable<Measurement<int>> PullMaxItemCount()
        {
            while (CosmosOperationMeter.maxItemCounts.Count > 0)
            {
                if (CosmosOperationMeter.maxItemCounts.TryTake(out Tuple<int, KeyValuePair<string, object>[]> maxItemCount))
                {
                    yield return new Measurement<int>(maxItemCount.Item1, maxItemCount.Item2);
                }
            }
        }

        public static IEnumerable<Measurement<int>> PullActualItemCount()
        {
            while (CosmosOperationMeter.actualItemCounts.Count > 0)
            {
                if (CosmosOperationMeter.actualItemCounts.TryTake(out Tuple<int, KeyValuePair<string, object>[]> actualItemCount))
                {
                    yield return new Measurement<int>(actualItemCount.Item1, actualItemCount.Item2);
                }
            }
        }

        public static IEnumerable<Measurement<int>> PullRegionContactedCount()
        {
            while (CosmosOperationMeter.regionsContactedCounts.Count > 0)
            {
                if (CosmosOperationMeter.regionsContactedCounts.TryTake(out Tuple<int, KeyValuePair<string, object>[]> regionsContactedCount))
                {
                    yield return new Measurement<int>(regionsContactedCount.Item1, regionsContactedCount.Item2);
                }
            }
        }

        internal static void RecordRequestUnit(double requestCharge, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!CosmosOperationMeter.RequestUnitsHistogram.Enabled)
            {
                return;
            }

            CosmosOperationMeter.RequestUnitsHistogram?.Record(requestCharge, dimensionsFunc());
        }

        internal static void RecordRequestLatency(TimeSpan? requestLatency, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!CosmosOperationMeter.RequestLatencyHistogram.Enabled || !requestLatency.HasValue)
            {
                return;
            }

            CosmosOperationMeter.RequestLatencyHistogram.Record(requestLatency.Value.Milliseconds, dimensionsFunc());
        }

        internal static void RecordOperationCallCount(Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!CosmosOperationMeter.NumberOfOperationCallCounter.Enabled)
            {
                return;
            }

            CosmosOperationMeter.NumberOfOperationCallCounter.Add(1, dimensionsFunc());
        }
    }
}
