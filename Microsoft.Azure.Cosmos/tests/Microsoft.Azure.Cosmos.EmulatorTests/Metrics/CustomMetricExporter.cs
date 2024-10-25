//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Metrics
{
    using OpenTelemetry.Metrics;
    using OpenTelemetry;
    using System.Collections.Generic;
    using System.Threading;

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
            foreach (Metric metric in batch)
            {
                ActualMetrics.TryAdd(metric.Name, metric.MetricType);
            }

            if (ActualMetrics.Count > 0)
            {
                this.manualResetEventSlim.Set();
            }

            return ExportResult.Success;
        }
    }
}
