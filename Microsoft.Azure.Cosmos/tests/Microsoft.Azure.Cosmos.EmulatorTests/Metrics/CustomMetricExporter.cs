//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Metrics
{
    using OpenTelemetry.Metrics;
    using OpenTelemetry;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Linq;

    internal class CustomMetricExporter : BaseExporter<Metric>
    {
        private readonly ManualResetEventSlim manualResetEventSlim = null;

        internal static Dictionary<string, MetricType> ActualMetrics = new();
        internal static Dictionary<string, List<string>> Dimensions = new();

        public CustomMetricExporter(ManualResetEventSlim manualResetEventSlim)
        {
            CustomMetricExporter.ActualMetrics = new();
            CustomMetricExporter.Dimensions = new();

            this.manualResetEventSlim = manualResetEventSlim;
        }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            try
            {
                foreach (Metric metric in batch)
                {
                    ActualMetrics.TryAdd(metric.Name, metric.MetricType);

                    HashSet<string> actualDimensions = new();
                    foreach (MetricPoint metricPoint in metric.GetMetricPoints())
                    {
                        foreach (KeyValuePair<string, object> dimension in metricPoint.Tags)
                        {
                            actualDimensions.Add(dimension.Key);
                        }
                    }

                    Dimensions.TryAdd(metric.Name, actualDimensions.ToList());
                }

                if (ActualMetrics.Count > 0)
                {
                    this.manualResetEventSlim.Set();
                }
            }
            catch
            {
                this.manualResetEventSlim.Set();
                return ExportResult.Failure;
            }


            return ExportResult.Success;
        }
    }

    public class DimensionKey
    {
        public string MetricName { get; set; }
        public bool IsDataOperation { get; set; }
    }
}
