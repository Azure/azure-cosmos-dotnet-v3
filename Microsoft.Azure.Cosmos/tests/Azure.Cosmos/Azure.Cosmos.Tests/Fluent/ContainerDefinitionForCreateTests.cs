//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Tests.Fluent
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ContainerDefinitionForCreateTests
    {
        private const string containerName = "someName";
        private const string partitionKey = "pk";
        private const int throughput = 2400;
        private const string path = "/path";
        private static TimeSpan timeToLive = TimeSpan.FromSeconds(25);

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task MissingPKForCreateThrows()
        {
            Mock<CosmosDatabase> mockContainers = new Mock<CosmosDatabase>();

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                GetContext(),
                containerName,
                null);

            await containerFluentDefinitionForCreate.CreateAsync();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task MissingPKForReplace_CallsReadAsync()
        {
            Mock<CosmosContainerResponse> mockContainerResponse = new Mock<CosmosContainerResponse>();
            mockContainerResponse
                .Setup(c => c.Value)
                .Returns(new CosmosContainerProperties() { PartitionKey = new Microsoft.Azure.Documents.PartitionKeyDefinition() { Paths = new Collection<string>() { partitionKey } } });

            Mock<CosmosContainer> mockContainer = new Mock<CosmosContainer>();
            mockContainer
                .Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);

            Mock<CosmosDatabase> mockContainers = new Mock<CosmosDatabase>();
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerProperties>((settings) => settings.PartitionKeyPath.Equals(partitionKey)), 
                    It.IsAny<int?>(), 
                    It.IsAny<RequestOptions>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);

            mockContainers.Setup(c => c.GetContainer(containerName)).Returns(mockContainer.Object);

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                GetContext(),
                containerName,
                null);

            CosmosContainerResponse response = await containerFluentDefinitionForCreate.CreateAsync();

            mockContainer.Verify(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithThroughput()
        {
            Mock<CosmosContainerResponse> mockContainerResponse = new Mock<CosmosContainerResponse>();
            Mock<CosmosDatabase> mockContainers = new Mock<CosmosDatabase>();
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.IsAny<CosmosContainerProperties>(),
                    It.Is<int?>((rus) => rus == throughput),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);
            mockContainers
                .Setup(c => c.Id)
                .Returns(Guid.NewGuid().ToString());

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                GetContext(),
                containerName,
                partitionKey);

            await containerFluentDefinitionForCreate
                .CreateAsync(2400);

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.IsAny<CosmosContainerProperties>(),
                    It.Is<int?>((rus) => rus == throughput),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithDefaultTimeToLiveTimeSpan()
        {
            Mock<CosmosContainerResponse> mockContainerResponse = new Mock<CosmosContainerResponse>();
            Mock<CosmosDatabase> mockContainers = new Mock<CosmosDatabase>();
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerProperties>((settings) => settings.DefaultTimeToLiveInSeconds.Equals((int)timeToLive.TotalSeconds)),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);
            mockContainers
                .Setup(c => c.Id)
                .Returns(Guid.NewGuid().ToString());

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                GetContext(),
                containerName,
                partitionKey);

            await containerFluentDefinitionForCreate
                .WithDefaultTimeToLive(timeToLive)
                .CreateAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerProperties>((settings) => settings.DefaultTimeToLiveInSeconds.Equals((int)timeToLive.TotalSeconds)),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithDefaultTimeToLiveInt()
        {
            Mock<CosmosContainerResponse> mockContainerResponse = new Mock<CosmosContainerResponse>();
            Mock<CosmosDatabase> mockContainers = new Mock<CosmosDatabase>();
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerProperties>((settings) => settings.DefaultTimeToLiveInSeconds.Equals((int)timeToLive.TotalSeconds)),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);
            mockContainers
                .Setup(c => c.Id)
                .Returns(Guid.NewGuid().ToString());

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                GetContext(),
                containerName,
                partitionKey);

            await containerFluentDefinitionForCreate
                .WithDefaultTimeToLive((int)timeToLive.TotalSeconds)
                .CreateAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerProperties>((settings) => settings.DefaultTimeToLiveInSeconds.Equals((int)timeToLive.TotalSeconds)),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithIndexingPolicy()
        {
            Mock<CosmosContainerResponse> mockContainerResponse = new Mock<CosmosContainerResponse>();
            Mock<CosmosDatabase> mockContainers = new Mock<CosmosDatabase>();
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerProperties>((settings) => IndexingMode.None.Equals(settings.IndexingPolicy.IndexingMode) && !settings.IndexingPolicy.Automatic),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);
            mockContainers
                .Setup(c => c.Id)
                .Returns(Guid.NewGuid().ToString());

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                GetContext(),
                containerName,
                partitionKey);

            await containerFluentDefinitionForCreate
                .WithIndexingPolicy()
                    .WithIndexingMode(IndexingMode.None)
                    .WithAutomaticIndexing(false)
                    .Attach()
                .CreateAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerProperties>((settings) => IndexingMode.None.Equals(settings.IndexingPolicy.IndexingMode) && !settings.IndexingPolicy.Automatic),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithUniqueKey()
        {
            Mock<CosmosContainerResponse> mockContainerResponse = new Mock<CosmosContainerResponse>();
            Mock<CosmosDatabase> mockContainers = new Mock<CosmosDatabase>();
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerProperties>((settings) => settings.UniqueKeyPolicy.UniqueKeys.Count == 1 && path.Equals(settings.UniqueKeyPolicy.UniqueKeys[0].Paths[0])),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);
            mockContainers
                .Setup(c => c.Id)
                .Returns(Guid.NewGuid().ToString());

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                GetContext(),
                containerName,
                partitionKey);

            await containerFluentDefinitionForCreate
                .WithUniqueKey()
                    .Path(path)
                    .Attach()
                .CreateAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerProperties>((settings) => settings.UniqueKeyPolicy.UniqueKeys.Count == 1 && path.Equals(settings.UniqueKeyPolicy.UniqueKeys[0].Paths[0])),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        private static CosmosClientContext GetContext()
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            CosmosResponseFactory responseFactory = new CosmosResponseFactory(MockCosmosUtil.Serializer, MockCosmosUtil.Serializer);

            return new ClientContextCore(
                client: client,
                clientOptions: new CosmosClientOptions(),
                userJsonSerializer: MockCosmosUtil.Serializer,
                defaultJsonSerializer: MockCosmosUtil.Serializer,
                sqlQuerySpecSerializer: MockCosmosUtil.Serializer,
                cosmosResponseFactory: responseFactory,
                requestHandler: client.RequestHandler,
                documentClient: new MockDocumentClient());
        }
    }
}
