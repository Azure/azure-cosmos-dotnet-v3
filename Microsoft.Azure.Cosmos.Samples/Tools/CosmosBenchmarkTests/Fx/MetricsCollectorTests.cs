namespace CosmosBenchmarkTests.Fx
{
    using CosmosBenchmark;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using OpenTelemetry.Metrics;

    [TestClass]
    public class MetricsCollectorTests
    {
        private Mock<MeterProvider> meterProviderMock;
        private Mock<IBenchmarkOperation> benchmarkOperationMock;
        private SerialOperationExecutor serialOperationExecutor;
        private Mock<MetricsCollector> metricsCollectorMock;

        [TestInitialize]
        public void Setup()
        {
            this.meterProviderMock = new Mock<MeterProvider>();

            this.benchmarkOperationMock = new Mock<IBenchmarkOperation>();
            this.benchmarkOperationMock.Setup(mock => mock.ExecuteOnceAsync()).Throws(new ApplicationException("Test exception."));

            this.serialOperationExecutor = new SerialOperationExecutor("TestExecutor", this.benchmarkOperationMock.Object);
            this.metricsCollectorMock = new Mock<MetricsCollector>(); // new InsertOperationMetricsCollector(new Meter("TestMeter"));
        }

        [TestMethod]
        public void MetricsCollector_CollectsMetrics()
        {
            this.serialOperationExecutor.ExecuteAsync(1, false, false, () => { }, new BenchmarkConfig(), this.meterProviderMock.Object);
            this.benchmarkOperationMock.Verify(mock => mock.ExecuteOnceAsync(), Times.Exactly(1));
            this.metricsCollectorMock.Verify(mock => mock.CollectMetricsOnFailure(), Times.Exactly(1));
        }
    }
}
