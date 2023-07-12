namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics.Metrics;

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

        public InsertOperationMetricsCollector GetInsertOperationMetricsCollector(Meter meter)
        {
            if (this.insertOperationMetricsCollector is null)
            {
                lock (this.insertOperationMetricsCollectorLock)
                {
                    this.insertOperationMetricsCollector ??= new InsertOperationMetricsCollector(meter);
                }
            }

            return this.insertOperationMetricsCollector;
        }

        public QueryOperationMetricsCollector GetQueryOperationMetricsCollector(Meter meter)
        {
            if (this.queryOperationMetricsCollector is null)
            {
                lock (this.queryOperationMetricsCollectorLock)
                {
                    this.queryOperationMetricsCollector ??= new QueryOperationMetricsCollector(meter);
                }
            }

            return this.queryOperationMetricsCollector;
        }

        public ReadOperationMetricsCollector GetReadOperationMetricsCollector(Meter meter)
        {
            if (this.readOperationMetricsCollector is null)
            {
                lock (this.readOperationMetricsCollectorLock)
                {
                    this.readOperationMetricsCollector ??= new ReadOperationMetricsCollector(meter);
                }
            }

            return this.readOperationMetricsCollector;
        }
    }
}
