//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
                Mock.Of<Common.CollectionCache>(),
                this.endpointManager);

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
                Mock.Of<Common.CollectionCache>(),
                this.endpointManager);

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
                Mock.Of<Common.CollectionCache>(),
                this.endpointManager);

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
                Mock.Of<Common.CollectionCache>(),
                this.endpointManager);

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
        /// Verifies it can create missing leases
        /// </summary>
        [TestMethod]
        public async Task CreateMissingLeases_NoLeases()
        {
            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                Mock.Of<Common.CollectionCache>(),
                this.endpointManager);

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
        /// Verifies it can create missing leases if the lease store has some PKRange leases
        /// </summary>
        [TestMethod]
        public async Task CreateMissingLeases_SomePKRangeLeases()
        {
            Mock<Routing.PartitionKeyRangeCache> pkRangeCache = new Mock<Routing.PartitionKeyRangeCache>(
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<Documents.IStoreModel>(),
                Mock.Of<Common.CollectionCache>(),
                this.endpointManager);

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
                Mock.Of<Common.CollectionCache>(),
                this.endpointManager);

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
    }
}