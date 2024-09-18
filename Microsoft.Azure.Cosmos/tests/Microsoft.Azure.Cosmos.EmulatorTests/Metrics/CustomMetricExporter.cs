//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Metrics
{
    using OpenTelemetry.Metrics;
    using OpenTelemetry;
    using System;

    public class CustomMetricExporter : BaseExporter<Metric>
    {
        // This method will be called periodically by OpenTelemetry SDK
        public override ExportResult Export(in Batch<Metric> batch)
        {
            Console.WriteLine("\n[Custom Exporter] Exporting metrics:");

            int gaugeCount = 0;
            foreach (var metric in batch)
            {
                Console.WriteLine($"Metric: {metric.Name}, Type: {metric.MetricType}");

                foreach (var metricPoint in metric.GetMetricPoints())
                {
                    switch (metric.MetricType)
                    {
                        case MetricType.LongSum:
                            Console.WriteLine($"Value (Counter): {metricPoint.GetSumLong()}");
                            break;
                        case MetricType.LongGauge:
                            Console.WriteLine($"Value (Gauge): {metricPoint.GetGaugeLastValueLong()}");
                            gaugeCount++;
                            break;
                        case MetricType.Histogram:
                            Console.WriteLine($"Value (Histogram): {metricPoint.GetHistogramCount()}");

                            foreach (HistogramBucket bucket in metricPoint.GetHistogramBuckets())
                            {
                                Console.WriteLine($"{bucket.ExplicitBound} : {bucket.BucketCount}");
                            }
                            break;
                    }
                }

            }

            Console.WriteLine($"Total gauge values exported: {gaugeCount}");
            Console.WriteLine();

            return ExportResult.Success;
        }
    }
}
