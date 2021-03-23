//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Fluent
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
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
            Mock<Database> mockContainers = new Mock<Database>();
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockContainers.Setup(m => m.Client).Returns(mockClient.Object);
            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                containerName,
                null);

            await containerFluentDefinitionForCreate.CreateAsync();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task MissingPKForReplace_CallsReadAsync()
        {
            Mock<ContainerResponse> mockContainerResponse = new Mock<ContainerResponse>();
            mockContainerResponse
                .Setup(c => c.Resource)
                .Returns(new ContainerProperties() { PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new Collection<string>() { partitionKey } } });

            Mock<Container> mockContainer = new Mock<Container>();
            mockContainer
                .Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);

            Mock<Database> mockContainers = new Mock<Database>();
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockContainers.Setup(m => m.Client).Returns(mockClient.Object);
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<ContainerProperties>((settings) => settings.PartitionKeyPath.Equals(partitionKey)), 
                    It.IsAny<int?>(), 
                    It.IsAny<RequestOptions>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);

            mockContainers.Setup(c => c.GetContainer(containerName)).Returns(mockContainer.Object);

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                containerName,
                null);

            ContainerResponse response = await containerFluentDefinitionForCreate.CreateAsync();

            mockContainer.Verify(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithThroughput()
        {
            Mock<ContainerResponse> mockContainerResponse = new Mock<ContainerResponse>();
            Mock<Database> mockContainers = new Mock<Database>();
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockContainers.Setup(m => m.Client).Returns(mockClient.Object);
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.IsAny<ContainerProperties>(),
                    It.Is<int?>((rus) => rus == throughput),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);
            mockContainers
                .Setup(c => c.Id)
                .Returns(Guid.NewGuid().ToString());

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                containerName,
                partitionKey);

            await containerFluentDefinitionForCreate
                .CreateAsync(2400);

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.IsAny<ContainerProperties>(),
                    It.Is<int?>((rus) => rus == throughput),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithTimeToLivePropertyPath()
        {
            Mock<ContainerResponse> mockContainerResponse = new Mock<ContainerResponse>();
            Mock<Database> mockContainers = new Mock<Database>();
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockContainers.Setup(m => m.Client).Returns(mockClient.Object);
            mockContainers
                .Setup(c => c.CreateContainerAsync(
#pragma warning disable CS0612 // Type or member is obsolete
                    It.Is<ContainerProperties>((settings) => settings.TimeToLivePropertyPath.Equals(path)),
#pragma warning restore CS0612 // Type or member is obsolete
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);
            mockContainers
                .Setup(c => c.Id)
                .Returns(Guid.NewGuid().ToString());

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                containerName,
                partitionKey);

            await containerFluentDefinitionForCreate
                .WithTimeToLivePropertyPath(path)
                .CreateAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
#pragma warning disable CS0612 // Type or member is obsolete
                    It.Is<ContainerProperties>((settings) => settings.TimeToLivePropertyPath.Equals(path)),
#pragma warning restore CS0612 // Type or member is obsolete
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithDefaultTimeToLiveTimeSpan()
        {
            Mock<ContainerResponse> mockContainerResponse = new Mock<ContainerResponse>();
            Mock<Database> mockContainers = new Mock<Database>();
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockContainers.Setup(m => m.Client).Returns(mockClient.Object);
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<ContainerProperties>((settings) => settings.DefaultTimeToLive.Equals((int)timeToLive.TotalSeconds)),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);
            mockContainers
                .Setup(c => c.Id)
                .Returns(Guid.NewGuid().ToString());

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                containerName,
                partitionKey);

            await containerFluentDefinitionForCreate
                .WithDefaultTimeToLive(timeToLive)
                .CreateAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<ContainerProperties>((settings) => settings.DefaultTimeToLive.Equals((int)timeToLive.TotalSeconds)),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithDefaultTimeToLiveInt()
        {
            Mock<ContainerResponse> mockContainerResponse = new Mock<ContainerResponse>();
            Mock<Database> mockContainers = new Mock<Database>();
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockContainers.Setup(m => m.Client).Returns(mockClient.Object);
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<ContainerProperties>((settings) => settings.DefaultTimeToLive.Equals((int)timeToLive.TotalSeconds)),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);
            mockContainers
                .Setup(c => c.Id)
                .Returns(Guid.NewGuid().ToString());

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                containerName,
                partitionKey);

            await containerFluentDefinitionForCreate
                .WithDefaultTimeToLive((int)timeToLive.TotalSeconds)
                .CreateAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<ContainerProperties>((settings) => settings.DefaultTimeToLive.Equals((int)timeToLive.TotalSeconds)),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithIndexingPolicy()
        {
            Mock<ContainerResponse> mockContainerResponse = new Mock<ContainerResponse>();
            Mock<Database> mockContainers = new Mock<Database>();
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockContainers.Setup(m => m.Client).Returns(mockClient.Object);
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<ContainerProperties>((settings) => IndexingMode.None.Equals(settings.IndexingPolicy.IndexingMode) && !settings.IndexingPolicy.Automatic),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);
            mockContainers
                .Setup(c => c.Id)
                .Returns(Guid.NewGuid().ToString());

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                containerName,
                partitionKey);

            await containerFluentDefinitionForCreate
                .WithIndexingPolicy()
                    .WithIndexingMode(IndexingMode.None)
                    .WithAutomaticIndexing(false)
                    .Attach()
                .CreateAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<ContainerProperties>((settings) => IndexingMode.None.Equals(settings.IndexingPolicy.IndexingMode) && !settings.IndexingPolicy.Automatic),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithUniqueKey()
        {
            Mock<ContainerResponse> mockContainerResponse = new Mock<ContainerResponse>();
            Mock<Database> mockContainers = new Mock<Database>();
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockContainers.Setup(m => m.Client).Returns(mockClient.Object);
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<ContainerProperties>((settings) => settings.UniqueKeyPolicy.UniqueKeys.Count == 1 && path.Equals(settings.UniqueKeyPolicy.UniqueKeys[0].Paths[0])),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);
            mockContainers
                .Setup(c => c.Id)
                .Returns(Guid.NewGuid().ToString());

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockContainers.Object,
                containerName,
                partitionKey);

            await containerFluentDefinitionForCreate
                .WithUniqueKey()
                    .Path(path)
                    .Attach()
                .CreateAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<ContainerProperties>((settings) => settings.UniqueKeyPolicy.UniqueKeys.Count == 1 && path.Equals(settings.UniqueKeyPolicy.UniqueKeys[0].Paths[0])),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        private static CosmosClientContext GetContext()
        {
            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(x => x.DocumentClient).Returns(new MockDocumentClient());
            return cosmosClientContext.Object;
        }
    }
}
