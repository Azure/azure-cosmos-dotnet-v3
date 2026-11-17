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
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

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
                .Verify(store => store.GetAllLeasesAsync(), Times.Exactly(3));
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

        [DataTestMethod]
        [DataRow(true, false, "same", true)]
        [DataRow(true, true, "same", true)]
        [DataRow(true, false, "different", false)]
        [DataRow(true, true, "different", false)]
        [DataRow(true, false, null, false)]
        [DataRow(true, true, null, false)]
        [DataRow(true, false, "", false)]
        [DataRow(false, false, null, true)]
        [DataRow(false, true, null, true)]
        [DataRow(false, false, "different", false)]
        [DataRow(false, true, "different", false)]
        [DataRow(false, true, "", false)]
        public async Task StartAsyncChecksEveryQueryLease(bool filtered, bool epk, string storedIdentity, bool compatible)
        {
            ChangeFeedQuerySpec query = filtered ? new ChangeFeedQuerySpec("SELECT * FROM c WHERE c.state = 'WA'") : null;
            string expectedHash = ChangeFeedHelper.GetQueryFilterHash(query);
            DocumentServiceLease secondLease = epk
                ? new DocumentServiceLeaseCoreEpk { LeaseId = "second", LeaseToken = "AA-BB" }
                : new DocumentServiceLeaseCore { LeaseId = "second", LeaseToken = "1" };
            secondLease.QueryFilterHash = storedIdentity == "same" ? expectedHash : storedIdentity;
            List<DocumentServiceLease> leases = new List<DocumentServiceLease>
            {
                new DocumentServiceLeaseCore { LeaseId = "first", LeaseToken = "0", QueryFilterHash = expectedHash },
                secondLease
            };
            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            int snapshots = 0;
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(
                () => snapshots++ < 2 ? leases : new List<DocumentServiceLease>());
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).ReturnsAsync(Enumerable.Empty<DocumentServiceLease>());
            Mock<DocumentServiceLeaseManager> leaseManager = new Mock<DocumentServiceLeaseManager>();
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(() =>
            {
                leaseManager.Object.VerifyQueryFilter(leases[0]);
                return true;
            });
            Mock<DocumentServiceLeaseStoreManager> store = new Mock<DocumentServiceLeaseStoreManager>();
            store.SetupGet(s => s.LeaseContainer).Returns(leaseContainer.Object);
            store.SetupGet(s => s.LeaseManager).Returns(leaseManager.Object);
            store.SetupGet(s => s.LeaseStore).Returns(leaseStore.Object);
            store.SetupGet(s => s.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>());
            ChangeFeedProcessorCore processor = CreateProcessor(out Mock<ChangeFeedObserverFactory> factory, out _);
            processor.ApplyBuildConfiguration(
                store.Object, null, "instance", new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions { QuerySpec = query }, GetMockedContainer("monitored"));

            bool started = false;
            try
            {
                if (compatible)
                {
                    await processor.StartAsync();
                    started = true;
                    leaseManager.Object.VerifyQueryFilter(leases[0]);
                    leaseManager.Object.VerifyQueryFilter(secondLease);
                }
                else
                {
                    ArgumentException error = await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
                    {
                        await processor.StartAsync();
                        started = true;
                    });
                    StringAssert.Contains(error.Message, "query filter");
                    Assert.IsFalse(error.Message.Contains("SELECT"));
                    leaseContainer.Verify(l => l.GetOwnedLeasesAsync(), Times.Never);
                }

                factory.Verify(f => f.CreateObserver(), Times.Never);
                leaseManager.Verify(m => m.AcquireAsync(It.IsAny<DocumentServiceLease>()), Times.Never);
            }
            finally
            {
                if (started)
                {
                    await processor.StopAsync();
                }
            }
        }

        [DataTestMethod]
        [DataRow(false, "query")]
        [DataRow(true, "query")]
        [DataRow(false, "lease")]
        [DataRow(true, "lease")]
        [DataRow(false, "unchanged")]
        [DataRow(true, "unchanged")]
        public async Task RestartRevalidatesQueryIdentity(bool filtered, string change)
        {
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);
            List<DocumentServiceLease> leases = new List<DocumentServiceLease>();
            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(leases);
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).ReturnsAsync(Enumerable.Empty<DocumentServiceLease>());
            Mock<DocumentServiceLeaseStoreManager> store = new Mock<DocumentServiceLeaseStoreManager>();
            store.SetupGet(s => s.LeaseContainer).Returns(leaseContainer.Object);
            store.SetupGet(s => s.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>());
            store.SetupGet(s => s.LeaseStore).Returns(leaseStore.Object);
            store.SetupGet(s => s.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>());
            ChangeFeedProcessorOptions options = new ChangeFeedProcessorOptions
            {
                QuerySpec = filtered ? new ChangeFeedQuerySpec("SELECT * FROM c WHERE c.state = 'WA'") : null
            };
            ChangeFeedProcessorCore processor = CreateProcessor(out Mock<ChangeFeedObserverFactory> factory, out _);
            processor.ApplyBuildConfiguration(
                store.Object, null, "instance", new ChangeFeedLeaseOptions(), options, GetMockedContainer("monitored"));
            await processor.StartAsync();
            await processor.StopAsync();

            if (change == "query")
            {
                options.QuerySpec = new ChangeFeedQuerySpec("SELECT * FROM c WHERE c.state = 'OR'");
            }
            else if (change == "lease")
            {
                leases.Add(new DocumentServiceLeaseCore { LeaseId = "0", LeaseToken = "0", QueryFilterHash = "different" });
            }

            if (change == "unchanged")
            {
                await processor.StartAsync();
                await processor.StopAsync();
            }
            else
            {
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => processor.StartAsync());
            }

            leaseStore.Verify(s => s.IsInitializedAsync(), Times.Exactly(change == "query" ? 1 : 2));
            leaseContainer.Verify(l => l.GetOwnedLeasesAsync(), Times.Exactly(change == "unchanged" ? 2 : 1));
            factory.Verify(f => f.CreateObserver(), Times.Never);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task RestoredQueryLeasesRejectIncompatibleStartup(bool epk, bool filtered)
        {
            string originalHash = ChangeFeedHelper.GetQueryFilterHash(new ChangeFeedQuerySpec("SELECT * FROM c"));
            DocumentServiceLease lease = epk
                ? new DocumentServiceLeaseCoreEpk { LeaseId = "AA-BB", LeaseToken = "AA-BB" }
                : new DocumentServiceLeaseCore { LeaseId = "0", LeaseToken = "0" };
            lease.QueryFilterHash = originalHash;
            lease.ContinuationToken = "saved-progress";
            lease.Owner = "original-owner";
            lease.FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false));
            ConcurrentDictionary<string, DocumentServiceLease> leases = new ConcurrentDictionary<string, DocumentServiceLease>();
            Assert.IsTrue(leases.TryAdd(lease.Id, lease));
            using MemoryStream state = new MemoryStream();
            DocumentServiceLeaseStoreManagerInMemory original = new DocumentServiceLeaseStoreManagerInMemory(leases, state);
            await original.ShutdownAsync();
            byte[] savedState = state.ToArray();
            DocumentServiceLeaseStoreManagerInMemory restored = new DocumentServiceLeaseStoreManagerInMemory(state);
            ChangeFeedProcessorCore processor = CreateProcessor(out Mock<ChangeFeedObserverFactory> factory, out _);
            processor.ApplyBuildConfiguration(
                restored, null, "instance", new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions
                {
                    QuerySpec = filtered ? new ChangeFeedQuerySpec("SELECT * FROM c WHERE c.state = 'WA'") : null
                },
                GetMockedContainer("monitored"));

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => processor.StartAsync());

            DocumentServiceLease unchanged = (await restored.LeaseContainer.GetAllLeasesAsync()).Single();
            Assert.AreEqual(originalHash, unchanged.QueryFilterHash);
            Assert.AreEqual("saved-progress", unchanged.ContinuationToken);
            Assert.AreEqual("original-owner", unchanged.Owner);
            Assert.IsFalse(await restored.LeaseStore.IsInitializedAsync());
            CollectionAssert.AreEqual(savedState, state.ToArray());
            factory.Verify(f => f.CreateObserver(), Times.Never);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task IncompatibleHostCannotDeliverFromActiveQueryNamespace(bool unfiltered)
        {
            ChangeFeedQuerySpec query = new ChangeFeedQuerySpec("SELECT * FROM c WHERE c.state = 'WA'");
            string hash = ChangeFeedHelper.GetQueryFilterHash(query);
            ConcurrentDictionary<string, DocumentServiceLease> leases = new ConcurrentDictionary<string, DocumentServiceLease>();
            DocumentServiceLeaseStoreManagerInMemory firstStore = new DocumentServiceLeaseStoreManagerInMemory(leases);
            firstStore.LeaseManager.ConfigureQueryFilter(hash);
            DocumentServiceLease lease = await firstStore.LeaseManager.CreateLeaseIfNotExistAsync(
                new Documents.PartitionKeyRange { Id = "0", MinInclusive = "", MaxExclusive = "FF" }, "saved-progress");
            await firstStore.LeaseStore.MarkInitializedAsync();
            DocumentServiceLeaseStoreManagerInMemory secondStore = new DocumentServiceLeaseStoreManagerInMemory(leases);
            await secondStore.LeaseStore.MarkInitializedAsync();
            int requests = 0;
            TestHandler handler = new TestHandler((request, cancellationToken) =>
            {
                ResponseMessage response = new ResponseMessage(
                    Interlocked.Increment(ref requests) == 1 ? HttpStatusCode.OK : HttpStatusCode.NotModified)
                {
                    Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"Documents\":[{\"id\":\"1\"}],\"_count\":1}"))
                };
                response.Headers.ETag = "next-progress";
                return Task.FromResult(response);
            });
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(builder => builder.AddCustomHandlers(handler));
            ContainerInternal container = (ContainerInternal)client.GetContainer("myDb", "myContainer");
            ChangeFeedProcessorCore first = CreateProcessor(out _, out Mock<ChangeFeedObserver> firstObserver);
            TaskCompletionSource<bool> delivered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            firstObserver.Setup(o => o.ProcessChangesAsync(
                It.IsAny<ChangeFeedObserverContextCore>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback(() => delivered.TrySetResult(true))
                .Returns(Task.CompletedTask);
            first.ApplyBuildConfiguration(
                firstStore, null, "first", new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions { QuerySpec = query }, container);
            ChangeFeedProcessorCore second = CreateProcessor(out Mock<ChangeFeedObserverFactory> secondFactory, out _);
            second.ApplyBuildConfiguration(
                secondStore, null, "second", new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions
                {
                    QuerySpec = unfiltered ? null : new ChangeFeedQuerySpec("SELECT * FROM c WHERE c.state = 'OR'")
                },
                container);

            await first.StartAsync();
            try
            {
                await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => second.StartAsync());

                secondFactory.Verify(f => f.CreateObserver(), Times.Never);
                Assert.AreEqual(hash, lease.QueryFilterHash);
                Assert.AreEqual("saved-progress", lease.ContinuationToken);
            }
            finally
            {
                await first.StopAsync();
            }
        }

        [DataTestMethod]
        [DataRow(false, HttpStatusCode.BadRequest)]
        [DataRow(true, HttpStatusCode.BadRequest)]
        [DataRow(false, HttpStatusCode.TooManyRequests)]
        [DataRow(true, HttpStatusCode.TooManyRequests)]
        [DataRow(false, HttpStatusCode.ServiceUnavailable)]
        [DataRow(true, HttpStatusCode.ServiceUnavailable)]
        public async Task ReadErrorsNotifyAndRecoverAcrossAcquisitionCycles(bool filtered, HttpStatusCode status)
        {
            ChangeFeedQuerySpec query = filtered ? new ChangeFeedQuerySpec("SELECT * FROM c") : null;
            DocumentServiceLeaseStoreManagerInMemory store = new DocumentServiceLeaseStoreManagerInMemory();
            store.LeaseManager.ConfigureQueryFilter(ChangeFeedHelper.GetQueryFilterHash(query));
            await store.LeaseManager.CreateLeaseIfNotExistAsync(
                new Documents.PartitionKeyRange { Id = "0", MinInclusive = "", MaxExclusive = "FF" }, "saved-progress");
            await store.LeaseStore.MarkInitializedAsync();
            int requests = 0;
            int notifications = 0;
            int releases = 0;
            int target = 2;
            TaskCompletionSource<bool> notified = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TestHandler handler = new TestHandler((request, cancellationToken) =>
            {
                Interlocked.Increment(ref requests);
                Assert.AreEqual("saved-progress", request.Headers.IfNoneMatch);
                Assert.AreEqual(filtered, request.Content != null);
                ResponseMessage response = new ResponseMessage(status);
                response.Headers.ETag = "error-position";
                response.Headers.ActivityId = "service-error";
                return Task.FromResult(response);
            });
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(builder => builder.AddCustomHandlers(handler));
            ChangeFeedProcessorCore processor = CreateProcessor(out Mock<ChangeFeedObserverFactory> factory, out _);
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder(
                "errors", (ContainerInternal)client.GetContainer("myDb", "myContainer"), processor,
                (unused, leaseContainer, instance, leaseOptions, options, container) =>
                {
                    options.QuerySpec = query;
                    processor.ApplyBuildConfiguration(store, null, instance, leaseOptions, options, container);
                });
            builder.WithInMemoryLeaseContainer()
                .WithLeaseConfiguration(acquireInterval: TimeSpan.FromMilliseconds(10))
                .WithErrorNotification((leaseToken, exception) =>
                {
                    Assert.AreEqual("0", leaseToken);
                    Assert.IsInstanceOfType(exception, typeof(CosmosException));
                    CosmosException error = (CosmosException)exception;
                    Assert.AreEqual(status, error.StatusCode);
                    Assert.AreEqual("service-error", error.ActivityId);
                    if (Interlocked.Increment(ref notifications) >= target)
                    {
                        notified.TrySetResult(true);
                    }

                    return Task.CompletedTask;
                })
                .WithLeaseReleaseNotification(leaseToken =>
                {
                    Interlocked.Increment(ref releases);
                    return Task.CompletedTask;
                })
                .Build();

            for (int run = 0; run < 2; run++)
            {
                await processor.StartAsync();
                try
                {
                    await notified.Task.WaitAsync(TimeSpan.FromSeconds(10));
                }
                finally
                {
                    await processor.StopAsync();
                }

                Assert.AreEqual(requests, notifications);
                Assert.AreEqual(requests, releases);
                DocumentServiceLease lease = (await store.LeaseContainer.GetAllLeasesAsync()).Single();
                Assert.AreEqual("saved-progress", lease.ContinuationToken);
                Assert.IsNull(lease.Owner);
                target = notifications + 2;
                notified = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            factory.Verify(f => f.CreateObserver(), Times.AtLeast(4));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task AllVersionsAndDeletesStartsAndRestartsWithFreshInMemoryLeases(bool filtered)
        {
            TaskCompletionSource<bool> read = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TestHandler handler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual(
                    Documents.HttpConstants.A_IMHeaderValues.FullFidelityFeed,
                    request.Headers[Documents.HttpConstants.HttpHeaders.A_IM]);
                Assert.AreEqual(filtered, request.Content != null);
                ResponseMessage response = new ResponseMessage(HttpStatusCode.NotModified);
                response.Headers.ETag = "progress";
                read.TrySetResult(true);
                return Task.FromResult(response);
            });
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(builder => builder.AddCustomHandlers(handler));
            ChangeFeedProcessorBuilder builder = client.GetContainer("myDb", "myContainer")
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes<MyDocument>(
                    "avad", (context, changes, token) => Task.CompletedTask)
                .WithInMemoryLeaseContainer()
                .WithPollInterval(TimeSpan.FromMilliseconds(10))
                .WithErrorNotification((lease, exception) =>
                {
                    read.TrySetException(exception);
                    return Task.CompletedTask;
                });
            if (filtered)
            {
                builder.WithQuery("SELECT * FROM c");
            }

            ChangeFeedProcessor processor = builder.Build();
            for (int run = 0; run < 2; run++)
            {
                await processor.StartAsync();
                try
                {
                    await read.Task.WaitAsync(TimeSpan.FromSeconds(10));
                }
                finally
                {
                    await processor.StopAsync();
                }

                read = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task StartAsyncRejectsExistingLeaseInDifferentMode(bool allVersionsAndDeletes)
        {
            DocumentServiceLeaseStoreManagerInMemory store = new DocumentServiceLeaseStoreManagerInMemory();
            DocumentServiceLease lease = await store.LeaseManager.CreateLeaseIfNotExistAsync(
                new Documents.PartitionKeyRange { Id = "0", MinInclusive = "", MaxExclusive = "FF" }, "saved-progress");
            lease.Mode = allVersionsAndDeletes ? null : Documents.HttpConstants.A_IMHeaderValues.FullFidelityFeed;
            await store.LeaseStore.MarkInitializedAsync();
            ChangeFeedMode mode = allVersionsAndDeletes ? ChangeFeedMode.AllVersionsAndDeletes : ChangeFeedMode.LatestVersion;
            ChangeFeedProcessorCore processor = CreateProcessor(out Mock<ChangeFeedObserverFactory> factory, out _);
            processor.ApplyBuildConfiguration(
                store, null, "instance",
                new ChangeFeedLeaseOptions { Mode = mode },
                new ChangeFeedProcessorOptions { Mode = mode },
                GetMockedContainer("monitored"));

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => processor.StartAsync());

            factory.Verify(f => f.CreateObserver(), Times.Never);
            Assert.AreEqual("saved-progress", lease.ContinuationToken);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ChangedQueryDuringRenewalStopsWorkerAndNotifiesWithoutCheckpoint(bool changedOwner)
        {
            const string query = "SELECT * FROM c";
            ConcurrentDictionary<string, DocumentServiceLease> leases = new ConcurrentDictionary<string, DocumentServiceLease>();
            DocumentServiceLeaseStoreManagerInMemory store = new DocumentServiceLeaseStoreManagerInMemory(leases);
            store.LeaseManager.ConfigureQueryFilter(ChangeFeedHelper.GetQueryFilterHash(new ChangeFeedQuerySpec(query)));
            DocumentServiceLease original = await store.LeaseManager.CreateLeaseIfNotExistAsync(
                new Documents.PartitionKeyRange { Id = "0", MinInclusive = "", MaxExclusive = "FF" }, "saved-progress");
            original.Owner = "original-owner";
            await store.LeaseStore.MarkInitializedAsync();
            int reads = 0;
            TestHandler handler = new TestHandler((request, token) =>
            {
                ResponseMessage response = new ResponseMessage(
                    Interlocked.Increment(ref reads) == 1 ? HttpStatusCode.OK : HttpStatusCode.NotModified)
                {
                    Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"Documents\":[{\"id\":\"1\"}],\"_count\":1}"))
                };
                response.Headers.ETag = "read-progress";
                return Task.FromResult(response);
            });
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(builder => builder.AddCustomHandlers(handler));
            ChangeFeedProcessorCore processor = CreateProcessor(out _, out Mock<ChangeFeedObserver> observer);
            TaskCompletionSource<bool> replaced = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            observer.Setup(o => o.ProcessChangesAsync(
                It.IsAny<ChangeFeedObserverContextCore>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    DocumentServiceLease replacement = JsonConvert.DeserializeObject<DocumentServiceLease>(
                        JsonConvert.SerializeObject(original));
                    replacement.QueryFilterHash = "different-query";
                    replacement.Owner = changedOwner ? "replacement-owner" : original.Owner;
                    leases[original.Id] = replacement;
                    replaced.TrySetResult(true);
                })
                .Returns(Task.CompletedTask);
            TaskCompletionSource<bool> closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            observer.Setup(o => o.CloseAsync("0", ChangeFeedObserverCloseReason.LeaseLost))
                .Callback(() => closed.TrySetResult(true))
                .Returns(Task.CompletedTask);
            TaskCompletionSource<Exception> notified = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder(
                "renewal", (ContainerInternal)client.GetContainer("myDb", "myContainer"), processor,
                (unused, container, instance, leaseOptions, options, monitored) =>
                    processor.ApplyBuildConfiguration(store, null, instance, leaseOptions, options, monitored));
            builder.WithInMemoryLeaseContainer()
                .WithQuery(query)
                .WithPollInterval(TimeSpan.FromMilliseconds(10))
                .WithLeaseConfiguration(acquireInterval: TimeSpan.FromHours(1), renewInterval: TimeSpan.FromMilliseconds(20))
                .WithErrorNotification((lease, exception) =>
                {
                    notified.TrySetResult(exception);
                    return Task.CompletedTask;
                })
                .Build();

            await processor.StartAsync();
            try
            {
                await replaced.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Exception error = await notified.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.IsInstanceOfType(error, typeof(ArgumentException));
                StringAssert.Contains(error.Message, "query filter");
                await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                await processor.StopAsync();
            }

            Assert.AreEqual("different-query", leases[original.Id].QueryFilterHash);
            Assert.AreEqual(changedOwner ? "replacement-owner" : "original-owner", leases[original.Id].Owner);
            Assert.AreEqual("saved-progress", leases[original.Id].ContinuationToken);
            observer.Verify(o => o.CloseAsync("0", ChangeFeedObserverCloseReason.LeaseLost), Times.Once);
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