//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Bootstrapping;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class PartitionSynchronizerCoreTests
    {
        private GlobalEndpointManager endpointManager;

        [TestInitialize]
        public void TestInitialize()
        {
            Mock<IDocumentClientInternal> mockDocumentClient = new();

            mockDocumentClient
                .Setup(client => client.ServiceEndpoint)
                .Returns(new Uri("https://foo"));

            this.endpointManager = new(
                mockDocumentClient.Object,
                new ConnectionPolicy());
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.endpointManager.Dispose();
        }

        /// <summary>
        /// Repros the customer NRE: TryGetOverlappingRangesAsync returns null and split handling
        /// used to dereference it.
        /// </summary>
        [TestMethod]
        public async Task HandlePartitionGoneAsync_OverlappingRangesLookupReturnsNull_ThrowsInvalidOperationExceptionNotNullReferenceException()
        {
            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                ContinuationToken = Guid.NewGuid().ToString(),
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("", "FF", true, false)),
            };

            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                new Mock<Common.CollectionCache>(false).Object,
                this.endpointManager,
                false,
                false,
                null);

            // Simulate the routing map cache lookup failing to resolve any ranges.
            pkRangeCache.Setup(p => p.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.IsAny<Documents.Routing.Range<string>>(),
                It.IsAny<ITrace>(),
                It.IsAny<bool>())).ReturnsAsync((IReadOnlyList<Documents.PartitionKeyRange>)null);

            PartitionSynchronizerCore partitionSynchronizerCore = new PartitionSynchronizerCore(
                Mock.Of<ContainerInternal>(),
                Mock.Of<DocumentServiceLeaseContainer>(),
                Mock.Of<DocumentServiceLeaseManager>(),
                1,
                pkRangeCache.Object,
                Guid.NewGuid().ToString());

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => partitionSynchronizerCore.HandlePartitionGoneAsync(lease));
        }

        /// <summary>
        /// Repros the customer NRE using the exact in-memory lease JSON shape (non-null FeedRange).
        /// </summary>
        [TestMethod]
        public async Task HandlePartitionGoneAsync_CustomerReportedLeaseJson_DoesNotThrowNullReferenceException()
        {
            string customerLeaseJson = @"[
                {
                    ""id"": ""0"",
                    ""version"": 0,
                    ""_etag"": null,
                    ""LeaseToken"": ""0"",
                    ""FeedRange"": {
                        ""Range"": {
                            ""min"": """",
                            ""max"": ""FF""
                        }
                    },
                    ""Owner"": """",
                    ""ContinuationToken"": ""\""32\"""",
                    ""properties"": {},
                    ""timestamp"": null,
                    ""_ts"": 0
                }
            ]";

            using MemoryStream leaseStateStream = new MemoryStream(Encoding.UTF8.GetBytes(customerLeaseJson));
            DocumentServiceLeaseStoreManagerInMemory storeManager = new DocumentServiceLeaseStoreManagerInMemory(leaseStateStream);

            IReadOnlyList<DocumentServiceLease> restoredLeases = await storeManager.LeaseContainer.GetAllLeasesAsync();
            DocumentServiceLease restoredLease = restoredLeases.Single();

            // Sanity-check deserialization before exercising the split path.
            Assert.IsNotNull(restoredLease.FeedRange);
            Assert.AreEqual("\"32\"", restoredLease.ContinuationToken);

            Documents.PartitionKeyRange currentRange = new Documents.PartitionKeyRange() { Id = "0", MinInclusive = "", MaxExclusive = "FF" };
            List<Documents.PartitionKeyRange> childRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange(){ Id = "1", MinInclusive = "", MaxExclusive = "BB" },
                new Documents.PartitionKeyRange(){ Id = "2", MinInclusive = "BB", MaxExclusive = "FF" },
            };

            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                new Mock<Common.CollectionCache>(false).Object,
                this.endpointManager,
                false,
                false,
                null);

            pkRangeCache.Setup(p => p.TryGetPartitionKeyRangeByIdAsync(
                It.IsAny<string>(),
                "0",
                It.IsAny<ITrace>(),
                It.IsAny<bool>())).ReturnsAsync(currentRange);

            pkRangeCache.Setup(p => p.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.IsAny<Documents.Routing.Range<string>>(),
                It.IsAny<ITrace>(),
                It.IsAny<bool>())).ReturnsAsync(childRanges);

            PartitionSynchronizerCore partitionSynchronizerCore = new PartitionSynchronizerCore(
                Mock.Of<ContainerInternal>(),
                storeManager.LeaseContainer,
                storeManager.LeaseManager,
                1,
                pkRangeCache.Object,
                Guid.NewGuid().ToString());

            (IEnumerable<DocumentServiceLease> newLeases, bool removeCurrentLease) =
                await partitionSynchronizerCore.HandlePartitionGoneAsync(restoredLease);

            Assert.IsTrue(removeCurrentLease);
            Assert.AreEqual(2, newLeases.Count());
        }

        /// <summary>
        /// Verifies handling of Splits on PKRange based leases
        /// </summary>
        [TestMethod]
        public async Task HandlePartitionGoneAsync_PKRangeBasedLease_Split()
        {
            string continuation = Guid.NewGuid().ToString();
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("", "FF", true, false);
            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                ContinuationToken = continuation,
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangeEpk(range)
            };

            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                new Mock<Common.CollectionCache>(false).Object,
                this.endpointManager,
                false,
                false,
                null);

            List<Documents.PartitionKeyRange> resultingRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange(){ Id = "1", MinInclusive = "", MaxExclusive = "BB" },
                new Documents.PartitionKeyRange(){ Id = "2", MinInclusive = "BB", MaxExclusive = "FF" },
            };

            pkRangeCache.Setup(p => p.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Documents.Routing.Range<string>>(r => r.Min == range.Min && r.Max == range.Max),
                It.IsAny<ITrace>(),
                true))
                .ReturnsAsync(resultingRanges);

            Mock<DocumentServiceLeaseManager> leaseManager = new Mock<DocumentServiceLeaseManager>();

            PartitionSynchronizerCore partitionSynchronizerCore = new PartitionSynchronizerCore(
                Mock.Of<ContainerInternal>(),
                Mock.Of<DocumentServiceLeaseContainer>(),
                leaseManager.Object,
                1,
                pkRangeCache.Object,
                Guid.NewGuid().ToString());

            await partitionSynchronizerCore.HandlePartitionGoneAsync(lease);

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.IsAny<Documents.PartitionKeyRange>(),
               It.IsAny<string>()), Times.Exactly(2));

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.IsAny<FeedRangeEpk>(),
               It.IsAny<string>()), Times.Never);

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.Is<Documents.PartitionKeyRange>(pkRange => pkRange.Id == resultingRanges[0].Id),
               It.Is<string>(c => c == continuation)), Times.Once);

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.Is<Documents.PartitionKeyRange>(pkRange => pkRange.Id == resultingRanges[1].Id),
               It.Is<string>(c => c == continuation)), Times.Once);
        }

        /// <summary>
        /// Verifies handling of Splits on EPK based leases
        /// </summary>
        [TestMethod]
        public async Task HandlePartitionGoneAsync_EpkBasedLease_Split()
        {
            string continuation = Guid.NewGuid().ToString();
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("AA", "EE", true, false);
            DocumentServiceLeaseCoreEpk lease = new DocumentServiceLeaseCoreEpk()
            {
                LeaseToken = "AA-BB",
                ContinuationToken = continuation,
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangeEpk(range)
            };

            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                new Mock<Common.CollectionCache>(false).Object,
                this.endpointManager,
                false,
                false,
                null);

            List<Documents.PartitionKeyRange> resultingRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange(){ Id = "1", MinInclusive = "", MaxExclusive = "BB" },
                new Documents.PartitionKeyRange(){ Id = "2", MinInclusive = "BB", MaxExclusive = "DD" },
                new Documents.PartitionKeyRange(){ Id = "3", MinInclusive = "DD", MaxExclusive = "FF" },
            };

            pkRangeCache.Setup(p => p.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Documents.Routing.Range<string>>(r => r.Min == range.Min && r.Max == range.Max),
                It.IsAny<ITrace>(),
                It.Is<bool>(b => b == true)))
                .ReturnsAsync(resultingRanges);

            Mock<DocumentServiceLeaseManager> leaseManager = new Mock<DocumentServiceLeaseManager>();

            PartitionSynchronizerCore partitionSynchronizerCore = new PartitionSynchronizerCore(
                Mock.Of<ContainerInternal>(),
                Mock.Of<DocumentServiceLeaseContainer>(),
                leaseManager.Object,
                1,
                pkRangeCache.Object,
                Guid.NewGuid().ToString());

            await partitionSynchronizerCore.HandlePartitionGoneAsync(lease);

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.IsAny<Documents.PartitionKeyRange>(),
               It.IsAny<string>()), Times.Never);

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.IsAny<FeedRangeEpk>(),
               It.IsAny<string>()), Times.Exactly(3));

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.Is<FeedRangeEpk>(epk => epk.Range.Min == range.Min && epk.Range.Max == resultingRanges[0].MaxExclusive),
               It.Is<string>(c => c == continuation)), Times.Once);

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.Is<FeedRangeEpk>(epk => epk.Range.Min == resultingRanges[1].MinInclusive && epk.Range.Max == resultingRanges[1].MaxExclusive),
               It.Is<string>(c => c == continuation)), Times.Once);

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.Is<FeedRangeEpk>(epk => epk.Range.Min == resultingRanges[2].MinInclusive && epk.Range.Max == range.Max),
               It.Is<string>(c => c == continuation)), Times.Once);
        }

        /// <summary>
        /// Verifies handling of Merges on PKRange based leases
        /// </summary>
        [TestMethod]
        public async Task HandlePartitionGoneAsync_PKRangeBasedLease_Merge()
        {
            string continuation = Guid.NewGuid().ToString();
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("", "BB", true, false);
            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                ContinuationToken = continuation,
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangeEpk(range)
            };

            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                new Mock<Common.CollectionCache>(false).Object,
                this.endpointManager,
                false,
                false,
                null);

            List<Documents.PartitionKeyRange> resultingRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange(){ Id = "2", MinInclusive = "", MaxExclusive = "FF" }
            };

            pkRangeCache.Setup(p => p.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Documents.Routing.Range<string>>(r => r.Min == range.Min && r.Max == range.Max),
                It.IsAny<ITrace>(),
                It.Is<bool>(b => b == true)))
                .ReturnsAsync(resultingRanges);

            Mock<DocumentServiceLeaseManager> leaseManager = new Mock<DocumentServiceLeaseManager>();

            PartitionSynchronizerCore partitionSynchronizerCore = new PartitionSynchronizerCore(
                Mock.Of<ContainerInternal>(),
                Mock.Of<DocumentServiceLeaseContainer>(),
                leaseManager.Object,
                1,
                pkRangeCache.Object,
                Guid.NewGuid().ToString());

            await partitionSynchronizerCore.HandlePartitionGoneAsync(lease);

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.IsAny<Documents.PartitionKeyRange>(),
               It.IsAny<string>()), Times.Never);

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.IsAny<FeedRangeEpk>(),
               It.IsAny<string>()), Times.Once);

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.Is<FeedRangeEpk>(epKRange => epKRange.Range.Min == range.Min && epKRange.Range.Max == range.Max),
               It.Is<string>(c => c == continuation)), Times.Once);
        }

        /// <summary>
        /// Verifies handling of Merges on EPK based leases
        /// </summary>
        [TestMethod]
        public async Task HandlePartitionGoneAsync_EpkBasedLease_Merge()
        {
            string continuation = Guid.NewGuid().ToString();
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("AA", "EE", true, false);
            DocumentServiceLeaseCoreEpk lease = new DocumentServiceLeaseCoreEpk()
            {
                LeaseToken = "AA-BB",
                ContinuationToken = continuation,
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangeEpk(range)
            };

            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                new Mock<Common.CollectionCache>(false).Object,
                this.endpointManager,
                false,
                false,
                null);

            List<Documents.PartitionKeyRange> resultingRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange(){ Id = "1", MinInclusive = "", MaxExclusive = "FF" },
            };

            pkRangeCache.Setup(p => p.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Documents.Routing.Range<string>>(r => r.Min == range.Min && r.Max == range.Max),
                It.IsAny<ITrace>(),
                true))
                .ReturnsAsync(resultingRanges);

            Mock<DocumentServiceLeaseManager> leaseManager = new Mock<DocumentServiceLeaseManager>();

            PartitionSynchronizerCore partitionSynchronizerCore = new PartitionSynchronizerCore(
                Mock.Of<ContainerInternal>(),
                Mock.Of<DocumentServiceLeaseContainer>(),
                leaseManager.Object,
                1,
                pkRangeCache.Object,
                Guid.NewGuid().ToString());

            (IEnumerable<DocumentServiceLease> addedLeases, bool shouldDelete) = await partitionSynchronizerCore.HandlePartitionGoneAsync(lease);

            Assert.IsFalse(shouldDelete);

            Assert.AreEqual(lease, addedLeases.First());

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.IsAny<Documents.PartitionKeyRange>(),
               It.IsAny<string>()), Times.Never);

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.IsAny<FeedRangeEpk>(),
               It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// PKRange lease with null FeedRange: children are resolved via Parents-based lookup
        /// on the full range (old id is gone from the routing map after the split).
        /// </summary>
        [TestMethod]
        public async Task HandlePartitionGoneAsync_PKRangeBasedLease_WithNullFeedRange_ResolvesViaParentsLookup()
        {
            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                ContinuationToken = Guid.NewGuid().ToString(),
                Owner = Guid.NewGuid().ToString(),
                FeedRange = null,
            };

            // Children whose Parents list the gone id "0".
            List<Documents.PartitionKeyRange> allCurrentRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange(){ Id = "1", MinInclusive = "", MaxExclusive = "BB", Parents = new Collection<string> { "0" } },
                new Documents.PartitionKeyRange(){ Id = "2", MinInclusive = "BB", MaxExclusive = "FF", Parents = new Collection<string> { "0" } },
            };

            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                new Mock<Common.CollectionCache>(false).Object,
                this.endpointManager,
                false,
                false,
                null);

            // Fix path: no by-id lookup; children are found via a full-range Parents lookup.
            pkRangeCache.Setup(p => p.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Documents.Routing.Range<string>>(r => r.Min == FeedRangeEpk.FullRange.Range.Min && r.Max == FeedRangeEpk.FullRange.Range.Max),
                It.IsAny<ITrace>(),
                It.IsAny<bool>())).ReturnsAsync(allCurrentRanges);

            Mock<DocumentServiceLeaseManager> leaseManager = new Mock<DocumentServiceLeaseManager>();
            leaseManager.Setup(l => l.CreateLeaseIfNotExistAsync(It.IsAny<Documents.PartitionKeyRange>(), It.IsAny<string>()))
                .ReturnsAsync((Documents.PartitionKeyRange range, string continuation) => new DocumentServiceLeaseCore { LeaseToken = range.Id, ContinuationToken = continuation });

            PartitionSynchronizerCore partitionSynchronizerCore = new PartitionSynchronizerCore(
                Mock.Of<ContainerInternal>(),
                Mock.Of<DocumentServiceLeaseContainer>(),
                leaseManager.Object,
                1,
                pkRangeCache.Object,
                Guid.NewGuid().ToString());

            (IEnumerable<DocumentServiceLease> newLeases, bool removeCurrentLease) =
                await partitionSynchronizerCore.HandlePartitionGoneAsync(lease);

            Assert.IsTrue(removeCurrentLease, "The parent lease should be marked for removal once child leases are created.");
            Assert.AreEqual(2, newLeases.Count(), "Both child ranges found via the Parents lookup should have produced a new lease.");

            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.IsAny<Documents.PartitionKeyRange>(),
               It.IsAny<string>()), Times.Exactly(2));
        }

        /// <summary>
        /// PKRange lease with null FeedRange resolving to a merge (one child): must throw instead
        /// of collapsing parents' continuation tokens onto a single lease.
        /// </summary>
        [TestMethod]
        public async Task HandlePartitionGoneAsync_PKRangeBasedLease_WithNullFeedRange_ResolvesToSingleMergedRange_Throws()
        {
            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                ContinuationToken = Guid.NewGuid().ToString(),
                Owner = Guid.NewGuid().ToString(),
                FeedRange = null,
            };

            // Merge: exactly one current range replaces the gone parent "0".
            List<Documents.PartitionKeyRange> allCurrentRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange(){ Id = "1", MinInclusive = "", MaxExclusive = "FF", Parents = new Collection<string> { "0", "0-sibling" } },
            };

            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                new Mock<Common.CollectionCache>(false).Object,
                this.endpointManager,
                false,
                false,
                null);

            pkRangeCache.Setup(p => p.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Documents.Routing.Range<string>>(r => r.Min == FeedRangeEpk.FullRange.Range.Min && r.Max == FeedRangeEpk.FullRange.Range.Max),
                It.IsAny<ITrace>(),
                It.IsAny<bool>())).ReturnsAsync(allCurrentRanges);

            Mock<DocumentServiceLeaseManager> leaseManager = new Mock<DocumentServiceLeaseManager>();
            leaseManager.Setup(l => l.CreateLeaseIfNotExistAsync(It.IsAny<Documents.PartitionKeyRange>(), It.IsAny<string>()))
                .ReturnsAsync((Documents.PartitionKeyRange range, string continuation) => new DocumentServiceLeaseCore { LeaseToken = range.Id, ContinuationToken = continuation });

            PartitionSynchronizerCore partitionSynchronizerCore = new PartitionSynchronizerCore(
                Mock.Of<ContainerInternal>(),
                Mock.Of<DocumentServiceLeaseContainer>(),
                leaseManager.Object,
                1,
                pkRangeCache.Object,
                Guid.NewGuid().ToString());

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => partitionSynchronizerCore.HandlePartitionGoneAsync(lease));

            StringAssert.Contains(ex.Message, "merge");
            StringAssert.Contains(ex.Message, "0");

            // Parent lease untouched.
            leaseManager.Verify(l => l.CreateLeaseIfNotExistAsync(
               It.IsAny<Documents.PartitionKeyRange>(),
               It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// End-to-end: rehydrated in-memory PKRange lease (null FeedRange) can still be split.
        /// </summary>
        [TestMethod]
        public async Task HandlePartitionGoneAsync_LeaseRehydratedFromSavedInMemoryState_SplitsSuccessfully()
        {
            string leaseToken = "0";
            string continuationToken = Guid.NewGuid().ToString();

            string legacyLeaseStateJson =
                "[{\"id\":\"" + leaseToken + "\",\"LeaseToken\":\"" + leaseToken + "\"," +
                "\"ContinuationToken\":\"" + continuationToken + "\",\"Owner\":\"owner1\"}]";

            using MemoryStream leaseStateStream = new MemoryStream(Encoding.UTF8.GetBytes(legacyLeaseStateJson));
            DocumentServiceLeaseStoreManagerInMemory storeManager = new DocumentServiceLeaseStoreManagerInMemory(leaseStateStream);

            IReadOnlyList<DocumentServiceLease> restoredLeases = await storeManager.LeaseContainer.GetAllLeasesAsync();
            DocumentServiceLease restoredLease = restoredLeases.Single();

            Assert.IsInstanceOfType(restoredLease, typeof(DocumentServiceLeaseCore));
            Assert.IsNull(restoredLease.FeedRange);

            Documents.PartitionKeyRange currentRange = new Documents.PartitionKeyRange() { Id = leaseToken, MinInclusive = "", MaxExclusive = "FF" };
            List<Documents.PartitionKeyRange> childRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange(){ Id = "1", MinInclusive = "", MaxExclusive = "BB", Parents = new Collection<string> { leaseToken } },
                new Documents.PartitionKeyRange(){ Id = "2", MinInclusive = "BB", MaxExclusive = "FF", Parents = new Collection<string> { leaseToken } },
            };

            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                new Mock<Common.CollectionCache>(false).Object,
                this.endpointManager,
                false,
                false,
                null);

            pkRangeCache.Setup(p => p.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Documents.Routing.Range<string>>(r => r.Min == FeedRangeEpk.FullRange.Range.Min && r.Max == FeedRangeEpk.FullRange.Range.Max),
                It.IsAny<ITrace>(),
                It.IsAny<bool>())).ReturnsAsync(childRanges);

            PartitionSynchronizerCore partitionSynchronizerCore = new PartitionSynchronizerCore(
                Mock.Of<ContainerInternal>(),
                storeManager.LeaseContainer,
                storeManager.LeaseManager,
                1,
                pkRangeCache.Object,
                Guid.NewGuid().ToString());

            (IEnumerable<DocumentServiceLease> newLeases, bool removeCurrentLease) =
                await partitionSynchronizerCore.HandlePartitionGoneAsync(restoredLease);

            Assert.IsTrue(removeCurrentLease);
            Assert.AreEqual(2, newLeases.Count());
        }

        /// <summary>
        /// Verifies it can create missing leases
        /// </summary>
        [TestMethod]
        public async Task CreateMissingLeases_NoLeases()
        {
            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                new Mock<Common.CollectionCache>(false).Object,
                this.endpointManager,
                false,
                false,
                null);

            List<Documents.PartitionKeyRange> resultingRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange(){ Id = "1", MinInclusive = "", MaxExclusive = "BB" },
                new Documents.PartitionKeyRange(){ Id = "2", MinInclusive = "BB", MaxExclusive = "FF" },
            };

            pkRangeCache.Setup(p => p.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.IsAny<Documents.Routing.Range<string>>(),
                It.IsAny<ITrace>(),
                false))
                .ReturnsAsync(resultingRanges);

            Mock<DocumentServiceLeaseManager> leaseManager = new Mock<DocumentServiceLeaseManager>();

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(c => c.GetAllLeasesAsync())
                .ReturnsAsync(new List<DocumentServiceLeaseCore>());

            PartitionSynchronizerCore partitionSynchronizerCore = new PartitionSynchronizerCore(
                Mock.Of<ContainerInternal>(),
                leaseContainer.Object,
                leaseManager.Object,
                1,
                pkRangeCache.Object,
                Guid.NewGuid().ToString());

            await partitionSynchronizerCore.CreateMissingLeasesAsync();

            leaseManager.Verify(m => m.CreateLeaseIfNotExistAsync(It.Is<PartitionKeyRange>(pkRange => pkRange.Id == resultingRanges[0].Id), It.IsAny<string>()), Times.Once);
            leaseManager.Verify(m => m.CreateLeaseIfNotExistAsync(It.Is<PartitionKeyRange>(pkRange => pkRange.Id == resultingRanges[1].Id), It.IsAny<string>()), Times.Once);
            leaseManager.Verify(m => m.CreateLeaseIfNotExistAsync(It.IsAny<PartitionKeyRange>(), It.IsAny<string>()), Times.Exactly(2));
        }

        /// <summary>
        /// Null routing map during lease store init must throw so Bootstrapper doesn't mark
        /// initialization complete with zero leases.
        /// </summary>
        [TestMethod]
        public async Task CreateMissingLeases_NullOverlappingRanges_ThrowsInvalidOperationException()
        {
            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                new Mock<Common.CollectionCache>(false).Object,
                this.endpointManager,
                false,
                false,
                null);

            pkRangeCache.Setup(p => p.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.IsAny<Documents.Routing.Range<string>>(),
                It.IsAny<ITrace>(),
                false))
                .ReturnsAsync((IReadOnlyList<Documents.PartitionKeyRange>)null);

            Mock<DocumentServiceLeaseManager> leaseManager = new Mock<DocumentServiceLeaseManager>();

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(c => c.GetAllLeasesAsync())
                .ReturnsAsync(new List<DocumentServiceLeaseCore>());

            PartitionSynchronizerCore partitionSynchronizerCore = new PartitionSynchronizerCore(
                Mock.Of<ContainerInternal>(),
                leaseContainer.Object,
                leaseManager.Object,
                1,
                pkRangeCache.Object,
                Guid.NewGuid().ToString());

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => partitionSynchronizerCore.CreateMissingLeasesAsync());

            // No leases must have been created when initialization is aborted this cycle - the retry
            // will happen on the next InitializeAsync attempt.
            leaseManager.Verify(m => m.CreateLeaseIfNotExistAsync(It.IsAny<PartitionKeyRange>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Verifies it can create missing leases if the lease store has some PKRange leases
        /// </summary>
        [TestMethod]
        public async Task CreateMissingLeases_SomePKRangeLeases()
        {
            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                new Mock<Common.CollectionCache>(false).Object,
                this.endpointManager,
                false,
                false,
                null);

            List<Documents.PartitionKeyRange> resultingRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange(){ Id = "1", MinInclusive = "", MaxExclusive = "BB" },
                new Documents.PartitionKeyRange(){ Id = "2", MinInclusive = "BB", MaxExclusive = "FF" },
            };

            pkRangeCache.Setup(p => p.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.IsAny<Documents.Routing.Range<string>>(),
                It.IsAny<ITrace>(),
                false))
                .ReturnsAsync(resultingRanges);

            Mock<DocumentServiceLeaseManager> leaseManager = new Mock<DocumentServiceLeaseManager>();

            // Existing for only one partition
            List<DocumentServiceLease> existingLeases = new List<DocumentServiceLease>()
            {
                new DocumentServiceLeaseCore()
                {
                    LeaseToken = resultingRanges[0].Id,
                    Owner = Guid.NewGuid().ToString()
                }
            };

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(c => c.GetAllLeasesAsync())
                .ReturnsAsync(existingLeases);

            PartitionSynchronizerCore partitionSynchronizerCore = new PartitionSynchronizerCore(
                Mock.Of<ContainerInternal>(),
                leaseContainer.Object,
                leaseManager.Object,
                1,
                pkRangeCache.Object,
                Guid.NewGuid().ToString());

            await partitionSynchronizerCore.CreateMissingLeasesAsync();

            leaseManager.Verify(m => m.CreateLeaseIfNotExistAsync(It.Is<PartitionKeyRange>(pkRange => pkRange.Id == resultingRanges[1].Id), It.IsAny<string>()), Times.Once);
            leaseManager.Verify(m => m.CreateLeaseIfNotExistAsync(It.IsAny<PartitionKeyRange>(), It.IsAny<string>()), Times.Exactly(1));
        }

        [TestMethod]
        public async Task CreateMissingLeases_SomePKRangeAndEPKLeases()
        {
            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                new Mock<Common.CollectionCache>(false).Object,
                this.endpointManager,
                false,
                false,
                null);

            List<Documents.PartitionKeyRange> resultingRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange(){ Id = "1", MinInclusive = "", MaxExclusive = "AA" },
                new Documents.PartitionKeyRange(){ Id = "2", MinInclusive = "AA", MaxExclusive = "CC" },
                new Documents.PartitionKeyRange(){ Id = "3", MinInclusive = "CC", MaxExclusive = "FF" },
            };

            pkRangeCache.Setup(p => p.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.IsAny<Documents.Routing.Range<string>>(),
                It.IsAny<ITrace>(),
                false))
                .ReturnsAsync(resultingRanges);

            Mock<DocumentServiceLeaseManager> leaseManager = new Mock<DocumentServiceLeaseManager>();

            // Existing for only one partition
            List<DocumentServiceLease> existingLeases = new List<DocumentServiceLease>()
            {
                new DocumentServiceLeaseCore()
                {
                    LeaseToken = resultingRanges[0].Id,
                    Owner = Guid.NewGuid().ToString()
                },
                new DocumentServiceLeaseCoreEpk()
                {
                    LeaseToken = "AA-BB",
                    Owner = Guid.NewGuid().ToString(),
                    FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false))
                },
                new DocumentServiceLeaseCoreEpk()
                {
                    LeaseToken = "BB-CC",
                    Owner = Guid.NewGuid().ToString(),
                    FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("BB", "CC", true, false))
                }
            };

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(c => c.GetAllLeasesAsync())
                .ReturnsAsync(existingLeases);

            PartitionSynchronizerCore partitionSynchronizerCore = new PartitionSynchronizerCore(
                Mock.Of<ContainerInternal>(),
                leaseContainer.Object,
                leaseManager.Object,
                1,
                pkRangeCache.Object,
                Guid.NewGuid().ToString());

            await partitionSynchronizerCore.CreateMissingLeasesAsync();

            leaseManager.Verify(m => m.CreateLeaseIfNotExistAsync(It.Is<PartitionKeyRange>(pkRange => pkRange.Id == resultingRanges[2].Id), It.IsAny<string>()), Times.Once);
            leaseManager.Verify(m => m.CreateLeaseIfNotExistAsync(It.IsAny<PartitionKeyRange>(), It.IsAny<string>()), Times.Exactly(1));
        }

        /// <summary>
        /// Offline split: stale parent "0" in lease store; children "1"/"2" advertise Parents=["0"].
        /// CreateMissingLeasesAsync must skip children so the parent's split path creates them with
        /// its real continuation token.
        /// </summary>
        [TestMethod]
        public async Task CreateMissingLeases_StaleParentLeaseFromOfflineSplit_DoesNotCreateChildLeases()
        {
            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                new Mock<Common.CollectionCache>(false).Object,
                this.endpointManager,
                false,
                false,
                null);

            List<Documents.PartitionKeyRange> resultingRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange(){ Id = "1", MinInclusive = "", MaxExclusive = "BB", Parents = new Collection<string> { "0" } },
                new Documents.PartitionKeyRange(){ Id = "2", MinInclusive = "BB", MaxExclusive = "FF", Parents = new Collection<string> { "0" } },
            };

            pkRangeCache.Setup(p => p.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.IsAny<Documents.Routing.Range<string>>(),
                It.IsAny<ITrace>(),
                false))
                .ReturnsAsync(resultingRanges);

            Mock<DocumentServiceLeaseManager> leaseManager = new Mock<DocumentServiceLeaseManager>();

            // Stale parent lease "0" - host never saw the split.
            List<DocumentServiceLease> existingLeases = new List<DocumentServiceLease>()
            {
                new DocumentServiceLeaseCore()
                {
                    LeaseToken = "0",
                    Owner = null,
                    ContinuationToken = "6"
                }
            };

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(c => c.GetAllLeasesAsync())
                .ReturnsAsync(existingLeases);

            PartitionSynchronizerCore partitionSynchronizerCore = new PartitionSynchronizerCore(
                Mock.Of<ContainerInternal>(),
                leaseContainer.Object,
                leaseManager.Object,
                1,
                pkRangeCache.Object,
                Guid.NewGuid().ToString());

            await partitionSynchronizerCore.CreateMissingLeasesAsync();

            // No lease should be created for either child - the parent owns that.
            leaseManager.Verify(m => m.CreateLeaseIfNotExistAsync(It.IsAny<PartitionKeyRange>(), It.IsAny<string>()), Times.Never);
        }
    }
}