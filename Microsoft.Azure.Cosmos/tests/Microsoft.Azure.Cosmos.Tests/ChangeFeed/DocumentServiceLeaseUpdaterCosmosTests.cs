//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tests;
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

            Stream leaseStream = new CosmosJsonDotNetSerializer().ToStream(leaseToUpdate);

            Mock<ContainerCore> mockedItems = new Mock<ContainerCore>();
            mockedItems.Setup(i => i.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, string id, PartitionKey pk, ItemRequestOptions options, CancellationToken cancellationToken) =>
                {
                    return new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = stream
                    };
                });

            DocumentServiceLeaseUpdaterCosmos updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            DocumentServiceLease updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            });

            Assert.AreEqual("newHost", updatedLease.Owner);
            Mock.Get(mockedItems.Object)
                .Verify(items => items.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
            Mock.Get(mockedItems.Object)
                .Verify(items => items.ReadItemStreamAsync(
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
            mockedItems.Setup(i => i.ReadItemStreamAsync(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    return new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new CosmosJsonDotNetSerializer().ToStream(leaseToUpdate)
                    };
                });

            mockedItems.SetupSequence(i => i.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    return Task.FromResult(new ResponseMessage(HttpStatusCode.PreconditionFailed));
                })
                .Returns(() =>
                {
                    return Task.FromResult(new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new CosmosJsonDotNetSerializer().ToStream(leaseToUpdate)
                    });
                });

            DocumentServiceLeaseUpdaterCosmos updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            DocumentServiceLease updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            });

            Mock.Get(mockedItems.Object)
                .Verify(items => items.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
            Mock.Get(mockedItems.Object)
                .Verify(items => items.ReadItemStreamAsync(It.Is<string>((id) => id == itemId),
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
            mockedItems.Setup(i => i.ReadItemStreamAsync(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    return new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new CosmosJsonDotNetSerializer().ToStream(leaseToUpdate)
                    };
                });

            mockedItems.Setup(i => i.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    return Task.FromResult(new ResponseMessage(HttpStatusCode.PreconditionFailed));
                });

            DocumentServiceLeaseUpdaterCosmos updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            DocumentServiceLease updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
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
            mockedItems.Setup(i => i.ReadItemStreamAsync(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    return new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new CosmosJsonDotNetSerializer().ToStream(leaseToUpdate)
                    };
                });

            mockedItems.SetupSequence(i => i.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    return Task.FromResult(new ResponseMessage(HttpStatusCode.Conflict));
                })
                .Returns(() =>
                {
                    return Task.FromResult(new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new CosmosJsonDotNetSerializer().ToStream(leaseToUpdate)
                    });
                });

            DocumentServiceLeaseUpdaterCosmos updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            DocumentServiceLease updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
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
            mockedItems.Setup(i => i.ReadItemStreamAsync(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    return new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new CosmosJsonDotNetSerializer().ToStream(leaseToUpdate)
                    };
                });

            mockedItems.SetupSequence(i => i.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    return Task.FromResult(new ResponseMessage(HttpStatusCode.NotFound));
                })
                .Returns(() =>
                {
                    return Task.FromResult(new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new CosmosJsonDotNetSerializer().ToStream(leaseToUpdate)
                    });
                });

            DocumentServiceLeaseUpdaterCosmos updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            DocumentServiceLease updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
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
            mockedItems.Setup(i => i.ReadItemStreamAsync(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    return new ResponseMessage(HttpStatusCode.NotFound);
                });

            mockedItems.SetupSequence(i => i.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    return Task.FromResult(new ResponseMessage(HttpStatusCode.PreconditionFailed));
                })
                .Returns(() =>
                {
                    return Task.FromResult(new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new CosmosJsonDotNetSerializer().ToStream(leaseToUpdate)
                    });
                });

            DocumentServiceLeaseUpdaterCosmos updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            DocumentServiceLease updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
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
