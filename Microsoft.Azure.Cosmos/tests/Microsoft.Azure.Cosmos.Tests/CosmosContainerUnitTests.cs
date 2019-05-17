//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class CosmosContainerUnitTests
    {
        [TestMethod]
        public async Task TestGetPartitionKeyPathTokens()
        {
            DocumentClient documentClient = new MockDocumentClient();
            Routing.ClientCollectionCache collectionCache = await documentClient.GetCollectionCacheAsync();
            CosmosClientContextCore context = new CosmosClientContextCore(
                client: null,
                clientConfiguration: null,
                cosmosJsonSerializer: null,
                cosmosResponseFactory: null,
                requestHandler: null,
                documentClient: documentClient,
                documentQueryClient: new Mock<IDocumentQueryClient>().Object
            );
            CosmosDatabaseCore database = new CosmosDatabaseCore(context, "testDatabase");
            CosmosContainerCore container = new CosmosContainerCore(context, database, "testContainer");
            Documents.PartitionKeyDefinition partitionKeyDefinition = await container.GetPartitionKeyDefinitionAsync();
            string[] tokens = partitionKeyDefinition.Paths[0].Split('/', System.StringSplitOptions.RemoveEmptyEntries);
            CollectionAssert.AreEqual(tokens, await container.GetPartitionKeyPathTokensAsync());
        }

        [TestMethod]
        public async Task TestGetPartitionKeyPathTokensThrowsOnNull()
        {
            DocumentClient documentClient = new MockDocumentClient();
            Routing.ClientCollectionCache collectionCache = await documentClient.GetCollectionCacheAsync();
            CosmosClientContextCore context = new CosmosClientContextCore(
                client: null,
                clientConfiguration: null,
                cosmosJsonSerializer: null,
                cosmosResponseFactory: null,
                requestHandler: null,
                documentClient: documentClient,
                documentQueryClient: new Mock<IDocumentQueryClient>().Object
            );
            CosmosDatabaseCore database = new CosmosDatabaseCore(context, "testDatabase");
            Mock<CosmosContainerCore> container = new Mock<CosmosContainerCore>();
            container.Setup(
                m => m.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>())
            ).Returns(Task.FromResult<Documents.PartitionKeyDefinition>(null));

            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => container.Object.GetPartitionKeyPathTokensAsync());
        }

        [TestMethod]
        public async Task TestGetPartitionKeyPathTokensThrowsOnMultiplePartitionKeys()
        {
            DocumentClient documentClient = new MockDocumentClient();
            Routing.ClientCollectionCache collectionCache = await documentClient.GetCollectionCacheAsync();
            CosmosClientContextCore context = new CosmosClientContextCore(
                client: null,
                clientConfiguration: null,
                cosmosJsonSerializer: null,
                cosmosResponseFactory: null,
                requestHandler: null,
                documentClient: documentClient,
                documentQueryClient: new Mock<IDocumentQueryClient>().Object
            );
            CosmosDatabaseCore database = new CosmosDatabaseCore(context, "testDatabase");
            Mock<CosmosContainerCore> container = new Mock<CosmosContainerCore>();
            container.Setup(
                m => m.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>())
            ).Returns(Task.FromResult(new Documents.PartitionKeyDefinition { Paths = new Collection<string> { "a", "b" } }));

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => container.Object.GetPartitionKeyPathTokensAsync());
        }        
    }
}
