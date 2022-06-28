//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PartitionKey = Documents.PartitionKey;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Routing;
    using Newtonsoft.Json.Linq;
    using System.Linq;

    [TestClass]
    public class CosmosItemSessionTokenTests : BaseCosmosClientHelper
    {
        private Container Container = null;
        private ContainerProperties containerSettings = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit(validateSinglePartitionKeyRangeCacheCall: true);
            string PartitionKey = "/pk";
            this.containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                this.containerSettings,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CreateDropItemTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ResetSessionToken(this.Container);
            Assert.IsNull(await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk)));
            ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync<ToDoActivity>(item: testItem);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Resource);
            Assert.IsNotNull(response.Diagnostics);
            long? lsnAfterCreate = await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk));
            Assert.IsNotNull(lsnAfterCreate);
            CosmosTraceDiagnostics diagnostics = (CosmosTraceDiagnostics)response.Diagnostics;
            Assert.IsFalse(diagnostics.IsGoneExceptionHit());
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics.ToString()));
            Assert.IsTrue(diagnostics.GetClientElapsedTime() > TimeSpan.Zero);

            response = await this.Container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.pk));
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Resource);
            Assert.IsNotNull(response.Diagnostics);
            Assert.IsFalse(string.IsNullOrEmpty(response.Diagnostics.ToString()));
            Assert.IsTrue(response.Diagnostics.GetClientElapsedTime() > TimeSpan.Zero);
            long? lsnAfterRead = await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk));
            Assert.IsNotNull(lsnAfterRead);
            Assert.AreEqual(lsnAfterCreate.Value, lsnAfterRead.Value);

            Assert.IsNotNull(response.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.MaxResourceQuota));
            Assert.IsNotNull(response.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.CurrentResourceQuotaUsage));
            ItemResponse<ToDoActivity> deleteResponse = await this.Container.DeleteItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id);
            Assert.IsNotNull(deleteResponse);
            Assert.IsNotNull(response.Diagnostics);
            Assert.IsFalse(string.IsNullOrEmpty(response.Diagnostics.ToString()));
            Assert.IsTrue(response.Diagnostics.GetClientElapsedTime() > TimeSpan.Zero);
            long? lsnAfterDelete = await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk));
            Assert.IsNotNull(lsnAfterDelete);
            Assert.IsTrue(lsnAfterDelete.Value > lsnAfterCreate.Value);
        }

        [TestMethod]
        public async Task ReplaceItemStreamTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ResetSessionToken(this.Container);
            Assert.IsNull(await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk)));

            using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
            {
                //Create the item
                using (ResponseMessage response = await this.Container.CreateItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                }
            }

            long? lsnAfterCreate = await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk));
            Assert.IsNotNull(lsnAfterCreate);

            ResetSessionToken(this.Container);
            Assert.IsNull(await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk)));

            using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
            {
                //Replace a non-existing item. It should fail, and not throw an exception.
                using (ResponseMessage response = await this.Container.ReplaceItemStreamAsync(
                    partitionKey: new Cosmos.PartitionKey("SomeNonExistingId"),
                    id: "SomeNonExistingId",
                    streamPayload: stream))
                {
                    Assert.IsFalse(response.IsSuccessStatusCode);
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, response.ErrorMessage);

                    // Session token should be captured for NotFound with SubStatusCode 0
                    long? lsnAfterNotFound = await GetLSNFromSessionContainer(
                        this.Container, this.containerSettings, new PartitionKey(testItem.pk));
                    Assert.IsNotNull(lsnAfterNotFound);
                    Assert.AreEqual(lsnAfterCreate.Value, lsnAfterNotFound.Value);
                }
            }

            ResetSessionToken(this.Container);
            Assert.IsNull(await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk)));
            
            //Updated the taskNum field
            testItem.taskNum = 9001;
            using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
            {
                using (ResponseMessage response = await this.Container.ReplaceItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id, streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }

                long? lsnAfterReplace= await GetLSNFromSessionContainer(
                    this.Container, this.containerSettings, new PartitionKey(testItem.pk));
                Assert.IsNotNull(lsnAfterReplace);
                Assert.IsTrue(lsnAfterReplace.Value > lsnAfterCreate.Value);

                using (ResponseMessage deleteResponse = await this.Container.DeleteItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id))
                {
                    Assert.IsNotNull(deleteResponse);
                    Assert.AreEqual(deleteResponse.StatusCode, HttpStatusCode.NoContent);
                }

                long? lsnAfterDelete = await GetLSNFromSessionContainer(
                    this.Container, this.containerSettings, new PartitionKey(testItem.pk));
                Assert.IsNotNull(lsnAfterDelete);
                Assert.IsTrue(lsnAfterDelete.Value > lsnAfterReplace.Value);
            }
        }

        [TestMethod]
        public async Task UpsertItemTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ResetSessionToken(this.Container);
            Assert.IsNull(await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk)));

            ItemResponse<ToDoActivity> response = await this.Container.UpsertItemAsync(testItem, partitionKey: new Cosmos.PartitionKey(testItem.pk));
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.IsNotNull(response.Headers.Session);
            long? lsnAfterCreate = await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk));
            Assert.IsNotNull(lsnAfterCreate);

            ResetSessionToken(this.Container);
            Assert.IsNull(await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk)));

            //Updated the taskNum field
            testItem.taskNum = 9001;
            response = await this.Container.UpsertItemAsync(testItem, partitionKey: new Cosmos.PartitionKey(testItem.pk));

            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(response.Headers.Session);

            long? lsnAfterUpsert = await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk));
            Assert.IsNotNull(lsnAfterUpsert);
            Assert.IsTrue(lsnAfterUpsert.Value > lsnAfterCreate.Value);
        }

        [TestMethod]
        public async Task NoSessionTokenCaptureForThrottledUpsertRequestsTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ResetSessionToken(this.Container);
            Assert.IsNull(await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk)));

            ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync<ToDoActivity>(item: testItem);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Resource);
            Assert.IsNotNull(response.Diagnostics);
            string sessionTokenHeaderValue = response.Headers[HttpConstants.HttpHeaders.SessionToken];
            Assert.IsNotNull(sessionTokenHeaderValue);
            
            long? lsnAfterCreate = await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk));
            Assert.IsNotNull(lsnAfterCreate);

            ResetSessionToken(this.Container);
            Assert.IsNull(await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk)));

            Container throttledContainer = TransportClientHelper.GetContainerWithIntercepter(
                this.database.Id,
                this.Container.Id,
                (uri, resourceOperation, documentServiceRequest) => { },
                useGatewayMode: false,
                (uri, resourceOperation, documentServiceRequest) =>
                {
                    StoreResponse throttledResponse = TransportClientHelper.ReturnThrottledStoreResponseOnItemOperation(
                        uri, resourceOperation, documentServiceRequest, Guid.NewGuid(), string.Empty);

                    throttledResponse.Headers.Add(HttpConstants.HttpHeaders.SessionToken, sessionTokenHeaderValue);

                    return throttledResponse;
                },
                this.cosmosClient.DocumentClient.sessionContainer);

            try
            {
                //Updated the taskNum field
                testItem.taskNum = 9001;
                response = await throttledContainer.UpsertItemAsync(testItem, partitionKey: new Cosmos.PartitionKey(testItem.pk));
            }
            catch (CosmosException cosmosException)
            {
                Assert.AreEqual(HttpStatusCode.TooManyRequests, cosmosException.StatusCode);
            }

            long? lsnAfterThrottledRequest = await GetLSNFromSessionContainer(
                this.Container, this.containerSettings, new PartitionKey(testItem.pk));
            Assert.IsNull(lsnAfterThrottledRequest);
        }

        /// <summary>
        /// This test functions as regression coverage for  an issue seen in a CRI - 
        ///     https://portal.microsofticm.com/imp/v3/incidents/details/292305626/home
        /// 
        /// SCENARIO
        /// - App using .Net SDK running queries against Container_1 or Container_2
        /// - Every 24 hours the app changes reading between the two of them
        /// - Before switching the query app to a new Container of either name the Container was deleted, 
        ///   re-created and data was ingested via a Spark job from a different process
        /// - Customer was seeing 404/1002 Not Found/Read Session not available regularly
        /// 
        /// ROOT CAUSE
        /// - SessionContainer and CollectionCache have both Dictionaries for a CollectionName to CollectionRid lookup
        /// - In some places where a stale collection name was identified, not both dictionaries were updated
        /// - Therefore, it was possible that the CollectionCache was updated so CollectionRid on
        ///   DocumentServiceRequest was populated correctly (for the new container) but SessionContainer still mapped 
        ///   the container name to the old CollectionRid - and as such still found the SessionToken captured from 
        ///   the old container
        /// - This could result in either using a stale LSN (LSN captured on old container less than current LSN
        ///   on new container - which would not be an issue - or LSN captured on old container was higher than the 
        ///   latest LSN on the new container - so all subsequent queries would fail with 404/1002
        ///   
        /// IMPACT
        /// - This would only permanently leave the CosmosClient in bad state when 
        ///     - Session consistency is used, 
        ///     - container deletes and recreates are happening,
        ///     - no point operations are used with the same CosmosClient instance (for point operations the 
        ///       RenameCollectionAwareClientRetryPolicy would have recovered the client instance because the session
        ///       cache would have been purged)
        ///     - A CollectionCache refresh is happening after the recreation without also updating the
        ///       SessionContainer (for example via Container.GetFeedRanges - which refreshed ColectionCache 
        ///       but not SessionContainer). If both caches are still stale, a query would trigger a 
        ///       410/1000 (Gone/NameCacheIsStale) for which the retry policy
        ///       would have purged the session container and collection cache.
        ///       
        /// TEST COVERAGE
        /// - Without PR https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3119 this test was consistently 
        ///   failing (either due to 404/1022 or because the requested 
        ///   session token was lower than the latest session token captured on the new container (meaning 
        ///   possible risk of returning outdated data and violating read your own write semantic in theory
        ///   because the requested session token was the last session token seen on the old container 
        ///   (but sent to backend with CollectionRid of new container which might have had further updates)
        /// </summary>
        /// <returns>The task representing the asynchronously processed operation</returns>
        [TestMethod]
        public async Task InvalidSessionTokenAfterContainerRecreationAndCollectionCacheRefreshReproTest()
        {
            // ingestionClinet is dedicated client simulating the writes / container recreation in
            // the separate process - like Spark job
            using CosmosClient ingestionClient = TestCommon.CreateCosmosClient();
            Cosmos.Database ingestionDatabase = ingestionClient.GetDatabase(this.database.Id);

            ContainerProperties multiPartitionContainerSettings =
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/pk");
            Container ingestionContainer = 
                await ingestionDatabase.CreateContainerAsync(multiPartitionContainerSettings);

            const int itemCountToBeIngested = 10;
            string pk = Guid.NewGuid().ToString("N");
            long? latestLsn = null;
            Console.WriteLine("INGEST DOCUMENTS");
            for (int i = 0; i < itemCountToBeIngested; i++)
            {
                ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                testItem.pk = pk;

                ItemResponse<ToDoActivity> response = 
                    await ingestionContainer.CreateItemAsync<ToDoActivity>(item: testItem);
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.Resource);
                Assert.IsNotNull(response.Diagnostics);
                long? lsnAfterCreate = await GetLSNFromSessionContainer(
                    ingestionContainer, multiPartitionContainerSettings, new PartitionKey(pk));
                Assert.IsNotNull(lsnAfterCreate);
                Assert.IsTrue(latestLsn == null || lsnAfterCreate.Value > latestLsn.Value);
                latestLsn = lsnAfterCreate;
                CosmosTraceDiagnostics diagnostics = (CosmosTraceDiagnostics)response.Diagnostics;
                Assert.IsFalse(diagnostics.IsGoneExceptionHit());
                Assert.IsFalse(string.IsNullOrEmpty(diagnostics.ToString()));
                Assert.IsTrue(diagnostics.GetClientElapsedTime() > TimeSpan.Zero);
            }

            // Dedciated query client used only for queries simulating the customer's app
            string lastRequestedSessionToken = null;
            Container queryContainer = TransportClientHelper.GetContainerWithIntercepter(
                this.database.Id,
                ingestionContainer.Id,
                (uri, operation, request) =>
                {
                    if (request.ResourceType == ResourceType.Document &&
                        request.OperationType == OperationType.Query)
                    {
                        lastRequestedSessionToken = request.Headers[HttpConstants.HttpHeaders.SessionToken];
                    }
                },
                false,
                null);

            long? lsnAfterQueryOnOldContainer = null;

            // Issueing two queries - first won't use session tokens yet
            // second will provide session tokens captured from first request in the request to the backend
            for (int i = 0; i < 2; i++)
            {
                Console.WriteLine("RUN QUERY ON OLD CONTAINER ({0})", i);
                using FeedIterator<JObject> queryIteratorOldContainer = queryContainer.GetItemQueryIterator<JObject>(
                    new QueryDefinition("Select c.id FROM c"),
                    continuationToken: null,
                    new QueryRequestOptions
                    {
                        ConsistencyLevel = Cosmos.ConsistencyLevel.Session,
                        PartitionKey = new Cosmos.PartitionKey(pk)
                    });
                int itemCountOldContainer = 0;
                while (queryIteratorOldContainer.HasMoreResults)
                {
                    FeedResponse<JObject> response = await queryIteratorOldContainer.ReadNextAsync();
                    if(i == 0)
                    {
                        string diagnosticString = response.Diagnostics.ToString();
                        Assert.IsTrue(diagnosticString.Contains("PKRangeCache Info("));
                        JObject diagnosticJobject = JObject.Parse(diagnosticString);
                        JToken actualToken = diagnosticJobject.SelectToken("$.children[0].children[?(@.name=='Get Partition Key Ranges')].children[?(@.name=='Try Get Overlapping Ranges')].data");
                        JToken actualNode = actualToken.Children().First().First();

                        Assert.IsTrue(actualNode["Previous Continuation Token"].ToString().Length == 0);
                        Assert.IsTrue(actualNode["Continuation Token"].ToString().Length > 0);
                    }
                    
                    itemCountOldContainer += response.Count;
                }

                Assert.AreEqual(itemCountToBeIngested, itemCountOldContainer);
                lsnAfterQueryOnOldContainer = await GetLSNFromSessionContainer(
                        queryContainer, multiPartitionContainerSettings, new PartitionKey(pk));
                Assert.IsNotNull(lsnAfterQueryOnOldContainer);
                Assert.AreEqual(latestLsn.Value, lsnAfterQueryOnOldContainer.Value);
                if (i == 0)
                {
                    Assert.IsNull(lastRequestedSessionToken);
                }
                else
                {
                    Assert.IsNotNull(lastRequestedSessionToken);
                    Assert.AreEqual(latestLsn.Value, SessionTokenHelper.Parse(lastRequestedSessionToken).LSN);
                }
            }
            
            Console.WriteLine(
                "DELETE CONTAINER {0}",
                (await queryContainer.ReadContainerAsync()).Resource.ResourceId);
            await ingestionContainer.DeleteContainerAsync();

            Console.WriteLine("RECREATING CONTAINER...");
            ContainerResponse ingestionContainerResponse =
                await ingestionDatabase.CreateContainerAsync(multiPartitionContainerSettings);
            ingestionContainer = ingestionContainerResponse.Container;

            string responseSessionTokenValue = 
                ingestionContainerResponse.Headers[HttpConstants.HttpHeaders.SessionToken];
            long? lsnAfterRecreatingContainerFromIngestionClient = responseSessionTokenValue != null ?
                            SessionTokenHelper.Parse(responseSessionTokenValue).LSN : null;
            Console.WriteLine(
                "RECREATED CONTAINER with new CollectionRid: {0} - LSN: {1}",
                ingestionContainerResponse.Resource.ResourceId,
                lsnAfterRecreatingContainerFromIngestionClient);

            // validates that the query container still uses the LSN captured from the old LSN
            long? lsnAfterCreatingNewContainerFromQueryClient = await GetLSNFromSessionContainer(
                    queryContainer, multiPartitionContainerSettings, new PartitionKey(pk));
            Assert.IsNotNull(lsnAfterCreatingNewContainerFromQueryClient);
            Assert.AreEqual(latestLsn.Value, lsnAfterCreatingNewContainerFromQueryClient.Value);

            Console.WriteLine("GET FEED RANGES");
            // this will force a CollectionCache refresh - because no pk ranegs can be identified
            // for the old container anymore
            _ = await queryContainer.GetFeedRangesAsync();


            Console.WriteLine("RUN QUERY ON NEW CONTAINER");
            int itemCountNewContainer = 0;
            using FeedIterator<JObject> queryIteratorNewContainer = queryContainer.GetItemQueryIterator<JObject>(
                new QueryDefinition("Select c.id FROM c"),
                continuationToken: null,
                new QueryRequestOptions
                {
                    ConsistencyLevel = Cosmos.ConsistencyLevel.Session,
                    PartitionKey = new Cosmos.PartitionKey(pk),
                });
            Console.WriteLine("Query iterator created");
            while (queryIteratorNewContainer.HasMoreResults)
            {
                Console.WriteLine("Retrieving first page");
                try
                {
                    FeedResponse<JObject> response = await queryIteratorNewContainer.ReadNextAsync();
                    Console.WriteLine("Request Diagnostics for query against new container: {0}",
                        response.Diagnostics.ToString());
                    itemCountNewContainer += response.Count;
                }
                catch (CosmosException cosmosException)
                {
                    Console.WriteLine("COSMOS EXCEPTION: {0}", cosmosException);
                    throw;
                }
            }

            Assert.AreEqual(0, itemCountNewContainer);
            long? lsnAfterQueryOnNewContainer = await GetLSNFromSessionContainer(
                    queryContainer, multiPartitionContainerSettings, new PartitionKey(pk));
            Assert.IsNotNull(lsnAfterQueryOnNewContainer);
            Assert.IsTrue(
                lastRequestedSessionToken == null || 
                SessionTokenHelper.Parse(lastRequestedSessionToken).LSN == 
                    lsnAfterRecreatingContainerFromIngestionClient,
                $"The requested session token {lastRequestedSessionToken} on the last query request should be null " +
                $"or have LSN '{lsnAfterRecreatingContainerFromIngestionClient}' (which is the LSN after " +
                "re-creating the container) if the session cache or the new CollectionName to Rid mapping was " +
                "correctly populated in the SessionCache.");
        }

        private static async Task<string> GetPKRangeIdForPartitionKey(
            Container container,
            ContainerProperties containerProperties,
            PartitionKey pkValue)
        {
            CollectionRoutingMap collectionRoutingMap = 
                await ((ContainerInternal)container).GetRoutingMapAsync(CancellationToken.None);
            string effectivePK = 
                pkValue.InternalKey.GetEffectivePartitionKeyString(containerProperties.PartitionKey);

            return collectionRoutingMap.GetRangeByEffectivePartitionKey(effectivePK).Id;
        }

        private static async Task<Nullable<long>> GetLSNFromSessionContainer(
            Container container,
            ContainerProperties containerProperties,
            PartitionKey pkValue)
        {
            string path = $"dbs/{container.Database.Id}/colls/{container.Id}";
            string pkRangeId = await GetPKRangeIdForPartitionKey(container, containerProperties, pkValue);
            DocumentServiceRequest dummyRequest = new DocumentServiceRequest(
                OperationType.Read,
                ResourceType.Document,
                path,
                body: null,
                AuthorizationTokenType.PrimaryMasterKey,
                headers: null);

            ISessionToken sessionToken = container
                .Database
                .Client
                .DocumentClient
                .sessionContainer
                .ResolvePartitionLocalSessionToken(
                    dummyRequest,
                    pkRangeId);

            return sessionToken?.LSN;
        }

        private static void ResetSessionToken(Container container)
        {
            string path = $"dbs/{container.Database.Id}/colls/{container.Id}";
            container.Database.Client.DocumentClient.sessionContainer.ClearTokenByCollectionFullname(path);

        }
    }
}