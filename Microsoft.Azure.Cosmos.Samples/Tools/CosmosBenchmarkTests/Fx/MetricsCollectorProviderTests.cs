namespace CosmosBenchmark.Fx.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using OpenTelemetry.Metrics;

    [TestClass]
    public class MetricsCollectorProviderTests
    {
        private InsertOperationMetricsCollector insertOperationMetricsCollector1;
        private InsertOperationMetricsCollector insertOperationMetricsCollector2;

        private ReadOperationMetricsCollector readOperationMetricsCollector1;
        private ReadOperationMetricsCollector readOperationMetricsCollector2;

        private QueryOperationMetricsCollector queryOperationMetricsCollector1;
        private QueryOperationMetricsCollector queryOperationMetricsCollector2;

        private MetricsCollectorProvider metricsCollectorProvider;

        [TestInitialize]
        public void Setup()
        {
            Mock<MeterProvider> meterProviderMock = new Mock<MeterProvider>();
            Mock<BenchmarkConfig> benchmarkConfigMock = new Mock<BenchmarkConfig>();
            this.metricsCollectorProvider = new MetricsCollectorProvider(meterProviderMock.Object, benchmarkConfigMock.Object);
        }

        public void InitInsertOperationMetricsCollector1()
        {
            this.insertOperationMetricsCollector1 = this.metricsCollectorProvider.InsertOperationMetricsCollector;
        }

        public void InitInsertOperationMetricsCollector2()
        {
            this.insertOperationMetricsCollector2 = this.metricsCollectorProvider.InsertOperationMetricsCollector;
        }

        public void InitReadOperationMetricsCollector1()
        {
            this.readOperationMetricsCollector1 = this.metricsCollectorProvider.ReadOperationMetricsCollector;
        }

        public void InitReadOperationMetricsCollector2()
        {
            this.readOperationMetricsCollector2 = this.metricsCollectorProvider.ReadOperationMetricsCollector;
        }

        public void InitQueryOperationMetricsCollector1()
        {
            this.queryOperationMetricsCollector1 = this.metricsCollectorProvider.QueryOperationMetricsCollector;
        }

        public void InitQueryOperationMetricsCollector2()
        {
            this.queryOperationMetricsCollector2 = this.metricsCollectorProvider.QueryOperationMetricsCollector;
        }

        [TestMethod]
        public void MetricsCollectorProvider_ProvidesSingletonInsertOperationMetricsCollector()
        {
            Thread thread1 = new Thread(this.InitInsertOperationMetricsCollector1);
            thread1.Start();

            Thread thread2 = new Thread(this.InitInsertOperationMetricsCollector2);
            thread2.Start();

            thread1.Join();
            thread2.Join();

            Assert.IsNotNull(this.insertOperationMetricsCollector1);
            Assert.AreSame(this.insertOperationMetricsCollector1, this.insertOperationMetricsCollector2);
        }

        [TestMethod]
        public void MetricsCollectorProvider_ProvidesSingletonReadOperationMetricsCollector()
        {
            Thread thread1 = new Thread(this.InitReadOperationMetricsCollector1);
            thread1.Start();

            Thread thread2 = new Thread(this.InitReadOperationMetricsCollector2);
            thread2.Start();

            thread1.Join();
            thread2.Join();

            Assert.IsNotNull(this.readOperationMetricsCollector1);
            Assert.AreSame(this.readOperationMetricsCollector1, this.readOperationMetricsCollector2);
        }

        [TestMethod]
        public void MetricsCollectorProvider_ProvidesSingletonQueryOperationMetricsCollector()
        {
            Thread thread1 = new Thread(this.InitQueryOperationMetricsCollector1);
            thread1.Start();

            Thread thread2 = new Thread(this.InitQueryOperationMetricsCollector2);
            thread2.Start();

            thread1.Join();
            thread2.Join();

            Assert.IsNotNull(this.queryOperationMetricsCollector1);
            Assert.AreSame(this.queryOperationMetricsCollector1, this.queryOperationMetricsCollector2);
        }
    }
}