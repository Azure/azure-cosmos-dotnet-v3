//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Metrics
{
    using OpenTelemetry.Metrics;
    using OpenTelemetry;
    using System.Collections.Generic;
    using System.Threading;
    using System;

    public class CustomMetricExporter : BaseExporter<Metric>
    {
        private readonly ManualResetEventSlim manualResetEventSlim = null;

        public static Dictionary<string, MetricType> ActualMetrics = new Dictionary<string, MetricType>();

        public CustomMetricExporter(ManualResetEventSlim manualResetEventSlim)
        {
            this.manualResetEventSlim = manualResetEventSlim;
        }

        // This method will be called periodically by OpenTelemetry SDK
        public override ExportResult Export(in Batch<Metric> batch)
        {
            Console.WriteLine("Exporting metrics...");
            foreach (Metric metric in batch)
            {
                Console.WriteLine($"Metric Name: {metric.Name}, Metric Type: {metric.MetricType}");
                ActualMetrics.Add(metric.Name, metric.MetricType);

                // Iterate over the data points for this metric
                foreach (MetricPoint metricPoint in metric.GetMetricPoints())
                {
                    Console.WriteLine($"  DataPoint - StartTime: {metricPoint.StartTime}, EndTime: {metricPoint.EndTime}");
                    Console.WriteLine($"  Attributes:");

                    foreach (KeyValuePair<string, object> attribute in metricPoint.Tags)
                    {
                        Console.WriteLine($"    {attribute.Key}: {attribute.Value}");
                    }

                    // Print different data depending on the MetricType
                    switch (metric.MetricType)
                    {
                        case MetricType.LongSum:
                        case MetricType.DoubleSum:
                            Console.WriteLine($"  Sum: {metricPoint.GetSumLong()}{metricPoint.GetSumDouble()}");
                            break;
                        case MetricType.LongGauge:
                        case MetricType.DoubleGauge:
                            Console.WriteLine($"  Gauge: {metricPoint.GetGaugeLastValueLong()}{metricPoint.GetGaugeLastValueDouble()}");
                            break;
                        case MetricType.LongSumNonMonotonic:  // Non-monotonic sums for UpDownCounter
                            Console.WriteLine($"  Non-Monotonic Long Sum: {metricPoint.GetSumLong()}");
                            break;
                        case MetricType.DoubleSumNonMonotonic:  // Non-monotonic sums for UpDownCounter (double version)
                            Console.WriteLine($"  Non-Monotonic Double Sum: {metricPoint.GetSumDouble()}");
                            break;
                        case MetricType.Histogram:
                            Console.WriteLine($"  Histogram - Count: {metricPoint.GetHistogramCount()}, Sum: {metricPoint.GetHistogramSum()}");
                            break;
                        default:
                            Console.WriteLine("  Unknown metric type.");
                            break;
                    }
                }

            }

            if (ActualMetrics.Count > 0)
            {
                this.manualResetEventSlim.Set();
            }

            Console.WriteLine($"Metrics Count {ActualMetrics.Count}");
            return ExportResult.Success;
        }
    }
}
