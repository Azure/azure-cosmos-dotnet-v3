//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
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

        #region WithInMemoryLeaseContainer(pairs) Tests

        [TestMethod]
        public async Task WithInMemoryLeaseContainerWithPairsInitializesStoreCorrectly()
        {
            List<(string LeaseToken, string ContinuationToken)> initialLeases = new List<(string, string)>
            {
                ("0", "continuation0"),
                ("1", "continuation1"),
            };

            DocumentServiceLeaseStoreManager capturedManager = null;

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
                    capturedManager = leaseStoreManager;
                    Assert.IsInstanceOfType(leaseStoreManager, typeof(DocumentServiceLeaseStoreManagerInMemory));
                };

            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                verifier);

            builder.WithInMemoryLeaseContainer(initialLeases);
            builder.Build();

            Assert.IsNotNull(capturedManager);
            IReadOnlyList<DocumentServiceLease> allLeases = await capturedManager.LeaseContainer.GetAllLeasesAsync();
            Assert.AreEqual(2, allLeases.Count);
            Assert.IsTrue(allLeases.Any(l => l.CurrentLeaseToken == "0" && l.ContinuationToken == "continuation0"));
            Assert.IsTrue(allLeases.Any(l => l.CurrentLeaseToken == "1" && l.ContinuationToken == "continuation1"));
        }

        [TestMethod]
        public async Task WithInMemoryLeaseContainerWithEmptyPairsInitializesEmptyStore()
        {
            List<(string LeaseToken, string ContinuationToken)> initialLeases = new List<(string, string)>();

            DocumentServiceLeaseStoreManager capturedManager = null;

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
                    capturedManager = leaseStoreManager;
                };

            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                verifier);

            builder.WithInMemoryLeaseContainer(initialLeases);
            builder.Build();

            Assert.IsNotNull(capturedManager);
            IReadOnlyList<DocumentServiceLease> allLeases = await capturedManager.LeaseContainer.GetAllLeasesAsync();
            Assert.AreEqual(0, allLeases.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void WithInMemoryLeaseContainerWithNullPairsThrows()
        {
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.WithInMemoryLeaseContainer((IReadOnlyList<(string, string)>)null);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void WithInMemoryLeaseContainerWithPairsCannotCombineWithLeaseContainer()
        {
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.WithLeaseContainer(ChangeFeedProcessorBuilderTests.GetMockedContainer());
            builder.WithInMemoryLeaseContainer(new List<(string, string)> { ("0", "token0") });
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void WithInMemoryLeaseContainerWithPairsCannotCombineWithExistingInMemory()
        {
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.WithInMemoryLeaseContainer();
            builder.WithInMemoryLeaseContainer(new List<(string, string)> { ("0", "token0") });
        }

        #endregion

        #region WithInMemoryLeaseContainer(leaseExportPath) Tests

        [TestMethod]
        public async Task WithInMemoryLeaseContainerWithFileInitializesStoreCorrectly()
        {
            // Create a temp file with exported lease data
            string tempFile = Path.GetTempFileName();
            try
            {
                DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore
                {
                    LeaseId = "file-lease",
                    LeaseToken = "0",
                    ContinuationToken = "file-continuation",
                    Owner = "file-owner",
                };

                // Use the real export path to create the file, matching what
                // ExportLeasesAsync produces and what the builder's file import expects.
                ConcurrentDictionary<string, DocumentServiceLease> sourceContainer = new ConcurrentDictionary<string, DocumentServiceLease>();
                sourceContainer.TryAdd(lease.Id, lease);
                DocumentServiceLeaseContainerInMemory source = new DocumentServiceLeaseContainerInMemory(sourceContainer);
                IReadOnlyList<JsonElement> exported = await source.ExportLeasesAsync();
                string json = JsonSerializer.Serialize(exported);
                File.WriteAllText(tempFile, json);

                DocumentServiceLeaseStoreManager capturedManager = null;

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
                        capturedManager = leaseStoreManager;
                        Assert.IsInstanceOfType(leaseStoreManager, typeof(DocumentServiceLeaseStoreManagerInMemory));
                    };

                ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                    ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                    ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                    verifier);

                builder.WithInMemoryLeaseContainer(tempFile);
                builder.Build();

                Assert.IsNotNull(capturedManager);
                IReadOnlyList<DocumentServiceLease> allLeases = await capturedManager.LeaseContainer.GetAllLeasesAsync();
                Assert.AreEqual(1, allLeases.Count);
                Assert.AreEqual("0", allLeases[0].CurrentLeaseToken);
                Assert.AreEqual("file-continuation", allLeases[0].ContinuationToken);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void WithInMemoryLeaseContainerWithNullPathThrows()
        {
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.WithInMemoryLeaseContainer((string)null);
        }

        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public void WithInMemoryLeaseContainerWithNonExistentFileThrows()
        {
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.WithInMemoryLeaseContainer("non_existent_file.json");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void WithInMemoryLeaseContainerWithFileCannotCombineWithLeaseContainer()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "[]");

                ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                    ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                    ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                    ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

                builder.WithLeaseContainer(ChangeFeedProcessorBuilderTests.GetMockedContainer());
                builder.WithInMemoryLeaseContainer(tempFile);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        #endregion

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