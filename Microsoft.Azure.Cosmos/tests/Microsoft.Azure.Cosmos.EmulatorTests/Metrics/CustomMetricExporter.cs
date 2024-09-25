//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Metrics
{
    using OpenTelemetry.Metrics;
    using OpenTelemetry;
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Linq;

    public class CustomMetricExporter : BaseExporter<Metric>
    {
        private static readonly Dictionary<string, MetricType> expectedMetrics = new Dictionary<string, MetricType>()
        {
            { "cosmos.client.op.calls", MetricType.LongSum },
            { "cosmos.client.op.latency", MetricType.Histogram },
            { "cosmos.client.op.RUs", MetricType.Histogram },
            { "cosmos.client.op.maxItemCount", MetricType.LongGauge},
            { "cosmos.client.op.actualItemCount", MetricType.LongGauge },
            { "cosmos.client.op.regionsContacted", MetricType.LongGauge }
        };

        // This method will be called periodically by OpenTelemetry SDK
        public override ExportResult Export(in Batch<Metric> batch)
        {
            Console.WriteLine("\n[Custom Exporter] Exporting metrics:");

            int gaugeCount = 0;

            Dictionary<string, MetricType> actualMetrics = new Dictionary<string, MetricType>();
            foreach (Metric metric in batch)
            {
                actualMetrics.Add(metric.Name, metric.MetricType);

                Console.WriteLine($"Metric: {metric.Name}, Type: {metric.MetricType}");
                foreach (MetricPoint metricPoint in metric.GetMetricPoints())
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

            Assert.IsTrue(expectedMetrics.Count == actualMetrics.Count && !expectedMetrics.Except(actualMetrics).Any());

            Console.WriteLine($"Total gauge values exported: {gaugeCount}");
            Console.WriteLine();

            return ExportResult.Success;
        }
    }
}
