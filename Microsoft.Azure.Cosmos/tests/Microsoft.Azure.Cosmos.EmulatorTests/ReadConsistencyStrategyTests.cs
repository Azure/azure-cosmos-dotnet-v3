//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// End-to-end tests for <see cref="ReadConsistencyStrategy"/>.
    /// These tests run against the local Cosmos DB emulator in Direct mode
    /// and exercise the full pipeline including the Direct layer (ConsistencyReader / QuorumReader).
    /// </summary>
    [TestClass]
    public class ReadConsistencyStrategyTests : BaseCosmosClientHelper
    {
        private Container Container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/pk"),
                throughput: 10000,
                cancellationToken: this.cancellationToken);

            Assert.IsNotNull(response);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        /// <summary>
        /// Verifies that point reads succeed with Eventual, Session, and LatestCommitted
        /// strategies in Direct mode. Each strategy exercises a different code path in
        /// ConsistencyReader.DeduceReadMode:
        ///   Eventual → ReadMode.Any (single replica)
        ///   Session → ReadMode.Any + session token
        ///   LatestCommitted → ReadMode.Primary (quorum with GLSN barrier)
        /// </summary>
        [TestMethod]
        [DataRow("Eventual", DisplayName = "Eventual - single replica read")]
        [DataRow("Session", DisplayName = "Session - session token read")]
        [DataRow("LatestCommitted", DisplayName = "LatestCommitted - quorum read")]
        public async Task ReadItemWithReadConsistencyStrategy(string strategy)
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            await this.Container.CreateItemAsync(testItem);

            ReadConsistencyStrategy readConsistencyStrategy = (ReadConsistencyStrategy)Enum.Parse(typeof(ReadConsistencyStrategy), strategy);

            ItemResponse<ToDoActivity> readResponse = await this.Container.ReadItemAsync<ToDoActivity>(
                testItem.id,
                new Cosmos.PartitionKey(testItem.pk),
                new ItemRequestOptions { ReadConsistencyStrategy = readConsistencyStrategy });

            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.AreEqual(testItem.id, readResponse.Resource.id);
            Assert.IsTrue(readResponse.Diagnostics.GetClientElapsedTime() > TimeSpan.Zero);
        }

        /// <summary>
        /// Verifies that queries work correctly with ReadConsistencyStrategy set at request level.
        /// The strategy header flows through the query pipeline via RequestInvokerHandler.
        /// </summary>
        [TestMethod]
        public async Task QueryWithReadConsistencyStrategy()
        {
            string uniquePk = "query-rcs-" + Guid.NewGuid().ToString();
            List<ToDoActivity> items = new List<ToDoActivity>();
            for (int i = 0; i < 3; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: uniquePk);
                await this.Container.CreateItemAsync(item);
                items.Add(item);
            }

            QueryRequestOptions queryOptions = new QueryRequestOptions
            {
                ReadConsistencyStrategy = ReadConsistencyStrategy.Session
            };

            FeedIterator<ToDoActivity> iterator = this.Container.GetItemQueryIterator<ToDoActivity>(
                queryText: $"SELECT * FROM c WHERE c.pk = '{uniquePk}'",
                requestOptions: queryOptions);

            List<ToDoActivity> results = new List<ToDoActivity>();
            while (iterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> response = await iterator.ReadNextAsync();
                Assert.IsTrue(response.StatusCode == HttpStatusCode.OK);
                results.AddRange(response);
            }

            Assert.AreEqual(items.Count, results.Count, "Query with ReadConsistencyStrategy should return all items");
        }

        /// <summary>
        /// End-to-end test that request-level ReadConsistencyStrategy overrides client-level.
        /// Uses a RequestHandlerHelper to intercept and verify the actual headers sent on the wire.
        /// </summary>
        [TestMethod]
        public async Task ReadConsistencyStrategyRequestLevelOverridesClientLevel()
        {
            string readConsistencyStrategyHeader = null;
            string consistencyLevelHeader = null;
            RequestHandlerHelper interceptor = new RequestHandlerHelper
            {
                UpdateRequestMessage = (request) =>
                {
                    if (request.OperationType == Documents.OperationType.Read
                        && request.ResourceType == Documents.ResourceType.Document)
                    {
                        readConsistencyStrategyHeader = request.Headers[HttpConstants.HttpHeaders.ReadConsistencyStrategy];
                        consistencyLevelHeader = request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel];
                    }
                }
            };

            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct,
                ReadConsistencyStrategy = ReadConsistencyStrategy.Eventual
            };
            clientOptions.CustomHandlers.Add(interceptor);

            using CosmosClient customClient = TestCommon.CreateCosmosClient(clientOptions);
            Container container = customClient.GetContainer(this.database.Id, this.Container.Id);

            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            await container.CreateItemAsync(testItem);

            // Read with request-level Session strategy (overriding client-level Eventual)
            await container.ReadItemAsync<ToDoActivity>(
                testItem.id,
                new Cosmos.PartitionKey(testItem.pk),
                new ItemRequestOptions { ReadConsistencyStrategy = ReadConsistencyStrategy.Session });

            Assert.AreEqual(
                ReadConsistencyStrategy.Session.ToString(),
                readConsistencyStrategyHeader,
                "Request-level ReadConsistencyStrategy should override client-level");
            Assert.IsNull(
                consistencyLevelHeader,
                "ConsistencyLevel header should not be set when ReadConsistencyStrategy is used");
        }

        /// <summary>
        /// Verifies that change feed operations work with ReadConsistencyStrategy set.
        /// The strategy flows through the same RequestInvokerHandler pipeline as point reads.
        /// </summary>
        [TestMethod]
        public async Task ChangeFeedWithReadConsistencyStrategy()
        {
            // Insert an item so the change feed has something to return
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            await this.Container.CreateItemAsync(testItem);

            ChangeFeedRequestOptions changeFeedOptions = new ChangeFeedRequestOptions
            {
                PageSizeHint = 10,
                ReadConsistencyStrategy = ReadConsistencyStrategy.Eventual
            };

            FeedIterator<ToDoActivity> iterator = this.Container.GetChangeFeedIterator<ToDoActivity>(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.Incremental,
                changeFeedOptions);

            bool foundItem = false;
            while (iterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> response = await iterator.ReadNextAsync();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    foreach (ToDoActivity item in response)
                    {
                        if (item.id == testItem.id)
                        {
                            foundItem = true;
                        }
                    }
                }

                if (response.StatusCode == HttpStatusCode.NotModified || foundItem)
                {
                    break;
                }
            }

            Assert.IsTrue(foundItem, "Change feed with ReadConsistencyStrategy should return the inserted item");
        }

        /// <summary>
        /// Verifies that ReadMany operations work with ReadConsistencyStrategy.
        /// ReadManyRequestOptions has its own ReadConsistencyStrategy property that is
        /// converted to QueryRequestOptions internally.
        /// </summary>
        [TestMethod]
        public async Task ReadManyWithReadConsistencyStrategy()
        {
            List<(string, Cosmos.PartitionKey)> itemsToRead = new List<(string, Cosmos.PartitionKey)>();
            for (int i = 0; i < 3; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                await this.Container.CreateItemAsync(item);
                itemsToRead.Add((item.id, new Cosmos.PartitionKey(item.pk)));
            }

            ReadManyRequestOptions readManyOptions = new ReadManyRequestOptions
            {
                ReadConsistencyStrategy = ReadConsistencyStrategy.Session
            };

            FeedResponse<ToDoActivity> response = await this.Container.ReadManyItemsAsync<ToDoActivity>(
                itemsToRead,
                readManyOptions);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(itemsToRead.Count, response.Count, "ReadMany with ReadConsistencyStrategy should return all requested items");
        }

        /// <summary>
        /// Verifies that the Direct layer's ServerStoreModel rejects requests when both
        /// x-ms-consistency-level and x-ms-cosmos-read-consistency-strategy headers are set.
        /// This guards against SDK regressions that accidentally send both headers.
        /// </summary>
        [TestMethod]
        public async Task DualConsistencyHeadersRejectedWithBadRequest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            await this.Container.CreateItemAsync(testItem);

            // Inject both headers manually via a custom handler to bypass SDK's logic
            // that normally prevents this.
            RequestHandlerHelper headerInjector = new RequestHandlerHelper();
            headerInjector.UpdateRequestMessage = (request) =>
            {
                if (request.OperationType == Documents.OperationType.Read
                    && request.ResourceType == Documents.ResourceType.Document)
                {
                    request.Headers.Set(
                        HttpConstants.HttpHeaders.ConsistencyLevel,
                        Cosmos.ConsistencyLevel.Session.ToString());
                    request.Headers.Set(
                        HttpConstants.HttpHeaders.ReadConsistencyStrategy,
                        ReadConsistencyStrategy.Session.ToString());
                }
            };

            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct
            };
            clientOptions.CustomHandlers.Add(headerInjector);

            using CosmosClient customClient = TestCommon.CreateCosmosClient(clientOptions);
            Container container = customClient.GetContainer(this.database.Id, this.Container.Id);

            try
            {
                await container.ReadItemAsync<ToDoActivity>(
                    testItem.id,
                    new Cosmos.PartitionKey(testItem.pk));

                Assert.Fail("Expected BadRequest when both ConsistencyLevel and ReadConsistencyStrategy headers are set");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
                Assert.IsTrue(
                    ex.Message.Contains("Cannot specify both"),
                    $"Error should mention dual header conflict. Got: {ex.Message}");
            }
        }
    }
}
