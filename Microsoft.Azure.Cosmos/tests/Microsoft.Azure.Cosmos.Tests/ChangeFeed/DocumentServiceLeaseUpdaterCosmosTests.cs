//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseUpdaterCosmosTests
    {
        [TestMethod]
        public async Task UpdatesLease()
        {
            string itemId = "1";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("1");
            DocumentServiceLeaseCore leaseToUpdate = new DocumentServiceLeaseCore();

            Mock<ContainerCore> mockedItems = new Mock<ContainerCore>();
            mockedItems.Setup(i => i.ReplaceItemAsync<DocumentServiceLeaseCore>(
                It.Is<DocumentServiceLeaseCore>((lease) => lease == leaseToUpdate),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var itemResponse = new Mock<ItemResponse<DocumentServiceLeaseCore>>();
                    itemResponse.Setup(i => i.Resource).Returns(leaseToUpdate);
                    return itemResponse.Object;
                });

            var updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            var updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            });

            Assert.AreEqual("newHost", updatedLease.Owner);
            Mock.Get(mockedItems.Object)
                .Verify(items => items.ReplaceItemAsync(
                It.Is<DocumentServiceLeaseCore>((lease) => lease == leaseToUpdate),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
            Mock.Get(mockedItems.Object)
                .Verify(items => items.ReadItemAsync<DocumentServiceLeaseCore>(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>((pk) => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task RetriesOnPreconditionFailed()
        {
            string itemId = "1";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("1");
            DocumentServiceLeaseCore leaseToUpdate = new DocumentServiceLeaseCore();

            Mock<ContainerCore> mockedItems = new Mock<ContainerCore>();
            mockedItems.Setup(i => i.ReadItemAsync<DocumentServiceLeaseCore>(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var itemResponse = new Mock<ItemResponse<DocumentServiceLeaseCore>>();
                    itemResponse.Setup(i => i.Resource).Returns(leaseToUpdate);
                    return itemResponse.Object;
                });

            mockedItems.SetupSequence(i => i.ReplaceItemAsync<DocumentServiceLeaseCore>(
                It.Is<DocumentServiceLeaseCore>((lease) => lease == leaseToUpdate),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Throws(new CosmosException(string.Empty, HttpStatusCode.PreconditionFailed, 0, string.Empty, 0))
                .Returns(() =>
                {
                    var itemResponse = new Mock<ItemResponse<DocumentServiceLeaseCore>>();
                    itemResponse.Setup(i => i.Resource).Returns(leaseToUpdate);
                    return Task.FromResult(itemResponse.Object);
                });

            var updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            var updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            });

            Assert.AreEqual("newHost", updatedLease.Owner);
            Mock.Get(mockedItems.Object)
                .Verify(items => items.ReplaceItemAsync(It.Is<DocumentServiceLeaseCore>((lease) => lease == leaseToUpdate), 
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
            Mock.Get(mockedItems.Object)
                .Verify(items => items.ReadItemAsync<DocumentServiceLeaseCore>(It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        [ExpectedException(typeof(LeaseLostException))]
        public async Task ThrowsAfterMaxRetries()
        {
            string itemId = "1";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("1");
            DocumentServiceLeaseCore leaseToUpdate = new DocumentServiceLeaseCore();

            Mock<ContainerCore> mockedItems = new Mock<ContainerCore>();
            mockedItems.Setup(i => i.ReadItemAsync<DocumentServiceLeaseCore>(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var itemResponse = new Mock<ItemResponse<DocumentServiceLeaseCore>>();
                    itemResponse.Setup(i => i.Resource).Returns(leaseToUpdate);
                    return itemResponse.Object;
                });

            mockedItems.Setup(i => i.ReplaceItemAsync<DocumentServiceLeaseCore>(
                It.Is<DocumentServiceLeaseCore>((lease) => lease == leaseToUpdate),
                It.Is<string>((id) => id == itemId), 
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Throws(new CosmosException(string.Empty, HttpStatusCode.PreconditionFailed, 0, string.Empty, 0));

            var updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            var updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            });
        }

        [TestMethod]
        [ExpectedException(typeof(LeaseLostException))]
        public async Task ThrowsOnConflict()
        {
            string itemId = "1";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("1");
            DocumentServiceLeaseCore leaseToUpdate = new DocumentServiceLeaseCore();

            Mock<ContainerCore> mockedItems = new Mock<ContainerCore>();
            mockedItems.Setup(i => i.ReadItemAsync<DocumentServiceLeaseCore>(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var itemResponse = new Mock<ItemResponse<DocumentServiceLeaseCore>>();
                    itemResponse.Setup(i => i.Resource).Returns(leaseToUpdate);
                    return itemResponse.Object;
                });

            mockedItems.SetupSequence(i => i.ReplaceItemAsync<DocumentServiceLeaseCore>(
                It.Is<DocumentServiceLeaseCore>((lease) => lease == leaseToUpdate),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Throws(new CosmosException(string.Empty, HttpStatusCode.Conflict, 0, string.Empty, 0))
                .Returns(() =>
                {
                    var itemResponse = new Mock<ItemResponse<DocumentServiceLeaseCore>>();
                    itemResponse.Setup(i => i.Resource).Returns(leaseToUpdate);
                    return Task.FromResult(itemResponse.Object);
                });

            var updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            var updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            });
        }

        [TestMethod]
        [ExpectedException(typeof(LeaseLostException))]
        public async Task ThrowsOnNotFoundReplace()
        {
            string itemId = "1";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("1");
            DocumentServiceLeaseCore leaseToUpdate = new DocumentServiceLeaseCore();

            Mock<ContainerCore> mockedItems = new Mock<ContainerCore>();
            mockedItems.Setup(i => i.ReadItemAsync<DocumentServiceLeaseCore>(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var itemResponse = new Mock<ItemResponse<DocumentServiceLeaseCore>>();
                    itemResponse.Setup(i => i.Resource).Returns(leaseToUpdate);
                    return itemResponse.Object;
                });

            mockedItems.SetupSequence(i => i.ReplaceItemAsync<DocumentServiceLeaseCore>(
                It.Is<DocumentServiceLeaseCore>((lease) => lease == leaseToUpdate),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    var itemResponse = new Mock<ItemResponse<DocumentServiceLeaseCore>>();
                    itemResponse.Setup(i => i.StatusCode).Returns(HttpStatusCode.NotFound);
                    return Task.FromResult(itemResponse.Object);
                })
                .Returns(() =>
                {
                    var itemResponse = new Mock<ItemResponse<DocumentServiceLeaseCore>>();
                    itemResponse.Setup(i => i.Resource).Returns(leaseToUpdate);
                    return Task.FromResult(itemResponse.Object);
                });

            var updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            var updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            });
        }

        [TestMethod]
        [ExpectedException(typeof(LeaseLostException))]
        public async Task ThrowsOnNotFoundRead()
        {
            string itemId = "1";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("1");
            DocumentServiceLeaseCore leaseToUpdate = new DocumentServiceLeaseCore();

            Mock<ContainerCore> mockedItems = new Mock<ContainerCore>();
            mockedItems.Setup(i => i.ReadItemAsync<DocumentServiceLeaseCore>(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var itemResponse = new Mock<ItemResponse<DocumentServiceLeaseCore>>();
                    itemResponse.Setup(i => i.StatusCode).Returns(HttpStatusCode.NotFound);
                    return itemResponse.Object;
                });

            mockedItems.SetupSequence(i => i.ReplaceItemAsync<DocumentServiceLeaseCore>(
                It.Is<DocumentServiceLeaseCore>((lease) => lease == leaseToUpdate),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Throws(new CosmosException(string.Empty, HttpStatusCode.PreconditionFailed, 0, string.Empty, 0))
                .Returns(() =>
                {
                    var itemResponse = new Mock<ItemResponse<DocumentServiceLeaseCore>>();
                    itemResponse.Setup(i => i.Resource).Returns(leaseToUpdate);
                    return Task.FromResult(itemResponse.Object);
                });

            var updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            var updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            });
        }

        private static ContainerCore GetMockedContainer(Mock<ContainerCore> mockedContainer)
        {
            mockedContainer.Setup(c => c.LinkUri).Returns(new Uri("/dbs/myDb/colls/myColl", UriKind.Relative));
            return mockedContainer.Object;
        }

        private static CosmosClient GetMockedClient()
        {
            DocumentClient documentClient = new MockDocumentClient();

            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder("http://localhost", Guid.NewGuid().ToString());

            return cosmosClientBuilder.Build(documentClient);
        }
    }
}
