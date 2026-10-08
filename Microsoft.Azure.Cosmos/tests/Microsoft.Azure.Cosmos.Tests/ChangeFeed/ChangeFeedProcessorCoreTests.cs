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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedProcessorCoreTests
    {

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ApplyBuildConfiguration_ValidatesNullStore()
        {
            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                null,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ApplyBuildConfiguration_ValidatesNullInstance()
        {
            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(Mock.Of<DocumentServiceLeaseContainer>);

            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                null,
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ApplyBuildConfiguration_ValidatesNullMonitoredContainer()
        {
            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(Mock.Of<DocumentServiceLeaseContainer>);

            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ApplyBuildConfiguration_ValidatesCustomStoreWithNullLeaseContainer()
        {
            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns((DocumentServiceLeaseContainer)null);

            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        public void ApplyBuildConfiguration_ValidCustomStore()
        {
            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(Mock.Of<DocumentServiceLeaseContainer>);

            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        public void ApplyBuildConfiguration_ValidContainerStore()
        {
            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                null,
                ChangeFeedProcessorCoreTests.GetMockedContainer("leases"),
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        public async Task StartAsync()
        {
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).Returns(Task.FromResult(Enumerable.Empty<DocumentServiceLease>()));
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(new List<DocumentServiceLease>());

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);
            ChangeFeedProcessorCore processor = null;
            try
            {
                processor = ChangeFeedProcessorCoreTests.CreateProcessor(out Mock<ChangeFeedObserverFactory> factory, out Mock<ChangeFeedObserver> observer);
                processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

                await processor.StartAsync();
                Mock.Get(leaseStore.Object)
                    .Verify(store => store.IsInitializedAsync(), Times.Once);
                Mock.Get(leaseContainer.Object)
                    .Verify(store => store.GetOwnedLeasesAsync(), Times.Once);
            }
            finally
            {
                if (processor != null)
                {
                    await processor.StopAsync();
                }
            }
        }

        [TestMethod]
        public async Task StartAsync_SetsStartTime_WhenNoStartOptionsProvided()
        {
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).Returns(Task.FromResult(Enumerable.Empty<DocumentServiceLease>()));
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(new List<DocumentServiceLease>());

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);

            ChangeFeedProcessorOptions options = new ChangeFeedProcessorOptions();
            ChangeFeedProcessorCore processor = null;
            try
            {
                processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
                processor.ApplyBuildConfiguration(
                    leaseStoreManager.Object,
                    null,
                    "instanceName",
                    new ChangeFeedLeaseOptions(),
                    options,
                    ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

                await processor.StartAsync();

                Assert.IsTrue(options.StartTime.HasValue);
                Assert.AreEqual(DateTimeKind.Utc, options.StartTime.Value.Kind);
            }
            finally
            {
                if (processor != null)
                {
                    await processor.StopAsync();
                }
            }
        }

        [TestMethod]
        public async Task StartAsync_DoesNotOverrideExplicitStartTime()
        {
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).Returns(Task.FromResult(Enumerable.Empty<DocumentServiceLease>()));
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(new List<DocumentServiceLease>());

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);

            DateTime explicitStartTime = DateTime.UtcNow.AddMinutes(-5);
            ChangeFeedProcessorOptions options = new ChangeFeedProcessorOptions
            {
                StartTime = explicitStartTime,
            };

            ChangeFeedProcessorCore processor = null;
            try
            {
                processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
                processor.ApplyBuildConfiguration(
                    leaseStoreManager.Object,
                    null,
                    "instanceName",
                    new ChangeFeedLeaseOptions(),
                    options,
                    ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

                await processor.StartAsync();

                Assert.AreEqual(explicitStartTime, options.StartTime);
            }
            finally
            {
                if (processor != null)
                {
                    await processor.StopAsync();
                }
            }
        }

        [TestMethod]
        public async Task StartAsync_DoesNotSetStartTime_WhenStartFromBeginning()
        {
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).Returns(Task.FromResult(Enumerable.Empty<DocumentServiceLease>()));
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(new List<DocumentServiceLease>());

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);

            ChangeFeedProcessorOptions options = new ChangeFeedProcessorOptions
            {
                StartFromBeginning = true,
            };

            ChangeFeedProcessorCore processor = null;
            try
            {
                processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
                processor.ApplyBuildConfiguration(
                    leaseStoreManager.Object,
                    null,
                    "instanceName",
                    new ChangeFeedLeaseOptions(),
                    options,
                    ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

                await processor.StartAsync();

                Assert.IsNull(options.StartTime);
            }
            finally
            {
                if (processor != null)
                {
                    await processor.StopAsync();
                }
            }
        }

        // Defends #5268 (https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5268) — AC7.
        // Symmetric companion to the AVAD test below: explicitly sets Mode = LatestVersion (rather
        // than relying on the default at ChangeFeedProcessorOptions.cs) so that a future contributor
        // over-broadening the Mode != AllVersionsAndDeletes guard regresses #5268 loudly.
        //
        // Assertion uses start/end-of-call bracket bounds instead of a fixed-tolerance window so the
        // test cannot be invalidated by CI slowness (a future flake here would silently quarantine
        // the regression fence — see PR #5852 review for context).
        [TestMethod]
        public async Task StartAsync_SetsStartTime_WhenLatestVersionMode_Explicit()
        {
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).Returns(Task.FromResult(Enumerable.Empty<DocumentServiceLease>()));
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(new List<DocumentServiceLease>());

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);

            ChangeFeedProcessorOptions options = new ChangeFeedProcessorOptions
            {
                Mode = ChangeFeedMode.LatestVersion,
            };

            ChangeFeedProcessorCore processor = null;
            try
            {
                processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
                processor.ApplyBuildConfiguration(
                    leaseStoreManager.Object,
                    null,
                    "instanceName",
                    new ChangeFeedLeaseOptions(),
                    options,
                    ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

                DateTime lowerBound = DateTime.UtcNow.AddSeconds(-1);

                await processor.StartAsync();

                DateTime upperBound = DateTime.UtcNow.AddSeconds(-1).AddMilliseconds(1);

                Assert.IsTrue(options.StartTime.HasValue);
                Assert.AreEqual(DateTimeKind.Utc, options.StartTime.Value.Kind);
                Assert.IsTrue(
                    options.StartTime.Value >= lowerBound,
                    $"StartTime {options.StartTime.Value:O} is earlier than pre-call lowerBound {lowerBound:O}.");
                Assert.IsTrue(
                    options.StartTime.Value <= upperBound,
                    $"StartTime {options.StartTime.Value:O} is later than post-call upperBound {upperBound:O}.");
            }
            finally
            {
                if (processor != null)
                {
                    await processor.StopAsync();
                }
            }
        }

        // Defends #5268 (https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5268) AND
        // #5846 (https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5846).
        //
        // AVAD push processor cold-start MUST NOT have StartTime backfilled by the #5617 guard:
        //   1. AVAD uses LSN-based continuation (IfNoneMatch: *), not RFC1123 IfModifiedSince,
        //      so the seconds-precision rounding issue from #5268 does not apply.
        //   2. The AVAD endpoint rejects an explicit StartTime on a null-continuation lease with
        //      HTTP 400 — the regression introduced by #5617 and tracked as #5846.
        //
        // Paired with the LatestVersion test above, this forms a dual-fence: dropping the mode
        // guard regresses #5846 (this test fails); over-broadening it regresses #5268 (the
        // LatestVersion test fails). Do not delete either without re-validating both issues.
        // Functional guard landed via PR #5825; this PR (#5852) carries the cited comment fence
        // and customer-facing changelog entry.
        [TestMethod]
        public async Task StartAsync_DoesNotSetStartTime_WhenAllVersionsAndDeletes()
        {
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).Returns(Task.FromResult(Enumerable.Empty<DocumentServiceLease>()));
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(new List<DocumentServiceLease>());

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);

            ChangeFeedProcessorOptions options = new ChangeFeedProcessorOptions
            {
                Mode = ChangeFeedMode.AllVersionsAndDeletes,
            };

            ChangeFeedProcessorCore processor = null;
            try
            {
                processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
                processor.ApplyBuildConfiguration(
                    leaseStoreManager.Object,
                    null,
                    "instanceName",
                    new ChangeFeedLeaseOptions(),
                    options,
                    ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

                await processor.StartAsync();

                Assert.IsNull(
                    options.StartTime,
                    "StartTime must remain null for AllVersionsAndDeletes mode (see leading comment).");
            }
            finally
            {
                if (processor != null)
                {
                    await processor.StopAsync();
                }
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task StartAsync_ThrowsWhenAllVersionsAndDeletes_WithStartFromBeginning()
        {
            ChangeFeedProcessorOptions options = new ChangeFeedProcessorOptions
            {
                Mode = ChangeFeedMode.AllVersionsAndDeletes,
                StartFromBeginning = true,
            };

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(Mock.Of<DocumentServiceLeaseContainer>);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(Mock.Of<DocumentServiceLeaseStore>);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);

            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                options,
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

            await processor.StartAsync();
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task StartAsync_ThrowsWhenAllVersionsAndDeletes_WithStartTime()
        {
            ChangeFeedProcessorOptions options = new ChangeFeedProcessorOptions
            {
                Mode = ChangeFeedMode.AllVersionsAndDeletes,
                StartTime = DateTime.UtcNow,
            };

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(Mock.Of<DocumentServiceLeaseContainer>);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(Mock.Of<DocumentServiceLeaseStore>);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);

            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                options,
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

            await processor.StartAsync();
        }

        [TestMethod]
        public async Task ObserverIsCreated()
        {
            IEnumerable<DocumentServiceLease> ownedLeases = new List<DocumentServiceLease>()
            {
                new DocumentServiceLeaseCore()
                {
                    LeaseId = "0",
                    LeaseToken = "0"
                }
            };

            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).Returns(Task.FromResult(ownedLeases));
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(new List<DocumentServiceLease>());

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);
            ChangeFeedProcessorCore processor = null;
            try
            {
                processor = ChangeFeedProcessorCoreTests.CreateProcessor(out Mock<ChangeFeedObserverFactory> factory, out Mock<ChangeFeedObserver> observer);
                processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

                await processor.StartAsync();

                Mock.Get(factory.Object)
                    .Verify(mock => mock.CreateObserver(), Times.Once);

                Mock.Get(observer.Object)
                    .Verify(mock => mock.OpenAsync(It.Is<string>((lt) => lt == ownedLeases.First().CurrentLeaseToken)), Times.Once);
            }
            finally
            {
                if (processor != null)
                {
                    await processor.StopAsync();
                }
            }
        }

        [TestMethod]
        public async Task StopAsync()
        {
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).Returns(Task.FromResult(Enumerable.Empty<DocumentServiceLease>()));
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(new List<DocumentServiceLease>());

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);
            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out Mock<ChangeFeedObserverFactory> factory, out Mock<ChangeFeedObserver> observer);
            processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

            await processor.StartAsync();

            await processor.StopAsync();
            Mock.Get(leaseContainer.Object)
                .Verify(store => store.GetAllLeasesAsync(), Times.Exactly(2));
        }

        [TestMethod]
        public async Task StopAsync_CallsShutdownAsync()
        {
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).Returns(Task.FromResult(Enumerable.Empty<DocumentServiceLease>()));
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(new List<DocumentServiceLease>());

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);
            leaseStoreManager.Setup(l => l.ShutdownAsync()).Returns(Task.CompletedTask);
            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out Mock<ChangeFeedObserverFactory> factory, out Mock<ChangeFeedObserver> observer);
            processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

            await processor.StartAsync();
            await processor.StopAsync();

            leaseStoreManager
                .Verify(store => store.ShutdownAsync(), Times.Once);
        }

        [TestMethod]
        public async Task StopAsync_WithInMemoryLeases_PersistsStateToStream()
        {
            // Arrange — real in-memory store with a real MemoryStream
            DocumentServiceLeaseCoreEpk lease = new DocumentServiceLeaseCoreEpk
            {
                LeaseId = "e2e-lease",
                LeaseToken = "0",
                ContinuationToken = "e2e-continuation",
                Owner = "e2e-owner",
                FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("", "FF", true, false))
            };

            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            container.TryAdd(lease.Id, lease);

            MemoryStream leaseState = new MemoryStream();
            DocumentServiceLeaseStoreManagerInMemory storeManager = new DocumentServiceLeaseStoreManagerInMemory(container, leaseState);

            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                storeManager,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

            // Act — full lifecycle: start → stop (which triggers ShutdownAsync → persist)
            await processor.StartAsync();
            await processor.StopAsync();

            // Assert — stream is usable and contains valid serialized lease state
            Assert.IsTrue(leaseState.CanRead, "Stream should still be readable after StopAsync");
            Assert.IsTrue(leaseState.Length > 0, "Stream should contain serialized lease data");

            // Verify the persisted data deserializes correctly
            leaseState.Position = 0;
            using (StreamReader sr = new StreamReader(leaseState, leaveOpen: true))
            using (Newtonsoft.Json.JsonTextReader jsonReader = new Newtonsoft.Json.JsonTextReader(sr))
            {
                List<DocumentServiceLease> persisted = Newtonsoft.Json.JsonSerializer.Create()
                    .Deserialize<List<DocumentServiceLease>>(jsonReader);

                Assert.AreEqual(1, persisted.Count);
                Assert.AreEqual("e2e-lease", persisted[0].Id);
                Assert.AreEqual("e2e-continuation", persisted[0].ContinuationToken);
                Assert.IsNotNull(persisted[0].FeedRange);
                Assert.IsInstanceOfType(persisted[0].FeedRange, typeof(FeedRangeEpk));
            }
        }

        [TestMethod]
        public async Task StopAsync_WhenShutdownAsyncThrows_ExceptionPropagates()
        {
            // Arrange — set up a processor where ShutdownAsync throws
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).Returns(Task.FromResult(Enumerable.Empty<DocumentServiceLease>()));
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(new List<DocumentServiceLease>());

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);
            leaseStoreManager.Setup(l => l.ShutdownAsync()).ThrowsAsync(new InvalidOperationException("Shutdown failed"));

            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

            await processor.StartAsync();

            // Act & Assert — StopAsync propagates ShutdownAsync exceptions so callers
            // know persistence failed.
            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => processor.StopAsync());
            Assert.AreEqual("Shutdown failed", ex.Message);

            // Assert — ShutdownAsync was still invoked
            leaseStoreManager.Verify(l => l.ShutdownAsync(), Times.Once);
        }

        private static ChangeFeedProcessorCore CreateProcessor(
            out Mock<ChangeFeedObserverFactory> factory,
            out Mock<ChangeFeedObserver> observer)
        {
            factory = new Mock<ChangeFeedObserverFactory>();
            observer = new Mock<ChangeFeedObserver>();
            factory.Setup(f => f.CreateObserver()).Returns(observer.Object);

            return new ChangeFeedProcessorCore(factory.Object);
        }

        public class MyDocument
        {
            public string id { get; set; }
        }

        private static ContainerInternal GetMockedContainer(string containerName = null)
        {
            Mock<ContainerInternal> mockedContainer = MockCosmosUtil.CreateMockContainer(containerName: containerName);
            mockedContainer.Setup(c => c.ClientContext).Returns(ChangeFeedProcessorCoreTests.GetMockedClientContext());
            string monitoredContainerRid = "V4lVAMl0wuQ=";
            mockedContainer.Setup(c => c.GetCachedRIDAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(monitoredContainerRid);
            Mock<DatabaseInternal> mockedDatabase = MockCosmosUtil.CreateMockDatabase();
            mockedContainer.Setup(c => c.Database).Returns(mockedDatabase.Object);
            return mockedContainer.Object;
        }

        private static CosmosClientContext GetMockedClientContext()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));

            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.ClientOptions).Returns(MockCosmosUtil.GetDefaultConfiguration());
            mockContext.Setup(x => x.DocumentClient).Returns(new MockDocumentClient());
            mockContext.Setup(x => x.SerializerCore).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(x => x.Client).Returns(mockClient.Object);
            return mockContext.Object;
        }
    }
}