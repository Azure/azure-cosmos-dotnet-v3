//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Fluent
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
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
        private static readonly TimeSpan timeToLive = TimeSpan.FromSeconds(25);

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

        [Ignore]
        [TestMethod]
        public async Task WithComputedProperties()
        {
            Mock<ContainerResponse> mockContainerResponse = new Mock<ContainerResponse>(MockBehavior.Strict);
            Mock<Database> mockDatabase = new Mock<Database>(MockBehavior.Strict);
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>(MockBehavior.Strict);
            mockDatabase.Setup(m => m.Client).Returns(mockClient.Object);
            mockDatabase
                .Setup(c => c.CreateContainerAsync(
                    It.IsAny<ContainerProperties>(),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);
            mockDatabase
                .Setup(c => c.Id)
                .Returns(Guid.NewGuid().ToString());

            ContainerBuilder containerFluentDefinitionForCreate = new ContainerBuilder(
                mockDatabase.Object,
                containerName,
                partitionKey);

            var definitions = new[]
                {
                    new { Name = "lowerName", Query = "SELECT VALUE LOWER(c.name) FROM c" },
                    new { Name = "estimatedTax", Query = "SELECT VALUE c.salary * 0.2 FROM c" }
                };
            await containerFluentDefinitionForCreate
                .WithComputedProperties()
                    .WithComputedProperty(definitions[0].Name, definitions[0].Query)
                    .WithComputedProperty(definitions[1].Name, definitions[1].Query)
                    .Attach()
                .CreateAsync();

            mockDatabase.Verify(c => c.CreateContainerAsync(
                    It.Is<ContainerProperties>((settings) =>
                            settings.ComputedProperties.Count == 2 &&
                            definitions[0].Name.Equals(settings.ComputedProperties[0].Name) &&
                            definitions[0].Query.Equals(settings.ComputedProperties[0].Query) &&
                            definitions[1].Name.Equals(settings.ComputedProperties[1].Name) &&
                            definitions[1].Query.Equals(settings.ComputedProperties[1].Query)
                        ),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
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

        [TestMethod]
        public async Task ValidateFullTextPolicyAndIndexUsingContainerBuilder()
        {
            string defaultLanguage = "en-US", fullTextPath1 = "/fts1", fullTextPath2 = "/fts2", fullTextPath3 = "/fts3";

            Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
                {
                    new Cosmos.FullTextPath()
                    {
                        Path = fullTextPath1,
                        Language = "en-US",
                    },
                    new Cosmos.FullTextPath()
                    {
                        Path = fullTextPath2,
                        Language = "en-US",
                    },
                    new Cosmos.FullTextPath()
                    {
                        Path = fullTextPath3,
                        Language = "en-US",
                    },
                };

            Mock<ContainerResponse> mockContainerResponse = new Mock<ContainerResponse>();
            mockContainerResponse
                .Setup(x => x.StatusCode)
                .Returns(HttpStatusCode.Created);

            Mock<Database> mockContainers = new Mock<Database>();
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockContainers.Setup(m => m.Client).Returns(mockClient.Object);
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.IsAny<ContainerProperties>(),
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

            ContainerResponse response = await containerFluentDefinitionForCreate
                .WithFullTextPolicy(
                    defaultLanguage: defaultLanguage,
                    fullTextPaths: fullTextPaths)
                .Attach()
                .WithIndexingPolicy()
                    .WithFullTextIndex()
                        .Path(fullTextPath1)
                        .Attach()
                    .WithFullTextIndex()
                        .Path(fullTextPath2)
                        .Attach()
                    .WithFullTextIndex()
                        .Path(fullTextPath3)
                        .Attach()
                .Attach()
                .CreateAsync();

            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<ContainerProperties>((settings) => settings.FullTextPolicy.FullTextPaths.Count == 3
                        && fullTextPath1.Equals(settings.FullTextPolicy.FullTextPaths[0].Path)
                        && "en-US".Equals(settings.FullTextPolicy.FullTextPaths[0].Language)
                        && fullTextPath2.Equals(settings.FullTextPolicy.FullTextPaths[1].Path)
                        && "en-US".Equals(settings.FullTextPolicy.FullTextPaths[1].Language)
                        && fullTextPath3.Equals(settings.FullTextPolicy.FullTextPaths[2].Path)
                        && "en-US".Equals(settings.FullTextPolicy.FullTextPaths[2].Language)
                        && fullTextPath1.Equals(settings.IndexingPolicy.FullTextIndexes[0].Path)
                        && fullTextPath2.Equals(settings.IndexingPolicy.FullTextIndexes[1].Path)
                        && fullTextPath3.Equals(settings.IndexingPolicy.FullTextIndexes[2].Path)),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ValidateVectorEmbeddingsAndIndexingPolicyUsingContainerBuilder()
        {
            string vector1Path = "/vector1", vector2Path = "/vector2", vector3Path = "/vector3";

            Collection<Embedding> embeddings = new Collection<Embedding>()
            {
                new ()
                {
                    Path = vector1Path,
                    DataType = VectorDataType.Int8,
                    DistanceFunction = DistanceFunction.DotProduct,
                    Dimensions = 1200,
                },
                new ()
                {
                    Path = vector2Path,
                    DataType = VectorDataType.Uint8,
                    DistanceFunction = DistanceFunction.Cosine,
                    Dimensions = 3,
                },
                new ()
                {
                    Path = vector3Path,
                    DataType = VectorDataType.Float32,
                    DistanceFunction = DistanceFunction.Euclidean,
                    Dimensions = 400,
                },
            };

            Mock<ContainerResponse> mockContainerResponse = new Mock<ContainerResponse>();
            mockContainerResponse
                .Setup(x => x.StatusCode)
                .Returns(HttpStatusCode.Created);

            Mock<Database> mockContainers = new Mock<Database>();
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockContainers.Setup(m => m.Client).Returns(mockClient.Object);
            mockContainers
                .Setup(c => c.CreateContainerAsync(
                    It.IsAny<ContainerProperties>(),
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

            ContainerResponse response = await containerFluentDefinitionForCreate
                .WithVectorEmbeddingPolicy(embeddings)
                .Attach()
                .WithIndexingPolicy()
                    .WithVectorIndex()
                        .Path(vector1Path, VectorIndexType.Flat)
                        .Attach()
                    .WithVectorIndex()
                        .Path(vector2Path, VectorIndexType.QuantizedFlat)
                        .WithQuantizationByteSize(3)
                        .WithVectorIndexShardKey(new string[] { "/Country" })
                        .Attach()
                    .WithVectorIndex()
                        .Path(vector3Path, VectorIndexType.DiskANN)
                        .WithQuantizationByteSize(2)
                        .WithIndexingSearchListSize(5)
                        .WithVectorIndexShardKey(new string[] { "/ZipCode" })
                        .Attach()
                .Attach()
            .CreateAsync();

            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            mockContainers.Verify(c => c.CreateContainerAsync(
                    It.Is<ContainerProperties>((settings) => settings.VectorEmbeddingPolicy.Embeddings.Count == 3
                        && vector1Path.Equals(settings.VectorEmbeddingPolicy.Embeddings[0].Path)
                        && VectorDataType.Int8.Equals(settings.VectorEmbeddingPolicy.Embeddings[0].DataType)
                        && DistanceFunction.DotProduct.Equals(settings.VectorEmbeddingPolicy.Embeddings[0].DistanceFunction)
                        && 1200.Equals(settings.VectorEmbeddingPolicy.Embeddings[0].Dimensions)
                        && vector2Path.Equals(settings.VectorEmbeddingPolicy.Embeddings[1].Path)
                        && VectorDataType.Uint8.Equals(settings.VectorEmbeddingPolicy.Embeddings[1].DataType)
                        && DistanceFunction.Cosine.Equals(settings.VectorEmbeddingPolicy.Embeddings[1].DistanceFunction)
                        && 3.Equals(settings.VectorEmbeddingPolicy.Embeddings[1].Dimensions)
                        && vector3Path.Equals(settings.VectorEmbeddingPolicy.Embeddings[2].Path)
                        && VectorDataType.Float32.Equals(settings.VectorEmbeddingPolicy.Embeddings[2].DataType)
                        && DistanceFunction.Euclidean.Equals(settings.VectorEmbeddingPolicy.Embeddings[2].DistanceFunction)
                        && 400.Equals(settings.VectorEmbeddingPolicy.Embeddings[2].Dimensions)),
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