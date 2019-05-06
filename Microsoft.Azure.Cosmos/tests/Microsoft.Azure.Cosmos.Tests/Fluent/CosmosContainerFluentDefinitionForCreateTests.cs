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
    public class CosmosContainerFluentDefinitionForCreateTests
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
            Mock<CosmosContainers> mockContainers = new Mock<CosmosContainers>();

            CosmosContainerFluentDefinitionForCreate CosmosContainerFluentDefinitionForCreate = new CosmosContainerFluentDefinitionForCreate(
                mockContainers.Object,
                containerName,
                null);

            await CosmosContainerFluentDefinitionForCreate.CreateAsync();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task MissingPKForReplace_CallsReadAsync()
        {
            Mock<CosmosContainerResponse> mockContainerResponse = new Mock<CosmosContainerResponse>();
            mockContainerResponse
                .Setup(c => c.Resource)
                .Returns(new CosmosContainerSettings() { PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new Collection<string>() { partitionKey } } });

            Mock<CosmosContainer> mockContainer = new Mock<CosmosContainer>();
            mockContainer
                .Setup(c => c.ReadAsync(It.IsAny<CosmosContainerRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);

            Mock<CosmosContainers> mockContainers = new Mock<CosmosContainers>();
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerSettings>((settings) => settings.PartitionKeyPath.Equals(partitionKey)), 
                    It.IsAny<int?>(), 
                    It.IsAny<CosmosRequestOptions>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);

            mockContainers.Setup(c => c[containerName]).Returns(mockContainer.Object);

            CosmosContainerFluentDefinitionForCreate CosmosContainerFluentDefinitionForCreate = new CosmosContainerFluentDefinitionForCreate(
                mockContainers.Object,
                containerName,
                null);

            CosmosContainerResponse response = await CosmosContainerFluentDefinitionForCreate.CreateAsync();

            mockContainer.Verify(c => c.ReadAsync(It.IsAny<CosmosContainerRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithThroughput()
        {
            Mock<CosmosContainerResponse> mockContainerResponse = new Mock<CosmosContainerResponse>();
            Mock<CosmosContainers> mockContainers = new Mock<CosmosContainers>();
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.IsAny<CosmosContainerSettings>(),
                    It.Is<int?>((rus) => rus == throughput),
                    It.IsAny<CosmosRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);

            CosmosContainerFluentDefinitionForCreate CosmosContainerFluentDefinitionForCreate = new CosmosContainerFluentDefinitionForCreate(
                mockContainers.Object,
                containerName,
                partitionKey);

            await CosmosContainerFluentDefinitionForCreate
                .CreateAsync(2400);

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.IsAny<CosmosContainerSettings>(),
                    It.Is<int?>((rus) => rus == throughput),
                    It.IsAny<CosmosRequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithTimeToLivePropertyPath()
        {
            Mock<CosmosContainerResponse> mockContainerResponse = new Mock<CosmosContainerResponse>();
            Mock<CosmosContainers> mockContainers = new Mock<CosmosContainers>();
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerSettings>((settings) => settings.TimeToLivePropertyPath.Equals(path)),
                    It.IsAny<int?>(),
                    It.IsAny<CosmosRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);

            CosmosContainerFluentDefinitionForCreate CosmosContainerFluentDefinitionForCreate = new CosmosContainerFluentDefinitionForCreate(
                mockContainers.Object,
                containerName,
                partitionKey);

            await CosmosContainerFluentDefinitionForCreate
                .TimeToLivePropertyPath(path)
                .CreateAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerSettings>((settings) => settings.TimeToLivePropertyPath.Equals(path)),
                    It.IsAny<int?>(),
                    It.IsAny<CosmosRequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithDefaultTimeToLiveTimeSpan()
        {
            Mock<CosmosContainerResponse> mockContainerResponse = new Mock<CosmosContainerResponse>();
            Mock<CosmosContainers> mockContainers = new Mock<CosmosContainers>();
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerSettings>((settings) => settings.DefaultTimeToLive.Equals((int)timeToLive.TotalSeconds)),
                    It.IsAny<int?>(),
                    It.IsAny<CosmosRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);

            CosmosContainerFluentDefinitionForCreate CosmosContainerFluentDefinitionForCreate = new CosmosContainerFluentDefinitionForCreate(
                mockContainers.Object,
                containerName,
                partitionKey);

            await CosmosContainerFluentDefinitionForCreate
                .DefaultTimeToLive(timeToLive)
                .CreateAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerSettings>((settings) => settings.DefaultTimeToLive.Equals((int)timeToLive.TotalSeconds)),
                    It.IsAny<int?>(),
                    It.IsAny<CosmosRequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithDefaultTimeToLiveInt()
        {
            Mock<CosmosContainerResponse> mockContainerResponse = new Mock<CosmosContainerResponse>();
            Mock<CosmosContainers> mockContainers = new Mock<CosmosContainers>();
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerSettings>((settings) => settings.DefaultTimeToLive.Equals((int)timeToLive.TotalSeconds)),
                    It.IsAny<int?>(),
                    It.IsAny<CosmosRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);

            CosmosContainerFluentDefinitionForCreate CosmosContainerFluentDefinitionForCreate = new CosmosContainerFluentDefinitionForCreate(
                mockContainers.Object,
                containerName,
                partitionKey);

            await CosmosContainerFluentDefinitionForCreate
                .DefaultTimeToLive((int)timeToLive.TotalSeconds)
                .CreateAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerSettings>((settings) => settings.DefaultTimeToLive.Equals((int)timeToLive.TotalSeconds)),
                    It.IsAny<int?>(),
                    It.IsAny<CosmosRequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithIndexingPolicy()
        {
            Mock<CosmosContainerResponse> mockContainerResponse = new Mock<CosmosContainerResponse>();
            Mock<CosmosContainers> mockContainers = new Mock<CosmosContainers>();
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerSettings>((settings) => IndexingMode.None.Equals(settings.IndexingPolicy.IndexingMode) && !settings.IndexingPolicy.Automatic),
                    It.IsAny<int?>(),
                    It.IsAny<CosmosRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);

            CosmosContainerFluentDefinitionForCreate CosmosContainerFluentDefinitionForCreate = new CosmosContainerFluentDefinitionForCreate(
                mockContainers.Object,
                containerName,
                partitionKey);

            await CosmosContainerFluentDefinitionForCreate
                .IndexingPolicy()
                    .IndexingMode(IndexingMode.None)
                    .AutomaticIndexing(false)
                    .Attach()
                .CreateAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerSettings>((settings) => IndexingMode.None.Equals(settings.IndexingPolicy.IndexingMode) && !settings.IndexingPolicy.Automatic),
                    It.IsAny<int?>(),
                    It.IsAny<CosmosRequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task WithUniqueKey()
        {
            Mock<CosmosContainerResponse> mockContainerResponse = new Mock<CosmosContainerResponse>();
            Mock<CosmosContainers> mockContainers = new Mock<CosmosContainers>();
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerSettings>((settings) => settings.UniqueKeyPolicy.UniqueKeys.Count == 1 && path.Equals(settings.UniqueKeyPolicy.UniqueKeys[0].Paths[0])),
                    It.IsAny<int?>(),
                    It.IsAny<CosmosRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);

            CosmosContainerFluentDefinitionForCreate CosmosContainerFluentDefinitionForCreate = new CosmosContainerFluentDefinitionForCreate(
                mockContainers.Object,
                containerName,
                partitionKey);

            await CosmosContainerFluentDefinitionForCreate
                .UniqueKey()
                    .Path(path)
                    .Attach()
                .CreateAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerSettings>((settings) => settings.UniqueKeyPolicy.UniqueKeys.Count == 1 && path.Equals(settings.UniqueKeyPolicy.UniqueKeys[0].Paths[0])),
                    It.IsAny<int?>(),
                    It.IsAny<CosmosRequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
