namespace CosmosBenchmarkTests.Fx
{
    using CosmosBenchmark;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class MetricsCollectorTests
    {
        private Mock<IMetricsCollectorProvider> metricsCollectorProviderMock;
        private Mock<ReadBenchmarkOperation> benchmarkFailedOperationMock;
        private Mock<ReadBenchmarkOperation> benchmarkSuccessOperationMock;
        private SerialOperationExecutor serialOperationExecutor;
        private Mock<IMetricsCollector> metricsCollectorMock;

        [TestInitialize]
        public void Setup()
        {
            this.benchmarkFailedOperationMock = new Mock<ReadBenchmarkOperation>();
            this.benchmarkFailedOperationMock.Setup(mock => mock.ExecuteOnceAsync()).Throws(new ApplicationException("Test exception."));

            this.benchmarkSuccessOperationMock = new Mock<ReadBenchmarkOperation>();

            this.metricsCollectorMock = new Mock<IMetricsCollector>();

            this.metricsCollectorProviderMock = new Mock<IMetricsCollectorProvider>();
            this.metricsCollectorProviderMock.Setup(mock => mock.GetMetricsCollector(It.IsAny<BenchmarkOperation>())).Returns(metricsCollectorMock.Object);
        }

        [TestMethod]
        public void MetricsCollector_CollectsMetricsOnFailure()
        {
            this.serialOperationExecutor = new SerialOperationExecutor("TestExecutor", this.benchmarkFailedOperationMock.Object);

            this.serialOperationExecutor.ExecuteAsync(1, false, false, () => { }, new BenchmarkConfig(), this.metricsCollectorProviderMock.Object);
            this.benchmarkFailedOperationMock.Verify(mock => mock.ExecuteOnceAsync(), Times.Exactly(1));
            this.metricsCollectorMock.Verify(mock => mock.CollectMetricsOnFailure(), Times.Exactly(1));
            this.metricsCollectorMock.Verify(mock => mock.CollectMetricsOnSuccess(), Times.Exactly(0));
        }

        [TestMethod]
        public void MetricsCollector_CollectsMetricsOnSccess()
        {
            this.serialOperationExecutor = new SerialOperationExecutor("TestExecutor", this.benchmarkSuccessOperationMock.Object);

            this.serialOperationExecutor.ExecuteAsync(1, false, false, () => { }, new BenchmarkConfig(), this.metricsCollectorProviderMock.Object);
            this.benchmarkSuccessOperationMock.Verify(mock => mock.ExecuteOnceAsync(), Times.Exactly(1));
            this.metricsCollectorMock.Verify(mock => mock.CollectMetricsOnFailure(), Times.Exactly(0));
            this.metricsCollectorMock.Verify(mock => mock.CollectMetricsOnSuccess(), Times.Exactly(1));
        }
    }
}
