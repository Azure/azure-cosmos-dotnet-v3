//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedProcessorBuilderTests
    {
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void WorkFlowName_IsRequired()
        {
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder(null,
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.Build();
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void MonitoredContainer_IsRequired()
        {
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                null,
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.Build();
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void MonitoredContainer_LeaseStore_IsRequired()
        {
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.Build();
        }

        [TestMethod]
        public void WithLeaseContainerPassesCorrectValues()
        {
            Container leaseContainerForBuilder = ChangeFeedProcessorBuilderTests.GetMockedContainer("leases");

            Action<DocumentServiceLeaseStoreManager,
                Container,
                string,
                ChangeFeedLeaseOptions,
                ChangeFeedProcessorOptions,
                Container> verifier = (DocumentServiceLeaseStoreManager leaseStoreManager,
                Container leaseContainer,
                string instanceName,
                ChangeFeedLeaseOptions changeFeedLeaseOptions,
                ChangeFeedProcessorOptions changeFeedProcessorOptions,
                Container monitoredContainer) => Assert.AreEqual(leaseContainerForBuilder.Id, leaseContainer.Id);

            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                verifier);

            builder.WithLeaseContainer(leaseContainerForBuilder);

            builder.Build();
        }

        [TestMethod]
        public void WithInMemoryLeaseContainerInitializesStoreCorrectly()
        {
            Action<DocumentServiceLeaseStoreManager,
                Container,
                string,
                ChangeFeedLeaseOptions,
                ChangeFeedProcessorOptions,
                Container> verifier = (DocumentServiceLeaseStoreManager leaseStoreManager,
                Container leaseContainer,
                string instanceName,
                ChangeFeedLeaseOptions changeFeedLeaseOptions,
                ChangeFeedProcessorOptions changeFeedProcessorOptions,
                Container monitoredContainer) => Assert.IsInstanceOfType(leaseStoreManager, typeof(DocumentServiceLeaseStoreManagerInMemory));

            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                verifier);

            builder.WithInMemoryLeaseContainer();

            builder.Build();
        }

        [TestMethod]
        public void WithInstanceNameCorrectlyPassesParameters()
        {
            string myInstance = "myInstance";
            Action<DocumentServiceLeaseStoreManager,
                Container,
                string,
                ChangeFeedLeaseOptions,
                ChangeFeedProcessorOptions,
                Container> verifier = (DocumentServiceLeaseStoreManager leaseStoreManager,
                Container leaseContainer,
                string instanceName,
                ChangeFeedLeaseOptions changeFeedLeaseOptions,
                ChangeFeedProcessorOptions changeFeedProcessorOptions,
                Container monitoredContainer) => Assert.AreEqual(myInstance, instanceName);

            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                verifier);

            builder.WithInMemoryLeaseContainer();
            builder.WithInstanceName(myInstance);

            builder.Build();
        }

        [TestMethod]
        public void WithLeaseConfigurationFillsCorrectValues()
        {
            TimeSpan acquireInterval = TimeSpan.FromSeconds(1);
            TimeSpan expirationInterval = TimeSpan.FromSeconds(2);
            TimeSpan renewInterval = TimeSpan.FromSeconds(3);
            string workflowName = "workflowName";


            Action<DocumentServiceLeaseStoreManager,
                Container,
                string,
                ChangeFeedLeaseOptions,
                ChangeFeedProcessorOptions,
                Container> verifier = (DocumentServiceLeaseStoreManager leaseStoreManager,
                Container leaseContainer,
                string instanceName,
                ChangeFeedLeaseOptions changeFeedLeaseOptions,
                ChangeFeedProcessorOptions changeFeedProcessorOptions,
                Container monitoredContainer) =>
                {
                    Assert.AreEqual(workflowName, changeFeedLeaseOptions.LeasePrefix);
                    Assert.AreEqual(acquireInterval, changeFeedLeaseOptions.LeaseAcquireInterval);
                    Assert.AreEqual(expirationInterval, changeFeedLeaseOptions.LeaseExpirationInterval);
                    Assert.AreEqual(renewInterval, changeFeedLeaseOptions.LeaseRenewInterval);
                };

            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder(workflowName,
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                verifier);

            builder.WithLeaseContainer(ChangeFeedProcessorBuilderTests.GetMockedContainer());
            builder.WithLeaseConfiguration(acquireInterval, expirationInterval, renewInterval);

            builder.Build();
        }

        [TestMethod]
        public void CannotBuildTwice()
        {
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.WithLeaseContainer(ChangeFeedProcessorBuilderTests.GetMockedContainer());

            // This build should not throw
            builder.Build();

            // This one should
            Assert.ThrowsException<InvalidOperationException>(() => builder.Build());
        }

        [TestMethod]
        public void CanBuildWithCosmosLeaseContainer()
        {
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.WithLeaseContainer(ChangeFeedProcessorBuilderTests.GetMockedContainer());

            Assert.IsInstanceOfType(builder.Build(), typeof(ChangeFeedProcessor));
        }

        [TestMethod]
        public void CanBuildWithInMemoryContainer()
        {
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.WithLeaseContainer(ChangeFeedProcessorBuilderTests.GetMockedContainer());

            Assert.IsInstanceOfType(builder.Build(), typeof(ChangeFeedProcessor));
        }

        [TestMethod]
        public void ConvertsToUTC()
        {
            DateTime localTime = DateTime.Now;

            Assert.AreEqual(DateTimeKind.Local, localTime.Kind);

            Action<DocumentServiceLeaseStoreManager,
                Container,
                string,
                ChangeFeedLeaseOptions,
                ChangeFeedProcessorOptions,
                Container> verifier = (DocumentServiceLeaseStoreManager leaseStoreManager,
                Container leaseContainer,
                string instanceName,
                ChangeFeedLeaseOptions changeFeedLeaseOptions,
                ChangeFeedProcessorOptions changeFeedProcessorOptions,
                Container monitoredContainer) =>
                {
                    Assert.AreEqual(DateTimeKind.Utc, changeFeedProcessorOptions.StartTime.Value.Kind);
                    Assert.AreEqual(localTime.ToUniversalTime(), changeFeedProcessorOptions.StartTime.Value);
                };

            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                verifier);

            builder.WithLeaseContainer(ChangeFeedProcessorBuilderTests.GetMockedContainer());

            builder.WithStartTime(localTime);

            Assert.IsInstanceOfType(builder.Build(), typeof(ChangeFeedProcessor));
        }

        private static ContainerInternal GetMockedContainer(string containerName = null)
        {
            Mock<ContainerInternal> mockedContainer = MockCosmosUtil.CreateMockContainer(containerName: containerName);
            return mockedContainer.Object;
        }

        private static ChangeFeedProcessor GetMockedProcessor()
        {
            Mock<ChangeFeedProcessor> mockedChangeFeedProcessor = new Mock<ChangeFeedProcessor>();
            return mockedChangeFeedProcessor.Object;
        }

        private static Action<DocumentServiceLeaseStoreManager, Container, string, ChangeFeedLeaseOptions, ChangeFeedProcessorOptions, Container> GetEmptyInitialization()
        {
            return (DocumentServiceLeaseStoreManager leaseStoreManager,
                Container leaseContainer,
                string instanceName,
                ChangeFeedLeaseOptions changeFeedLeaseOptions,
                ChangeFeedProcessorOptions changeFeedProcessorOptions,
                Container monitoredContainer) =>
            { };
        }
    }
}