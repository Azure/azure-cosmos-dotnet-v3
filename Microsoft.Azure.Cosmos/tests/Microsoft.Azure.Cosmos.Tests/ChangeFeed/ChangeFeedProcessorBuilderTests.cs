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
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedProcessorBuilderTests
    {
        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        public void AllContainerBuilderShapesAcceptQuery(int shape)
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Container container = client.GetContainer("myDb", "myContainer");
            ChangeFeedProcessorBuilder builder = shape switch
            {
                0 => container.GetChangeFeedProcessorBuilder<JObject>("filtered", (changes, token) => Task.CompletedTask),
                1 => container.GetChangeFeedProcessorBuilder<JObject>("filtered", (context, changes, token) => Task.CompletedTask),
                2 => container.GetChangeFeedProcessorBuilderWithManualCheckpoint<JObject>("filtered", (context, changes, checkpoint, token) => Task.CompletedTask),
                3 => container.GetChangeFeedProcessorBuilder("filtered", (context, changes, token) => Task.CompletedTask),
                4 => container.GetChangeFeedProcessorBuilderWithManualCheckpoint("filtered", (context, changes, checkpoint, token) => Task.CompletedTask),
                5 => container.GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes<JObject>("filtered", (context, changes, token) => Task.CompletedTask),
                _ => throw new ArgumentOutOfRangeException(nameof(shape))
            };
            Assert.IsNotNull(builder.WithQuery("SELECT * FROM c").WithInMemoryLeaseContainer().Build());
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void WithQueryPreservesExactSqlAndBecomesImmutableAfterBuild(bool allVersionsAndDeletes)
        {
            const string query = " SELECT * FROM c WHERE c.enabled = true ";
            ChangeFeedProcessorOptions captured = null;
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder(
                "filtered", GetMockedContainer(), GetMockedProcessor(),
                (store, leases, instance, leaseOptions, options, container) => captured = options);
            ChangeFeedMode mode = allVersionsAndDeletes ? ChangeFeedMode.AllVersionsAndDeletes : ChangeFeedMode.LatestVersion;
            builder.WithChangeFeedMode(mode);
            Assert.AreSame(builder, builder.WithQuery("SELECT * FROM c"));
            Assert.AreSame(builder, builder.WithQuery(query));
            builder.WithInMemoryLeaseContainer().Build();

            Assert.AreEqual(query, captured.QuerySpec.QueryText);
            Assert.AreEqual(mode, captured.Mode);
            Assert.ThrowsException<InvalidOperationException>(() => builder.WithQuery("SELECT * FROM c"));
            Assert.AreEqual(query, captured.QuerySpec.QueryText);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" \r\n\t")]
        public void WithQueryRejectsInvalidTextWithoutReplacingConfiguration(string query)
        {
            ChangeFeedProcessorOptions captured = null;
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder(
                "filtered", GetMockedContainer(), GetMockedProcessor(),
                (store, leases, instance, leaseOptions, options, container) => captured = options);
            builder.WithQuery("SELECT * FROM c");
            if (query == null)
            {
                Assert.ThrowsException<ArgumentNullException>(() => builder.WithQuery(query));
            }
            else
            {
                Assert.ThrowsException<ArgumentException>(() => builder.WithQuery(query));
            }

            builder.WithInMemoryLeaseContainer().Build();
            Assert.AreEqual("SELECT * FROM c", captured.QuerySpec.QueryText);
        }

        [TestMethod]
        public void WithoutQueryRetainsUnfilteredConfiguration()
        {
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder(
                "unfiltered", GetMockedContainer(), GetMockedProcessor(),
                (store, leases, instance, leaseOptions, options, container) => Assert.IsNull(options.QuerySpec));
            builder.WithInMemoryLeaseContainer().Build();
        }

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

        #region WithInMemoryLeaseContainer(MemoryStream) Tests

        [TestMethod]
        public async Task WithInMemoryLeaseContainerWithStreamInitializesStoreCorrectly()
        {
            // Build a MemoryStream with lease data
            DocumentServiceLeaseCoreEpk lease = new DocumentServiceLeaseCoreEpk
            {
                LeaseId = "stream-lease",
                LeaseToken = "0",
                ContinuationToken = "stream-continuation",
                Owner = "stream-owner",
                FeedRange = new FeedRangeEpk(new Range<string>("", "FF", true, false))
            };

            ConcurrentDictionary<string, DocumentServiceLease> sourceContainer = new ConcurrentDictionary<string, DocumentServiceLease>();
            sourceContainer.TryAdd(lease.Id, lease);
            MemoryStream leaseState = new MemoryStream();
            DocumentServiceLeaseContainerInMemory source = new DocumentServiceLeaseContainerInMemory(sourceContainer, leaseState);
            await source.ShutdownAsync();

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

            builder.WithInMemoryLeaseContainer(leaseState);
            builder.Build();

            Assert.IsNotNull(capturedManager);
            IReadOnlyList<DocumentServiceLease> allLeases = await capturedManager.LeaseContainer.GetAllLeasesAsync();
            Assert.AreEqual(1, allLeases.Count);
            Assert.AreEqual("0", allLeases[0].CurrentLeaseToken);
            Assert.AreEqual("stream-continuation", allLeases[0].ContinuationToken);
        }

        [TestMethod]
        public async Task WithInMemoryLeaseContainerWithEmptyStreamInitializesEmptyStore()
        {
            MemoryStream leaseState = new MemoryStream();

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
                    Container monitoredContainer) => capturedManager = leaseStoreManager;

            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                verifier);

            builder.WithInMemoryLeaseContainer(leaseState);
            builder.Build();

            Assert.IsNotNull(capturedManager);
            IReadOnlyList<DocumentServiceLease> allLeases = await capturedManager.LeaseContainer.GetAllLeasesAsync();
            Assert.AreEqual(0, allLeases.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void WithInMemoryLeaseContainerWithNullStreamThrows()
        {
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.WithInMemoryLeaseContainer((MemoryStream)null);
        }

        [TestMethod]
        public async Task WithInMemoryLeaseContainer_FullLifecycle_RestoreProcessStopPersist()
        {
            // Arrange — create initial lease state in a stream
            DocumentServiceLeaseCoreEpk originalLease = new DocumentServiceLeaseCoreEpk
            {
                LeaseId = "lifecycle-lease",
                LeaseToken = "0",
                ContinuationToken = "original-continuation",
                Owner = "original-owner",
                FeedRange = new FeedRangeEpk(new Range<string>("", "FF", true, false))
            };

            ConcurrentDictionary<string, DocumentServiceLease> seedContainer = new ConcurrentDictionary<string, DocumentServiceLease>();
            seedContainer.TryAdd(originalLease.Id, originalLease);
            MemoryStream leaseState = new MemoryStream();
            DocumentServiceLeaseContainerInMemory seed = new DocumentServiceLeaseContainerInMemory(seedContainer, leaseState);
            await seed.ShutdownAsync();

            // Act — build with the populated stream, capturing the store manager
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

            builder.WithInMemoryLeaseContainer(leaseState);
            builder.Build();

            // Verify leases were restored
            Assert.IsNotNull(capturedManager);
            IReadOnlyList<DocumentServiceLease> restoredLeases = await capturedManager.LeaseContainer.GetAllLeasesAsync();
            Assert.AreEqual(1, restoredLeases.Count);
            Assert.AreEqual("original-continuation", restoredLeases[0].ContinuationToken);

            // Simulate stop — persist state back to the same stream
            await capturedManager.ShutdownAsync();

            // Assert — stream is still usable and contains valid serialized state
            Assert.IsTrue(leaseState.CanRead, "Stream should still be readable after ShutdownAsync");
            Assert.IsTrue(leaseState.Length > 0, "Stream should contain serialized lease data");

            // Verify the persisted data round-trips correctly
            leaseState.Position = 0;
            using (StreamReader sr = new StreamReader(leaseState, leaveOpen: true))
            using (JsonTextReader jsonReader = new JsonTextReader(sr))
            {
                List<DocumentServiceLease> persisted = JsonSerializer.Create().Deserialize<List<DocumentServiceLease>>(jsonReader);

                Assert.AreEqual(1, persisted.Count);
                Assert.AreEqual("lifecycle-lease", persisted[0].Id);
                Assert.AreEqual("original-continuation", persisted[0].ContinuationToken);
                Assert.IsNotNull(persisted[0].FeedRange);
            }
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void WithInMemoryLeaseContainerWithCorruptedStreamThrows()
        {
            byte[] garbage = System.Text.Encoding.UTF8.GetBytes("not valid json {{{");
            MemoryStream corruptedStream = new MemoryStream();
            corruptedStream.Write(garbage, 0, garbage.Length);
            corruptedStream.Position = 0;

            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.WithInMemoryLeaseContainer(corruptedStream);
        }

        [TestMethod]
        public async Task WithInMemoryLeaseContainerWithEmptyArrayStreamInitializesEmptyStore()
        {
            byte[] emptyArray = System.Text.Encoding.UTF8.GetBytes("[]");
            MemoryStream stream = new MemoryStream();
            stream.Write(emptyArray, 0, emptyArray.Length);
            stream.Position = 0;

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

            builder.WithInMemoryLeaseContainer(stream);
            builder.Build();

            Assert.IsNotNull(capturedManager);
            IReadOnlyList<DocumentServiceLease> allLeases = await capturedManager.LeaseContainer.GetAllLeasesAsync();
            Assert.AreEqual(0, allLeases.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void WithInMemoryLeaseContainerWithNullLeaseEntryThrows()
        {
            byte[] nullEntry = System.Text.Encoding.UTF8.GetBytes("[null]");
            MemoryStream stream = new MemoryStream();
            stream.Write(nullEntry, 0, nullEntry.Length);
            stream.Position = 0;

            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.WithInMemoryLeaseContainer(stream);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void WithInMemoryLeaseContainerWithEmptyLeaseIdThrows()
        {
            byte[] emptyId = System.Text.Encoding.UTF8.GetBytes("[{\"id\":\"\"}]");
            MemoryStream stream = new MemoryStream();
            stream.Write(emptyId, 0, emptyId.Length);
            stream.Position = 0;

            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.WithInMemoryLeaseContainer(stream);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void WithInMemoryLeaseContainerWithReadOnlyStreamThrows()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("[]");
            MemoryStream readOnlyStream = new MemoryStream(data, writable: false);

            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            builder.WithInMemoryLeaseContainer(readOnlyStream);
        }

        [TestMethod]
        public void WithInMemoryLeaseContainerWithCorruptedStreamThrowsInvalidOperation()
        {
            byte[] corruptedData = System.Text.Encoding.UTF8.GetBytes("this is not valid JSON{{{");
            MemoryStream corruptedStream = new MemoryStream();
            corruptedStream.Write(corruptedData, 0, corruptedData.Length);
            corruptedStream.Position = 0;

            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder("workflowName",
                ChangeFeedProcessorBuilderTests.GetMockedContainer(),
                ChangeFeedProcessorBuilderTests.GetMockedProcessor(),
                ChangeFeedProcessorBuilderTests.GetEmptyInitialization());

            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(
                () => builder.WithInMemoryLeaseContainer(corruptedStream));

            Assert.IsTrue(ex.Message.Contains("Failed to deserialize lease state"));
            Assert.IsNotNull(ex.InnerException);
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