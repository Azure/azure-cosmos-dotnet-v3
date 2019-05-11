

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
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
            CollectionAssert.AreEqual(tokens, await container.GetPartitionKeyPathTokens());
        }
    }
}
