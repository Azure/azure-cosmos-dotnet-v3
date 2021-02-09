//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseManagerCosmosTests
    {
        /// <summary>
        /// Verifies that the update lambda updates the owner.
        /// </summary>
        [TestMethod]
        public async Task AcquireCompletes()
        {
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangePartitionKeyRange("0")
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Func<Func<DocumentServiceLease, DocumentServiceLease>, bool> validateUpdater = (Func<DocumentServiceLease, DocumentServiceLease> updater) =>
            {
                DocumentServiceLease afterUpdateLease = updater(lease);
                return options.HostName == afterUpdateLease.Owner;
            };

            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.Is<Func<DocumentServiceLease, DocumentServiceLease>>(f=> validateUpdater(f))))
                .ReturnsAsync(lease);

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                Mock.Of<ContainerInternal>(),
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            DocumentServiceLease afterAcquire = await documentServiceLeaseManagerCosmos.AcquireAsync(lease);

            Assert.AreEqual(options.HostName, afterAcquire.Owner);
        }

        [TestMethod]
        public async Task CreatesEPKBasedLease()
        {
            string continuation = Guid.NewGuid().ToString();
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            FeedRangeEpk feedRangeEpk = new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false));

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync((Stream stream, PartitionKey partitionKey, ItemRequestOptions options, CancellationToken token) => new ResponseMessage(System.Net.HttpStatusCode.OK) { Content = stream });

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            DocumentServiceLease afterAcquire = await documentServiceLeaseManagerCosmos.CreateLeaseIfNotExistAsync(feedRangeEpk, continuation);

            DocumentServiceLeaseCoreEpk epkBasedLease = (DocumentServiceLeaseCoreEpk)afterAcquire;

            Assert.IsNotNull(epkBasedLease);
            Assert.AreEqual(continuation, afterAcquire.ContinuationToken);
            Assert.AreEqual(feedRangeEpk.Range.Min, ((FeedRangeEpk)epkBasedLease.FeedRange).Range.Min);
            Assert.AreEqual(feedRangeEpk.Range.Max, ((FeedRangeEpk)epkBasedLease.FeedRange).Range.Max);
        }

        [TestMethod]
        public async Task CreatesPartitionKeyBasedLease()
        {
            string continuation = Guid.NewGuid().ToString();
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange()
            {
                Id = "0",
                MinInclusive = "",
                MaxExclusive = "FF"
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync((Stream stream, PartitionKey partitionKey, ItemRequestOptions options, CancellationToken token) => new ResponseMessage(System.Net.HttpStatusCode.OK) { Content = stream });

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            DocumentServiceLease afterAcquire = await documentServiceLeaseManagerCosmos.CreateLeaseIfNotExistAsync(partitionKeyRange, continuation);

            DocumentServiceLeaseCore pkRangeBasedLease = (DocumentServiceLeaseCore)afterAcquire;

            Assert.IsNotNull(pkRangeBasedLease);
            Assert.AreEqual(continuation, afterAcquire.ContinuationToken);
            Assert.AreEqual(partitionKeyRange.Id, pkRangeBasedLease.CurrentLeaseToken);
        }

        /// <summary>
        /// Verifies a Release sets the owner on null
        /// </summary>
        [TestMethod]
        public async Task ReleaseCompletes()
        {
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseId = Guid.NewGuid().ToString(),
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangePartitionKeyRange("0")
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Func<Func<DocumentServiceLease, DocumentServiceLease>, bool> validateUpdater = (Func<DocumentServiceLease, DocumentServiceLease> updater) =>
            {
                DocumentServiceLease afterUpdateLease = updater(lease);
                return afterUpdateLease.Owner == null;
            };

            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.Is<Func<DocumentServiceLease, DocumentServiceLease>>(f => validateUpdater(f))))
                .ReturnsAsync(lease);

            ResponseMessage leaseResponse = new ResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new CosmosJsonDotNetSerializer().ToStream(lease)
            };

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.ReadItemStreamAsync(
                It.Is<string>(id => id == lease.LeaseId),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(leaseResponse);


            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            await documentServiceLeaseManagerCosmos.ReleaseAsync(lease);
        }

        /// <summary>
        /// Verifies that if the updater read a different Owner from the captured in memory, throws a LeaseLost
        /// </summary>
        [ExpectedException(typeof(LeaseLostException))]
        [TestMethod]
        public async Task IfOwnerChangedThrow()
        {
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangePartitionKeyRange("0")
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Func<Func<DocumentServiceLease, DocumentServiceLease>, bool> validateUpdater = (Func<DocumentServiceLease, DocumentServiceLease> updater) =>
            {
                // Simulate dirty read from db
                lease.Owner = Guid.NewGuid().ToString();
                DocumentServiceLease afterUpdateLease = updater(lease);
                return true;
            };

            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.Is<Func<DocumentServiceLease, DocumentServiceLease>>(f => validateUpdater(f))))
                .ReturnsAsync(lease);

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                Mock.Of<ContainerInternal>(),
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            await documentServiceLeaseManagerCosmos.AcquireAsync(lease);
        }

        /// <summary>
        /// When a lease is missing the range information, check that we are adding it
        /// </summary>
        [TestMethod]
        public async Task PopulateMissingRange()
        {
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString()
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Func<Func<DocumentServiceLease, DocumentServiceLease>, bool> validateUpdater = (Func<DocumentServiceLease, DocumentServiceLease> updater) =>
            {
                // Simulate dirty read from db
                DocumentServiceLease afterUpdateLease = updater(lease);
                return afterUpdateLease.FeedRange != null;
            };

            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.Is<Func<DocumentServiceLease, DocumentServiceLease>>(f => validateUpdater(f))))
                .ReturnsAsync(lease);

            Mock<ContainerInternal> containerMock = new Mock<ContainerInternal>();
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            containerMock.Setup(c => c.ClientContext).Returns(mockContext.Object);
            containerMock.Setup(c => c.LinkUri).Returns("http://localhot");
            containerMock.Setup(c => c.GetCachedRIDAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync("test");
            MockDocumentClient mockDocumentClient = new MockDocumentClient();
            mockContext.Setup(c => c.DocumentClient).Returns(mockDocumentClient);

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                containerMock.Object,
                Mock.Of<ContainerInternal>(),
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            DocumentServiceLease afterAcquire = await documentServiceLeaseManagerCosmos.AcquireAsync(lease);

            Assert.IsNotNull(afterAcquire.FeedRange);
        }
    }
}
