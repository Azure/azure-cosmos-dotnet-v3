// unset

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.TransportClientHelper;

    [TestClass]
    public class PartitionKeyRangeCacheTests
    {
        private bool loopBackgroundOperaitons = false;

        [TestInitialize]
        public void TestInitialize()
        {
            this.loopBackgroundOperaitons = false;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.loopBackgroundOperaitons = false;
        }

        [TestMethod]
        public async Task VerifyPkRangeCacheRefreshOnSplitWithErrorsAsync()
        {
            ManualResetEventSlim signalSplitException = new ManualResetEventSlim(false);
            ManualResetEventSlim pkRangesRefreshed = new ManualResetEventSlim(false);

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
                    if (signalSplitException.IsSet)
                    {
                        pkRangesRefreshed.Set();
                    }
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                }

                if (signalSplitException.IsSet)
                {
                    pkRangesRefreshed.Set();
                }

                return null;
            };

            int countSplitExceptions = 0;
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                HttpClientFactory = () => new HttpClient(httpHandlerHelper),
                TransportClientHandlerFactory = (transportClient) => new TransportClientWrapper(
                    transportClient,
                    (uri, resource, dsr) =>
                    {
                        if (dsr.OperationType == Documents.OperationType.Read &&
                            dsr.ResourceType == Documents.ResourceType.Document &&
                            causeSplitExceptionInRntbdCall)
                        {
                            countSplitExceptions++;
                            causeSplitExceptionInRntbdCall = false;

                            signalSplitException.Set();
                            throw new Documents.Routing.PartitionKeyRangeIsSplittingException("Test");
                        }
                    })
            };

            using CosmosClient resourceClient = TestCommon.CreateCosmosClient(clientOptions);

            string dbName = Guid.NewGuid().ToString();
            string containerName = nameof(PartitionKeyRangeCacheTests);

            Database db = await resourceClient.CreateDatabaseIfNotExistsAsync(dbName);
            Container container = await db.CreateContainerIfNotExistsAsync(
                containerName,
                "/pk",
                400);

            // Start a background job that loops forever
            ManualResetEventSlim backgroundOperationReady = new ManualResetEventSlim(false);
            List<Exception> exceptions = new();
            Task backgroundItemOperatios = Task.Factory.StartNew(() => this.CreateAndReadItemBackgroundLoop(container, exceptions, backgroundOperationReady));

            // Wait for the background job to start
            backgroundOperationReady.Wait();

            Assert.IsTrue(this.loopBackgroundOperaitons);
            Assert.AreEqual(2, pkRangeCalls);

            // Cause direct call to hit a split exception and wait for the background job to hit it
            causeSplitExceptionInRntbdCall = true;
            signalSplitException.Wait();
            pkRangesRefreshed.Wait();
            Assert.IsFalse(causeSplitExceptionInRntbdCall);
            Assert.AreEqual(3, pkRangeCalls);

            signalSplitException.Reset();
            pkRangesRefreshed.Reset();

            // Cause another direct call split exception
            causeSplitExceptionInRntbdCall = true;
            signalSplitException.Wait();
            pkRangesRefreshed.Wait();
            Assert.IsFalse(causeSplitExceptionInRntbdCall);

            Assert.AreEqual(4, pkRangeCalls);

            Assert.AreEqual(1, ifNoneMatchValues.Count(x => string.IsNullOrEmpty(x)));
            Assert.AreEqual(3, ifNoneMatchValues.Count(x => x == failedIfNoneMatchValue), $"3 request with same if none value. 1 initial, 2 from the split errors. split exception count: {countSplitExceptions}; {string.Join(';', ifNoneMatchValues)}");

            HashSet<string> verifyUniqueIfNoneHeaderValues = new HashSet<string>();
            foreach (string ifNoneValue in ifNoneMatchValues)
            {
                if (!verifyUniqueIfNoneHeaderValues.Contains(ifNoneValue))
                {
                    verifyUniqueIfNoneHeaderValues.Add(ifNoneValue);
                }
                else if (ifNoneValue != failedIfNoneMatchValue)
                {
                    Assert.AreEqual(failedIfNoneMatchValue, ifNoneValue, $"Multiple duplicates; split exception count: {countSplitExceptions}; {string.Join(';', ifNoneMatchValues)}");
                }
            }

            Assert.AreEqual(0, exceptions.Count, $"Unexpected exceptions: {string.Join(';', exceptions)}");

            await db.DeleteStreamAsync();
        }

        private async Task CreateAndReadItemBackgroundLoop(Container container, List<Exception> exceptions, ManualResetEventSlim backgroundOperationReady)
        {
            this.loopBackgroundOperaitons = true;

            while (this.loopBackgroundOperaitons)
            {
                ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();
                try
                {
                    await container.CreateItemAsync<ToDoActivity>(toDoActivity);
                    await container.ReadItemAsync<ToDoActivity>(toDoActivity.id, new PartitionKey(toDoActivity.pk));
                }
                catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.InternalServerError)
                {
                    // Expected exception caused by failure on pk range refresh
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

                backgroundOperationReady.Set();
            }
        }

        [TestMethod]
        public async Task VerifyPkRangeCacheRefreshOnThrottlesAsync()
        {
            int pkRangeCalls = 0;
            bool causeSplitExceptionInRntbdCall = false;
            HttpClientHandlerHelper httpHandlerHelper = new();
            List<string> ifNoneMatchValues = new();
            httpHandlerHelper.RequestCallBack = (request, cancellationToken) =>
            {
                if (!request.RequestUri.ToString().EndsWith("pkranges"))
                {
                    return null;
                }

                ifNoneMatchValues.Add(request.Headers.IfNoneMatch.ToString());

                pkRangeCalls++;

                // Cause throttle on the init call
                if (pkRangeCalls <= 3)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
                }

                return null;
            };

            int countSplitExceptions = 0;
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                HttpClientFactory = () => new HttpClient(httpHandlerHelper),
                TransportClientHandlerFactory = (transportClient) => new TransportClientWrapper(
                    transportClient,
                    (uri, resource, dsr) =>
                    {
                        if (dsr.ResourceType == Documents.ResourceType.Document &&
                            causeSplitExceptionInRntbdCall)
                        {
                            countSplitExceptions++;
                            causeSplitExceptionInRntbdCall = false;
                            throw new Documents.Routing.PartitionKeyRangeIsSplittingException("Test");
                        }
                    })
            };

            using CosmosClient resourceClient = TestCommon.CreateCosmosClient(clientOptions);

            string dbName = Guid.NewGuid().ToString();
            string containerName = nameof(PartitionKeyRangeCacheTests);

            Database db = await resourceClient.CreateDatabaseIfNotExistsAsync(dbName);
            Container container = await db.CreateContainerIfNotExistsAsync(
                containerName,
                "/pk",
                400);

            ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();
            await container.CreateItemAsync<ToDoActivity>(toDoActivity);
            Assert.AreEqual(5, pkRangeCalls);

            Assert.AreEqual(4, ifNoneMatchValues.Count(x => string.IsNullOrEmpty(x)), "First 3 calls are throttled and 4 succeeds");

            string lastIfNoneMatchValue = ifNoneMatchValues.Last();
            ifNoneMatchValues.Clear();

            pkRangeCalls = 0;
            causeSplitExceptionInRntbdCall = true;
            await container.ReadItemAsync<ToDoActivity>(toDoActivity.id, new PartitionKey(toDoActivity.pk));
            Assert.AreEqual(4, pkRangeCalls);

            Assert.AreEqual(0, ifNoneMatchValues.Count(x => string.IsNullOrEmpty(x)), "The cache is already init. It should never re-initialize the cache.");

            await db.DeleteStreamAsync();
        }

        [TestMethod]
        public async Task VerifyPkRangeCacheRefreshOnTimeoutsAsync()
        {
            int pkRangeCalls = 0;
            HttpClientHandlerHelper httpHandlerHelper = new();
            List<string> ifNoneMatchValues = new();
            httpHandlerHelper.RequestCallBack = (request, cancellationToken) =>
            {
                if (!request.RequestUri.ToString().EndsWith("pkranges"))
                {
                    return null;
                }

                ifNoneMatchValues.Add(request.Headers.IfNoneMatch.ToString());

                pkRangeCalls++;

                // Cause timeout on the init
                if (pkRangeCalls == 1)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.RequestTimeout));
                }

                return null;
            };

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                HttpClientFactory = () => new HttpClient(httpHandlerHelper),
            };

            using CosmosClient resourceClient = TestCommon.CreateCosmosClient(clientOptions);

            string dbName = Guid.NewGuid().ToString();
            string containerName = nameof(PartitionKeyRangeCacheTests);

            Database db = await resourceClient.CreateDatabaseIfNotExistsAsync(dbName);
            Container container = await db.CreateContainerIfNotExistsAsync(
                containerName,
                "/pk",
                400);

            ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();
            await container.CreateItemAsync<ToDoActivity>(toDoActivity);
            Assert.AreEqual(3, pkRangeCalls);

            Assert.AreEqual(2, ifNoneMatchValues.Count(x => string.IsNullOrEmpty(x)), "First call is a 408");

            await db.DeleteStreamAsync();
        }

        [TestMethod]
        public async Task TestRidRefreshOnNotFoundAsync()
        {
            using CosmosClient resourceClient = TestCommon.CreateCosmosClient();

            string dbName = Guid.NewGuid().ToString();
            string containerName = Guid.NewGuid().ToString();

            Database db = await resourceClient.CreateDatabaseAsync(dbName);
            Container container = await db.CreateContainerAsync(containerName, "/_id");

            using CosmosClient testClient = TestCommon.CreateCosmosClient();
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

            await db.DeleteStreamAsync();
        }
    }
}