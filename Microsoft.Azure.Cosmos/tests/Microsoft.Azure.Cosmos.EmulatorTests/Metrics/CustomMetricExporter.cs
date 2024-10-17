//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Metrics
{
    using OpenTelemetry.Metrics;
    using OpenTelemetry;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Linq;
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
                ActualMetrics.Add(metric.Name, metric.MetricType);
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
