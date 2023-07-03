namespace CosmosBenchmark.Fx.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MetricsCollectorProviderTests
    {
        InsertOperationMetricsCollector insertOperationMetricsCollector1;
        InsertOperationMetricsCollector insertOperationMetricsCollector2;

        ReadOperationMetricsCollector readOperationMetricsCollector1;
        ReadOperationMetricsCollector readOperationMetricsCollector2;

        QueryOperationMetricsCollector queryOperationMetricsCollector1;
        QueryOperationMetricsCollector queryOperationMetricsCollector2;

        public void InitInsertOperationMetricsCollector1()
        {
            this.insertOperationMetricsCollector1 = MetricsCollectorProvider.InsertOperationMetricsCollector;
        }

        public void InitInsertOperationMetricsCollector2()
        {
            this.insertOperationMetricsCollector2 = MetricsCollectorProvider.InsertOperationMetricsCollector;
        }

        public void InitReadOperationMetricsCollector1()
        {
            this.readOperationMetricsCollector1 = MetricsCollectorProvider.ReadOperationMetricsCollector;
        }

        public void InitReadOperationMetricsCollector2()
        {
            this.readOperationMetricsCollector2 = MetricsCollectorProvider.ReadOperationMetricsCollector;
        }

        public void InitQueryOperationMetricsCollector1()
        {
            this.queryOperationMetricsCollector1 = MetricsCollectorProvider.QueryOperationMetricsCollector;
        }

        public void InitQueryOperationMetricsCollector2()
        {
            this.queryOperationMetricsCollector2 = MetricsCollectorProvider.QueryOperationMetricsCollector;
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