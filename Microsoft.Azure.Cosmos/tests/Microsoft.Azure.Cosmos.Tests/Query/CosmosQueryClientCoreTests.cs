//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosQueryClientCoreTests
    {
        private const string ContainerLink = "dbs/database/colls/source";

        [DataTestMethod]
        [DataRow(HttpStatusCode.Forbidden)]
        [DataRow(HttpStatusCode.NotFound)]
        [DataRow(HttpStatusCode.TooManyRequests)]
        [DataRow(HttpStatusCode.ServiceUnavailable)]
        public async Task SecondaryIndexDiscoveryPropagatesCosmosException(HttpStatusCode statusCode)
        {
            CosmosException exception = new CosmosException("Discovery failed.", statusCode, 0, "activity", 0);

            await AssertDiscoveryFailureAsync(exception);
        }

        [TestMethod]
        public async Task SecondaryIndexDiscoveryPropagatesUnexpectedException()
        {
            await AssertDiscoveryFailureAsync(new InvalidOperationException("Discovery failed."));
        }

        [TestMethod]
        public async Task SecondaryIndexDiscoveryPropagatesCancellation()
        {
            await AssertDiscoveryFailureAsync(new OperationCanceledException());
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task SecondaryIndexDiscoveryReturnsSuccessfulMetadata(bool hasIndexes)
        {
            ContainerProperties source = CreateSource();
            IEnumerable<ISecondaryIndexMetadata> metadata = hasIndexes
                ? new ISecondaryIndexMetadata[]
                {
                    new SecondaryIndexMetadata(
                        "index",
                        "indexRid",
                        source.ResourceId,
                        source.PartitionKey,
                        source.IndexingPolicy,
                        new Dictionary<string, PropertyPath>(),
                        ConsistencyLevel.Eventual),
                }
                : Array.Empty<ISecondaryIndexMetadata>();
            Mock<ISecondaryIndexMetadataCache> cache = new Mock<ISecondaryIndexMetadataCache>(MockBehavior.Strict);
            cache.Setup(c => c.GetSecondaryIndexMetadataAsync(source, NoOpTrace.Singleton, CancellationToken.None, false))
                .ReturnsAsync(metadata);
            using MockDocumentClient documentClient = new MockDocumentClient();
            CosmosQueryClientCore queryClient = CreateQueryClient(documentClient, source, cache.Object, true, CancellationToken.None);

            ContainerQueryProperties properties = await queryClient.GetCachedContainerQueryPropertiesAsync(
                ContainerLink, null, NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreSame(metadata, properties.GlobalSecondaryIndexes);
            Assert.AreEqual(source.ResourceId, properties.ResourceId);
            Assert.AreSame(source.PartitionKey, properties.PartitionKeyDefinition);
            cache.Verify(c => c.GetSecondaryIndexMetadataAsync(source, NoOpTrace.Singleton, CancellationToken.None, false), Times.Once);
        }

        [TestMethod]
        public async Task DisabledSecondaryIndexRoutingSkipsDiscovery()
        {
            ContainerProperties source = CreateSource();
            Mock<ISecondaryIndexMetadataCache> cache = new Mock<ISecondaryIndexMetadataCache>(MockBehavior.Strict);
            using MockDocumentClient documentClient = new MockDocumentClient();
            CosmosQueryClientCore queryClient = CreateQueryClient(documentClient, source, cache.Object, false, CancellationToken.None);

            ContainerQueryProperties properties = await queryClient.GetCachedContainerQueryPropertiesAsync(
                ContainerLink, null, NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(0, properties.GlobalSecondaryIndexes.Count());
            Assert.AreEqual(source.ResourceId, properties.ResourceId);
            Assert.AreSame(source.PartitionKey, properties.PartitionKeyDefinition);
            cache.VerifyNoOtherCalls();
        }

        private static async Task AssertDiscoveryFailureAsync<TException>(TException exception)
            where TException : Exception
        {
            ContainerProperties source = CreateSource();
            using CancellationTokenSource cancellationSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationSource.Token;
            Mock<ISecondaryIndexMetadataCache> cache = new Mock<ISecondaryIndexMetadataCache>(MockBehavior.Strict);
            cache.Setup(c => c.GetSecondaryIndexMetadataAsync(source, NoOpTrace.Singleton, cancellationToken, false))
                .Returns(Task.FromException<IEnumerable<ISecondaryIndexMetadata>>(exception));
            using MockDocumentClient documentClient = new MockDocumentClient();
            CosmosQueryClientCore queryClient = CreateQueryClient(documentClient, source, cache.Object, true, cancellationToken);

            TException actual = await Assert.ThrowsExceptionAsync<TException>(() =>
                queryClient.GetCachedContainerQueryPropertiesAsync(ContainerLink, null, NoOpTrace.Singleton, cancellationToken));

            Assert.AreSame(exception, actual);
            cache.Verify(c => c.GetSecondaryIndexMetadataAsync(source, NoOpTrace.Singleton, cancellationToken, false), Times.Once);
        }

        private static ContainerProperties CreateSource()
        {
            ContainerProperties source = ContainerProperties.CreateWithResourceId("sourceRid");
            source.Id = "source";
            source.PartitionKeyPath = "/pk";
            return source;
        }

        private static CosmosQueryClientCore CreateQueryClient(
            MockDocumentClient documentClient,
            ContainerProperties source,
            ISecondaryIndexMetadataCache cache,
            bool enableSecondaryIndexRouting,
            CancellationToken cancellationToken)
        {
            Mock<CosmosClientContext> context = new Mock<CosmosClientContext>(MockBehavior.Strict);
            context.SetupGet(c => c.DocumentClient).Returns(documentClient);
            context.SetupGet(c => c.ClientOptions)
                .Returns(new CosmosClientOptions { EnableSecondaryIndexRouting = enableSecondaryIndexRouting });
            context.SetupGet(c => c.SecondaryIndexMetadataCache).Returns(cache);
            context.Setup(c => c.GetCachedContainerPropertiesAsync(ContainerLink, NoOpTrace.Singleton, cancellationToken))
                .ReturnsAsync(source);
            return new CosmosQueryClientCore(context.Object, null);
        }
    }
}
