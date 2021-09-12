// unset

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PartitionKeyRangeCacheTests
    {
        [TestMethod]
        [Owner("flnarenj")]
        public async Task TestRidRefreshOnNotFoundAsync()
        {
            CosmosClient resourceClient = TestCommon.CreateCosmosClient();

            string dbName = Guid.NewGuid().ToString();
            string containerName = Guid.NewGuid().ToString();

            Database db = await resourceClient.CreateDatabaseAsync(dbName);
            Container container = await db.CreateContainerAsync(containerName, "/_id");

            CosmosClient testClient = TestCommon.CreateCosmosClient();
            ContainerInternal testContainer = (ContainerInlineCore)testClient.GetContainer(dbName, containerName);

            // Populate the RID cache.
            string cachedRidAsync = await testContainer.GetCachedRIDAsync(forceRefresh: false, trace: NoOpTrace.Singleton, cancellationToken: default);

            // Delete the container (using resource client).
            await container.DeleteContainerAsync();

            // Because the RID is cached, this will now try to resolve the collection routing map.
            Assert.AreEqual(cachedRidAsync, await testContainer.GetCachedRIDAsync(forceRefresh: false, trace: NoOpTrace.Singleton, cancellationToken: default));
            CosmosException notFoundException = await Assert.ThrowsExceptionAsync<CosmosException>(() => testContainer.GetRoutingMapAsync(cancellationToken: default));
            Assert.AreEqual(HttpStatusCode.NotFound, notFoundException.StatusCode);

            await db.CreateContainerAsync(containerName, "/_id");

            CollectionRoutingMap collectionRoutingMap = await testContainer.GetRoutingMapAsync(cancellationToken: default);
            Assert.IsNotNull(collectionRoutingMap);
            Assert.AreNotEqual(cachedRidAsync, await testContainer.GetCachedRIDAsync(forceRefresh: false, trace: NoOpTrace.Singleton, cancellationToken: default));

            // Delete the container (using resource client).
            await container.DeleteContainerAsync();

            CollectionRoutingMap collectionRoutingMapFromCache = await testContainer.GetRoutingMapAsync(cancellationToken: default);
            Assert.AreEqual(collectionRoutingMap, collectionRoutingMapFromCache);
        }
    }
}