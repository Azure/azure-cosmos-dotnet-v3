// unset

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.TransportClientHelper;

    [TestClass]
    public class PartitionKeyRangeCacheTests
    {
        private bool loopBackgroundOperaitons = false;

        [TestMethod]
        public async Task VerifyPkRangeCacheRefreshOnSplitWithErrorsAsync()
        {
            this.loopBackgroundOperaitons = false;

            int throwOnPkRefreshCount = 3;
            int pkRangeCalls = 0;
            bool causeSplitExceptionInRntbdCall = false;
            HttpClientHandlerHelper httpHandlerHelper = new();
            List<string> ifNoneMatchValues = new();
            string failedIfNoneMatchValue = null;
            httpHandlerHelper.RequestCallBack = (request, cancellationToken) =>
            {
                if (!request.RequestUri.ToString().EndsWith("pkranges"))
                {
                    return null;
                }

                ifNoneMatchValues.Add(request.Headers.IfNoneMatch.ToString());

                pkRangeCalls++;

                if (pkRangeCalls == throwOnPkRefreshCount)
                {
                    failedIfNoneMatchValue = request.Headers.IfNoneMatch.ToString();
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                }

                return null;
            };

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                HttpClientFactory = () => new HttpClient(httpHandlerHelper),
                TransportClientHandlerFactory = (transportClient) => new TransportClientWrapper(
                    transportClient,
                    (uri, resource, dsr) =>
                    {
                        if(dsr.OperationType == Documents.OperationType.Read && 
                            dsr.ResourceType == Documents.ResourceType.Document &&
                            causeSplitExceptionInRntbdCall)
                        {
                            causeSplitExceptionInRntbdCall = false;
                            throw new Documents.Routing.PartitionKeyRangeIsSplittingException("Test");
                        }
                    })
            };

            CosmosClient resourceClient = TestCommon.CreateCosmosClient(clientOptions);

            string dbName = "f7d55b47-1dc8-40b3-8389-dd330e7f08f5";
            string containerName = "8e5dece5-1e9c-4e54-8da2-33e9cb7b38df";

            Database db = await resourceClient.CreateDatabaseIfNotExistsAsync(dbName);
            Container container = await db.CreateContainerIfNotExistsAsync(
                containerName, 
                "/pk",
                400);

            // Start a background job that loops forever
            Task backgroundItemOperatios = Task.Factory.StartNew(() => this.CreateAndReadItemBackgroundLoop(container));

            // Wait for the background job to start
            Stopwatch stopwatch = Stopwatch.StartNew();
            while(!this.loopBackgroundOperaitons && stopwatch.Elapsed.TotalSeconds < 30)
            {
                await Task.Delay(TimeSpan.FromSeconds(.5));
            } 

            Assert.IsTrue(this.loopBackgroundOperaitons);
            Assert.AreEqual(2, pkRangeCalls);

            // Cause direct call to hit a split exception and wait for the background job to hit it
            causeSplitExceptionInRntbdCall = true;
            stopwatch = Stopwatch.StartNew();
            while (causeSplitExceptionInRntbdCall && stopwatch.Elapsed.TotalSeconds < 10)
            {
                await Task.Delay(TimeSpan.FromSeconds(.5));
            }
            Assert.IsFalse(causeSplitExceptionInRntbdCall);
            Assert.AreEqual(3, pkRangeCalls);

            // Cause another direct call split exception
            causeSplitExceptionInRntbdCall = true;
            stopwatch = Stopwatch.StartNew();
            while (causeSplitExceptionInRntbdCall && stopwatch.Elapsed.TotalSeconds < 10)
            {
                await Task.Delay(TimeSpan.FromSeconds(.5));
            }

            Assert.IsFalse(causeSplitExceptionInRntbdCall);

            Assert.AreEqual(4, pkRangeCalls);

            Assert.AreEqual(1, ifNoneMatchValues.Count(x => string.IsNullOrEmpty(x)));

            HashSet<string> verifyUniqueIfNoneHeaderValues = new HashSet<string>();
            foreach(string ifNoneValue in ifNoneMatchValues)
            {
                if (!verifyUniqueIfNoneHeaderValues.Contains(ifNoneValue))
                {
                    verifyUniqueIfNoneHeaderValues.Add(ifNoneValue);
                }
                else
                {
                    Assert.AreEqual(failedIfNoneMatchValue, ifNoneValue);
                    // There should only be 1 duplicate. Reset the value to cause failure if another one fails
                    failedIfNoneMatchValue = null;
                }
            }
        }

        private async Task CreateAndReadItemBackgroundLoop(Container container)
        {
            this.loopBackgroundOperaitons = true;

            while(this.loopBackgroundOperaitons)
            {
                ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();
                try
                {
                    await container.CreateItemAsync<ToDoActivity>(toDoActivity);
                    await container.ReadItemAsync<ToDoActivity>(toDoActivity.id, new PartitionKey(toDoActivity.pk));
                }
                catch(CosmosException ce) when (ce.StatusCode == HttpStatusCode.InternalServerError)
                {
                    // Expected exception caused by failure on pk range refresh
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        [TestMethod]
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