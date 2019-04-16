//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
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
            CosmosContainer leaseContainerForBuilder = ChangeFeedProcessorBuilderTests.GetMockedContainer("leases");

            Action<DocumentServiceLeaseStoreManager,
                CosmosContainer,
                string,
                string,
                ChangeFeedLeaseOptions,
                ChangeFeedProcessorOptions,
                CosmosContainer> verifier = (DocumentServiceLeaseStoreManager leaseStoreManager,
                CosmosContainer leaseContainer,
                string leaseContainerPrefix,
                string instanceName,
                ChangeFeedLeaseOptions changeFeedLeaseOptions,
                ChangeFeedProcessorOptions changeFeedProcessorOptions,
                CosmosContainer monitoredContainer) =>
                {
                    Assert.AreEqual(leaseContainerForBuilder, leaseContainer);
                };
            
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                verifier);

            builder.WithCosmosLeaseContainer(leaseContainerForBuilder);

            builder.Build();
        }

        [TestMethod]
        public void WithInMemoryLeaseContainerInitializesStoreCorrectly()
        {
            Action<DocumentServiceLeaseStoreManager,
                CosmosContainer,
                string,
                string,
                ChangeFeedLeaseOptions,
                ChangeFeedProcessorOptions,
                CosmosContainer> verifier = (DocumentServiceLeaseStoreManager leaseStoreManager,
                CosmosContainer leaseContainer,
                string leaseContainerPrefix,
                string instanceName,
                ChangeFeedLeaseOptions changeFeedLeaseOptions,
                ChangeFeedProcessorOptions changeFeedProcessorOptions,
                CosmosContainer monitoredContainer) =>
                {
                    Assert.IsInstanceOfType(leaseStoreManager, typeof(DocumentServiceLeaseStoreManagerInMemory));
                };

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
                CosmosContainer,
                string,
                string,
                ChangeFeedLeaseOptions,
                ChangeFeedProcessorOptions,
                CosmosContainer> verifier = (DocumentServiceLeaseStoreManager leaseStoreManager,
                CosmosContainer leaseContainer,
                string leaseContainerPrefix,
                string instanceName,
                ChangeFeedLeaseOptions changeFeedLeaseOptions,
                ChangeFeedProcessorOptions changeFeedProcessorOptions,
                CosmosContainer monitoredContainer) =>
                {
                    Assert.AreEqual(myInstance, instanceName);
                };

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
                CosmosContainer,
                string,
                string,
                ChangeFeedLeaseOptions,
                ChangeFeedProcessorOptions,
                CosmosContainer> verifier = (DocumentServiceLeaseStoreManager leaseStoreManager,
                CosmosContainer leaseContainer,
                string leaseContainerPrefix,
                string instanceName,
                ChangeFeedLeaseOptions changeFeedLeaseOptions,
                ChangeFeedProcessorOptions changeFeedProcessorOptions,
                CosmosContainer monitoredContainer) =>
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

            builder.WithCosmosLeaseContainer(ChangeFeedProcessorBuilderTests.GetMockedContainer());
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

            builder.WithCosmosLeaseContainer(ChangeFeedProcessorBuilderTests.GetMockedContainer());

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

            builder.WithCosmosLeaseContainer(ChangeFeedProcessorBuilderTests.GetMockedContainer());

            Assert.IsInstanceOfType(builder.Build(), typeof(ChangeFeedProcessor));
        }

        [TestMethod]
        public void CanBuildWithInMemoryContainer()
        {
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.WithCosmosLeaseContainer(ChangeFeedProcessorBuilderTests.GetMockedContainer());

            Assert.IsInstanceOfType(builder.Build(), typeof(ChangeFeedProcessor));
        }

        private static CosmosContainer GetMockedContainer(string containerName = "myColl")
        {
            Mock<CosmosContainer> mockedContainer = new Mock<CosmosContainer>();
            mockedContainer.Setup(c => c.LinkUri).Returns(new Uri("/dbs/myDb/colls/" + containerName, UriKind.Relative));
            mockedContainer.Setup(c => c.Client).Returns(ChangeFeedProcessorBuilderTests.GetMockedClient());
            return mockedContainer.Object;
        }

        private static CosmosClient GetMockedClient()
        {
            return MockDocumentClient.CreateMockCosmosClient();
        }

        private static ChangeFeedProcessor GetMockedProcessor()
        {
            Mock<ChangeFeedProcessor> mockedChangeFeedProcessor = new Mock<ChangeFeedProcessor>();
            return mockedChangeFeedProcessor.Object;
        }

        private static Action<DocumentServiceLeaseStoreManager, CosmosContainer, string, string, ChangeFeedLeaseOptions, ChangeFeedProcessorOptions, CosmosContainer> GetEmptyInitialization()
        {
            return (DocumentServiceLeaseStoreManager leaseStoreManager, 
                CosmosContainer leaseContainer, 
                string leaseContainerPrefix, 
                string instanceName, 
                ChangeFeedLeaseOptions changeFeedLeaseOptions,
                ChangeFeedProcessorOptions changeFeedProcessorOptions,
                CosmosContainer monitoredContainer) => { };
        }
    }
}
