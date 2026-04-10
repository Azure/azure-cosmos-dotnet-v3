//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]

    public class DocumentServiceLeaseStoreCosmosTests
    {
        [TestMethod]
        public async Task IsInitializedAsync_IfExists()
        {
            string prefix = "prefix";
            Mock<Container> container = new Mock<Container>();
            container.Setup(c => c.ReadItemStreamAsync(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new ResponseMessage(System.Net.HttpStatusCode.OK));
            Mock<RequestOptionsFactory> requestOptionsFactory = new Mock<RequestOptionsFactory>();
            DocumentServiceLeaseStoreCosmos documentServiceLeaseStoreCosmos = new DocumentServiceLeaseStoreCosmos(
                container.Object,
                prefix,
                requestOptionsFactory.Object);

            Assert.IsTrue(await documentServiceLeaseStoreCosmos.IsInitializedAsync());
        }

        [TestMethod]
        public async Task IsInitializedAsync_IfDoesNotExist()
        {
            string prefix = "prefix";
            Mock<Container> container = new Mock<Container>();
            container.Setup(c => c.ReadItemStreamAsync(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new ResponseMessage(System.Net.HttpStatusCode.NotFound));
            Mock<RequestOptionsFactory> requestOptionsFactory = new Mock<RequestOptionsFactory>();
            DocumentServiceLeaseStoreCosmos documentServiceLeaseStoreCosmos = new DocumentServiceLeaseStoreCosmos(
                container.Object,
                prefix,
                requestOptionsFactory.Object);

            Assert.IsFalse(await documentServiceLeaseStoreCosmos.IsInitializedAsync());
        }

        [TestMethod]
        public async Task AcquireInitializationLockAsync_ConcurrentConflict()
        {
            string prefix = "prefix";
            Mock<Container> container = new Mock<Container>();
            container.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new ResponseMessage(System.Net.HttpStatusCode.Conflict));
            Mock<RequestOptionsFactory> requestOptionsFactory = new Mock<RequestOptionsFactory>();
            DocumentServiceLeaseStoreCosmos documentServiceLeaseStoreCosmos = new DocumentServiceLeaseStoreCosmos(
                container.Object,
                prefix,
                requestOptionsFactory.Object);

            Assert.IsFalse(await documentServiceLeaseStoreCosmos.AcquireInitializationLockAsync(TimeSpan.FromSeconds(1)));
        }

        [TestMethod]
        [ExpectedException(typeof(CosmosException))]
        public async Task AcquireInitializationLockAsync_OnFailure()
        {
            string prefix = "prefix";
            Mock<Container> container = new Mock<Container>();
            container.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new ResponseMessage(System.Net.HttpStatusCode.TooManyRequests));
            Mock<RequestOptionsFactory> requestOptionsFactory = new Mock<RequestOptionsFactory>();
            DocumentServiceLeaseStoreCosmos documentServiceLeaseStoreCosmos = new DocumentServiceLeaseStoreCosmos(
                container.Object,
                prefix,
                requestOptionsFactory.Object);

            await documentServiceLeaseStoreCosmos.AcquireInitializationLockAsync(TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        public async Task AcquireInitializationLockAsync_Success()
        {
            string prefix = "prefix";
            string etag = Guid.NewGuid().ToString();

            dynamic lockDocument = new { id = Guid.NewGuid().ToString() };

            ResponseMessage response = new ResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new CosmosJsonDotNetSerializer().ToStream(lockDocument)
            };

            Mock<Container> container = new Mock<Container>();
            container.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(response);

            Mock<RequestOptionsFactory> requestOptionsFactory = new Mock<RequestOptionsFactory>();
            DocumentServiceLeaseStoreCosmos documentServiceLeaseStoreCosmos = new DocumentServiceLeaseStoreCosmos(
                container.Object,
                prefix,
                requestOptionsFactory.Object);

            Assert.IsTrue(await documentServiceLeaseStoreCosmos.AcquireInitializationLockAsync(TimeSpan.FromSeconds(1)));
        }
    }
}