namespace CosmosBenchmark
{
    using System;
    using Microsoft.ApplicationInsights;

    internal class MetricCollectionWindow
    {
        private InsertOperationMetricsCollector insertOperationMetricsCollector;
        private readonly object insertOperationMetricsCollectorLock = new object();

        private QueryOperationMetricsCollector queryOperationMetricsCollector;
        private readonly object queryOperationMetricsCollectorLock = new object();

        private ReadOperationMetricsCollector readOperationMetricsCollector;
        private readonly object readOperationMetricsCollectorLock = new object();

        public DateTime DateTimeCreated { get; init; }

        public MetricCollectionWindow()
        {
            this.DateTimeCreated = DateTime.Now;
        }

        public InsertOperationMetricsCollector GetInsertOperationMetricsCollector(TelemetryClient telemetryClient)
        {
            if (this.insertOperationMetricsCollector is null)
            {
                lock (this.insertOperationMetricsCollectorLock)
                {
                    this.insertOperationMetricsCollector ??= new InsertOperationMetricsCollector(telemetryClient);
                }
            }

            return this.insertOperationMetricsCollector;
        }

        public QueryOperationMetricsCollector GetQueryOperationMetricsCollector(TelemetryClient telemetryClient)
        {
            if (this.queryOperationMetricsCollector is null)
            {
                lock (this.queryOperationMetricsCollectorLock)
                {
                    this.queryOperationMetricsCollector ??= new QueryOperationMetricsCollector(telemetryClient);
                }
            }

            return this.queryOperationMetricsCollector;
        }

        public ReadOperationMetricsCollector GetReadOperationMetricsCollector(TelemetryClient telemetryClient)
        {
            if (this.readOperationMetricsCollector is null)
            {
                lock (this.readOperationMetricsCollectorLock)
                {
                    this.readOperationMetricsCollector ??= new ReadOperationMetricsCollector(telemetryClient);
                }
            }

            return this.readOperationMetricsCollector;
        }
    }
}
