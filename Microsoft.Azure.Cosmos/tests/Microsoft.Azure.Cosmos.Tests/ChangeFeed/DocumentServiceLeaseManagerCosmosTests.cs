//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection.Metadata.Ecma335;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tests;
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
            containerMock.Setup(c => c.GetRIDAsync(It.IsAny<CancellationToken>())).ReturnsAsync("test");
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
