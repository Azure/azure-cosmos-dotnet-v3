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

            CosmosContainerFluentDefinitionCore cosmosContainerFluentDefinitionCore = new CosmosContainerFluentDefinitionCore(
                mockContainers.Object,
                containerName,
                FluentSettingsOperation.Create,
                null);

            await cosmosContainerFluentDefinitionCore.ApplyAsync();
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

            CosmosContainerFluentDefinitionCore cosmosContainerFluentDefinitionCore = new CosmosContainerFluentDefinitionCore(
                mockContainers.Object,
                containerName,
                FluentSettingsOperation.Create,
                null);

            CosmosContainerResponse response = await cosmosContainerFluentDefinitionCore.ApplyAsync();

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

            CosmosContainerFluentDefinitionCore cosmosContainerFluentDefinitionCore = new CosmosContainerFluentDefinitionCore(
                mockContainers.Object,
                containerName,
                FluentSettingsOperation.Create,
                partitionKey);

            await cosmosContainerFluentDefinitionCore
                .WithThroughput(2400)
                .ApplyAsync();

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

            CosmosContainerFluentDefinitionCore cosmosContainerFluentDefinitionCore = new CosmosContainerFluentDefinitionCore(
                mockContainers.Object,
                containerName,
                FluentSettingsOperation.Create,
                partitionKey);

            await cosmosContainerFluentDefinitionCore
                .WithTimeToLivePropertyPath(path)
                .ApplyAsync();

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

            CosmosContainerFluentDefinitionCore cosmosContainerFluentDefinitionCore = new CosmosContainerFluentDefinitionCore(
                mockContainers.Object,
                containerName,
                FluentSettingsOperation.Create,
                partitionKey);

            await cosmosContainerFluentDefinitionCore
                .WithDefaultTimeToLive(timeToLive)
                .ApplyAsync();

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

            CosmosContainerFluentDefinitionCore cosmosContainerFluentDefinitionCore = new CosmosContainerFluentDefinitionCore(
                mockContainers.Object,
                containerName,
                FluentSettingsOperation.Create,
                partitionKey);

            await cosmosContainerFluentDefinitionCore
                .WithDefaultTimeToLive((int)timeToLive.TotalSeconds)
                .ApplyAsync();

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

            CosmosContainerFluentDefinitionCore cosmosContainerFluentDefinitionCore = new CosmosContainerFluentDefinitionCore(
                mockContainers.Object,
                containerName,
                FluentSettingsOperation.Create,
                partitionKey);

            await cosmosContainerFluentDefinitionCore
                .WithIndexingPolicy()
                    .WithIndexingMode(IndexingMode.None)
                    .WithoutAutomaticIndexing()
                    .Attach()
                .ApplyAsync();

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

            CosmosContainerFluentDefinitionCore cosmosContainerFluentDefinitionCore = new CosmosContainerFluentDefinitionCore(
                mockContainers.Object,
                containerName,
                FluentSettingsOperation.Create,
                partitionKey);

            await cosmosContainerFluentDefinitionCore
                .WithUniqueKey()
                    .Path(path)
                    .Attach()
                .ApplyAsync();

            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<CosmosContainerSettings>((settings) => settings.UniqueKeyPolicy.UniqueKeys.Count == 1 && path.Equals(settings.UniqueKeyPolicy.UniqueKeys[0].Paths[0])),
                    It.IsAny<int?>(),
                    It.IsAny<CosmosRequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
