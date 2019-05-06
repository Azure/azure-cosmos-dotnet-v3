//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using JsonReader = Json.JsonReader;
    using JsonWriter = Json.JsonWriter;

    [TestClass]
    public class CosmosItemTests : BaseCosmosClientHelper
    {
        private CosmosContainer Container = null;
        private CosmosDefaultJsonSerializer jsonSerializer = null;
        private CosmosContainerSettings containerSettings = null;

        private static CosmosContainer fixedContainer = null;
        private static readonly string utc_date = DateTime.UtcNow.ToString("r");

        private static readonly string PreNonPartitionedMigrationApiVersion = "2018-09-17";
        private static readonly string nonPartitionContainerId = "fixed-Container";
        private static readonly string nonPartitionItemId = "fixed-Container-Item";

        private static readonly string undefinedPartitionItemId = "undefined-partition-Item";

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            this.containerSettings = new CosmosContainerSettings(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            CosmosContainerResponse response = await this.database.Containers.CreateContainerAsync(
                this.containerSettings,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
            this.jsonSerializer = new CosmosDefaultJsonSerializer();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CreateDropItemTest()
        {
            ToDoActivity testItem = this.CreateRandomToDoActivity();
            CosmosItemResponse<ToDoActivity> response = await this.Container.Items.CreateItemAsync<ToDoActivity>(partitionKey: testItem.status, item: testItem);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.MaxResourceQuota);
            Assert.IsNotNull(response.CurrentResourceQuotaUsage);
            CosmosItemResponse<ToDoActivity> deleteResponse = await this.Container.Items.DeleteItemAsync<ToDoActivity>(partitionKey: testItem.status, id: testItem.id);
            Assert.IsNotNull(deleteResponse);
        }

        [TestMethod]
        public async Task CreateDropItemUndefinedPartitionKeyTest()
        {
            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString()
            };

            CosmosItemResponse<dynamic> response = await this.Container.Items.CreateItemAsync<dynamic>(partitionKey: Undefined.Value, item: testItem);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.MaxResourceQuota);
            Assert.IsNotNull(response.CurrentResourceQuotaUsage);

            CosmosItemResponse<dynamic> deleteResponse = await this.Container.Items.DeleteItemAsync<dynamic>(partitionKey: "[{}]", id: testItem.id);
            Assert.IsNotNull(deleteResponse);
        }

        [TestMethod]
        public async Task CreateDropItemStreamTest()
        {
            ToDoActivity testItem = this.CreateRandomToDoActivity();
            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                using (CosmosResponseMessage response = await this.Container.Items.CreateItemStreamAsync(partitionKey: testItem.status, streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                    Assert.IsTrue(response.Headers.RequestCharge > 0);
                    Assert.IsNotNull(response.Headers.ActivityId);
                    Assert.IsNotNull(response.Headers.ETag);
                }
            }

            using (CosmosResponseMessage deleteResponse = await this.Container.Items.DeleteItemStreamAsync(partitionKey: testItem.status, id: testItem.id))
            {
                Assert.IsNotNull(deleteResponse);
                Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
                Assert.IsTrue(deleteResponse.Headers.RequestCharge > 0);
                Assert.IsNotNull(deleteResponse.Headers.ActivityId);
            }
        }

        [TestMethod]
        public async Task UpsertItemStreamTest()
        {
            ToDoActivity testItem = this.CreateRandomToDoActivity();
            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                //Create the object
                using (CosmosResponseMessage response = await this.Container.Items.UpsertItemStreamAsync(partitionKey: testItem.status, streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                    using (StreamReader str = new StreamReader(response.Content))
                    {
                        string responseContentAsString = await str.ReadToEndAsync();
                    }
                }
            }

            //Updated the taskNum field
            testItem.taskNum = 9001;
            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                using (CosmosResponseMessage response = await this.Container.Items.UpsertItemStreamAsync(partitionKey: testItem.status, streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }
            }
            using (CosmosResponseMessage deleteResponse = await this.Container.Items.DeleteItemStreamAsync(partitionKey: testItem.status, id: testItem.id))
            {
                Assert.IsNotNull(deleteResponse);
                Assert.AreEqual(deleteResponse.StatusCode, HttpStatusCode.NoContent);
            }
        }

        [TestMethod]
        public async Task ReplaceItemStreamTest()
        {
            ToDoActivity testItem = this.CreateRandomToDoActivity();
            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                //Replace a non-existing item. It should fail, and not throw an exception.
                using (CosmosResponseMessage response = await this.Container.Items.ReplaceItemStreamAsync(
                    partitionKey: testItem.status,
                    id: testItem.id,
                    streamPayload: stream))
                {
                    Assert.IsFalse(response.IsSuccessStatusCode);
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, response.ErrorMessage);
                }
            }

            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                //Create the item
                using (CosmosResponseMessage response = await this.Container.Items.CreateItemStreamAsync(partitionKey: testItem.status, streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                }
            }

            //Updated the taskNum field
            testItem.taskNum = 9001;
            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                using (CosmosResponseMessage response = await this.Container.Items.ReplaceItemStreamAsync(partitionKey: testItem.status, id: testItem.id, streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }

                using (CosmosResponseMessage deleteResponse = await this.Container.Items.DeleteItemStreamAsync(partitionKey: testItem.status, id: testItem.id))
                {
                    Assert.IsNotNull(deleteResponse);
                    Assert.AreEqual(deleteResponse.StatusCode, HttpStatusCode.NoContent);
                }
            }
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod]
        public async Task ItemStreamIterator(bool useStatelessIterator)
        {
            IList<ToDoActivity> deleteList = await this.CreateRandomItems(3, randomPartitionKey: true);
            HashSet<string> itemIds = deleteList.Select(x => x.id).ToHashSet<string>();

            string lastContinuationToken = null;
            int pageSize = 1;
            CosmosItemRequestOptions requestOptions = new CosmosItemRequestOptions();
            CosmosFeedIterator setIterator =
                this.Container.Items.GetItemStreamIterator(maxItemCount: pageSize, continuationToken: lastContinuationToken, requestOptions: requestOptions);

            while (setIterator.HasMoreResults)
            {
                if (useStatelessIterator)
                {
                    setIterator = this.Container.Items.GetItemStreamIterator(maxItemCount: pageSize, continuationToken: lastContinuationToken, requestOptions: requestOptions);
                }

                using (CosmosResponseMessage responseMessage =
                    await setIterator.FetchNextSetAsync(this.cancellationToken))
                {
                    lastContinuationToken = responseMessage.Headers.Continuation;

                    Collection<ToDoActivity> response = new CosmosDefaultJsonSerializer().FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                    foreach (ToDoActivity toDoActivity in response)
                    {
                        if (itemIds.Contains(toDoActivity.id))
                        {
                            itemIds.Remove(toDoActivity.id);
                        }
                    }

                }

            }

            Assert.IsNull(lastContinuationToken);
            Assert.AreEqual(itemIds.Count, 0);
        }

        [TestMethod]
        public async Task ItemIterator()
        {
            IList<ToDoActivity> deleteList = await this.CreateRandomItems(3, randomPartitionKey: true);
            HashSet<string> itemIds = deleteList.Select(x => x.id).ToHashSet<string>();
            CosmosFeedIterator<ToDoActivity> setIterator =
                this.Container.Items.GetItemIterator<ToDoActivity>();
            while (setIterator.HasMoreResults)
            {
                foreach (ToDoActivity toDoActivity in await setIterator.FetchNextSetAsync(this.cancellationToken))
                {
                    if (itemIds.Contains(toDoActivity.id))
                    {
                        itemIds.Remove(toDoActivity.id);
                    }
                }
            }

            Assert.AreEqual(itemIds.Count, 0);
        }

        [DataRow(1, 1)]
        [DataRow(5, 5)]
        [DataRow(6, 2)]
        [DataTestMethod]
        public async Task QuerySinglePartitionItemStreamTest(int perPKItemCount, int maxItemCount)
        {
            IList<ToDoActivity> deleteList = deleteList = await this.CreateRandomItems(pkCount: 3, perPKItemCount: perPKItemCount, randomPartitionKey: true);
            ToDoActivity find = deleteList.First();

            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("select * from r");

            int iterationCount = 0;
            int totalReadItem = 0;
            int expectedIterationCount = perPKItemCount / maxItemCount;
            string lastContinuationToken = null;

            do
            {
                iterationCount++;
                CosmosFeedIterator setIterator = this.Container.Items
                    .CreateItemQueryAsStream(sql, 1, find.status,
                        maxItemCount: maxItemCount,
                        continuationToken: lastContinuationToken,
                        requestOptions: new CosmosQueryRequestOptions());

                CosmosResponseMessage response = await setIterator.FetchNextSetAsync();
                lastContinuationToken = response.Headers.Continuation;
                Trace.TraceInformation($"ContinuationToken: {lastContinuationToken}");
                JsonSerializer serializer = new JsonSerializer();

                using (StreamReader sr = new StreamReader(response.Content))
                using (JsonTextReader jtr = new JsonTextReader(sr))
                {
                    ToDoActivity[] results = serializer.Deserialize<ToDoActivity[]>(jtr);
                    ToDoActivity[] readTodoActivities = results.OrderBy(e => e.id)
                        .ToArray();

                    ToDoActivity[] expectedTodoActivities = deleteList
                            .Where(e => e.status == find.status)
                            .Where(e => readTodoActivities.Any(e1 => e1.id == e.id))
                            .OrderBy(e => e.id)
                            .ToArray();

                    totalReadItem += expectedTodoActivities.Length;
                    string expectedSerialized = JsonConvert.SerializeObject(expectedTodoActivities);
                    string readSerialized = JsonConvert.SerializeObject(readTodoActivities);
                    Trace.TraceInformation($"Expected: {Environment.NewLine} {expectedSerialized}");
                    Trace.TraceInformation($"Read: {Environment.NewLine} {readSerialized}");

                    int count = results.Length;
                    Assert.AreEqual(maxItemCount, count);

                    Assert.AreEqual(expectedSerialized, readSerialized);

                    Assert.AreEqual(maxItemCount, expectedTodoActivities.Length);
                }
            }
            while (lastContinuationToken != null);

            Assert.AreEqual(expectedIterationCount, iterationCount);
            Assert.AreEqual(perPKItemCount, totalReadItem);
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemMultiplePartitionQuery()
        {
            IList<ToDoActivity> deleteList = await this.CreateRandomItems(3, randomPartitionKey: true);

            ToDoActivity find = deleteList.First();
            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("select * from toDoActivity t where t.id = '" + find.id + "'");

            CosmosQueryRequestOptions requestOptions = new CosmosQueryRequestOptions()
            {
                MaxBufferedItemCount = 10,
                ResponseContinuationTokenLimitInKb = 500
            };

            CosmosFeedIterator<ToDoActivity> setIterator =
                this.Container.Items.CreateItemQuery<ToDoActivity>(sql, maxConcurrency: 1, maxItemCount: 1, requestOptions: requestOptions);
            while (setIterator.HasMoreResults)
            {
                CosmosFeedResponse<ToDoActivity> iter = await setIterator.FetchNextSetAsync();
                Assert.AreEqual(1, iter.Count());
                ToDoActivity response = iter.First();
                Assert.AreEqual(find.id, response.id);
            }
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemMultiplePartitionOrderByQueryStream()
        {
            IList<ToDoActivity> deleteList = await this.CreateRandomItems(300, randomPartitionKey: true);

            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("SELECT * FROM toDoActivity t ORDER BY t.taskNum ");

            CosmosQueryRequestOptions requestOptions = new CosmosQueryRequestOptions()
            {
                MaxBufferedItemCount = 10,
                ResponseContinuationTokenLimitInKb = 500
            };

            List<ToDoActivity> resultList = new List<ToDoActivity>();
            double totalRequstCharge = 0;
            CosmosFeedIterator setIterator =
                this.Container.Items.CreateItemQueryAsStream(sql, maxConcurrency: 5, maxItemCount: 1, requestOptions: requestOptions);
            while (setIterator.HasMoreResults)
            {
                CosmosResponseMessage iter = await setIterator.FetchNextSetAsync();
                Assert.IsTrue(iter.IsSuccessStatusCode);
                Assert.IsNull(iter.ErrorMessage);
                totalRequstCharge += iter.Headers.RequestCharge;

                ToDoActivity[] activities = this.jsonSerializer.FromStream<ToDoActivity[]>(iter.Content);
                Assert.AreEqual(1, activities.Length);
                ToDoActivity response = activities.First();
                resultList.Add(response);
            }

            Assert.AreEqual(deleteList.Count, resultList.Count);
            Assert.IsTrue(totalRequstCharge > 0);

            List<ToDoActivity> verifiedOrderBy = deleteList.OrderBy(x => x.taskNum).ToList();
            for (int i = 0; i < verifiedOrderBy.Count(); i++)
            {
                Assert.AreEqual(verifiedOrderBy[i].taskNum, resultList[i].taskNum);
                Assert.AreEqual(verifiedOrderBy[i].id, resultList[i].id);
            }
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemMultiplePartitionQueryStream()
        {
            IList<ToDoActivity> deleteList = await this.CreateRandomItems(101, randomPartitionKey: true);
            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("SELECT * FROM toDoActivity t");

            List<ToDoActivity> resultList = new List<ToDoActivity>();
            double totalRequstCharge = 0;
            CosmosFeedIterator setIterator =
                this.Container.Items.CreateItemQueryAsStream(sql, maxConcurrency: 5, maxItemCount: 5);
            while (setIterator.HasMoreResults)
            {
                CosmosResponseMessage iter = await setIterator.FetchNextSetAsync();
                Assert.IsTrue(iter.IsSuccessStatusCode);
                Assert.IsNull(iter.ErrorMessage);
                totalRequstCharge += iter.Headers.RequestCharge;
                ToDoActivity[] response = this.jsonSerializer.FromStream<ToDoActivity[]>(iter.Content);
                Assert.IsTrue(response.Length <= 5);
                resultList.AddRange(response);
            }

            Assert.AreEqual(deleteList.Count, resultList.Count);
            Assert.IsTrue(totalRequstCharge > 0);

            List<ToDoActivity> verifiedOrderBy = deleteList.OrderBy(x => x.taskNum).ToList();
            resultList = resultList.OrderBy(x => x.taskNum).ToList();
            for (int i = 0; i < verifiedOrderBy.Count(); i++)
            {
                Assert.AreEqual(verifiedOrderBy[i].taskNum, resultList[i].taskNum);
                Assert.AreEqual(verifiedOrderBy[i].id, resultList[i].id);
            }
        }

        [TestMethod]
        public async Task EpkPointReadTest()
        {
            string pk = Guid.NewGuid().ToString();
            string epk = new PartitionKey(pk)
                            .InternalKey
                            .GetEffectivePartitionKeyString(this.containerSettings.PartitionKey);

            CosmosItemRequestOptions itemRequestOptions = new CosmosItemRequestOptions();
            itemRequestOptions.Properties = new Dictionary<string, object>();
            itemRequestOptions.Properties.Add(WFConstants.BackendHeaders.EffectivePartitionKeyString, epk);

            CosmosResponseMessage response = await this.Container.Items.ReadItemStreamAsync(
                null,
                Guid.NewGuid().ToString(),
                itemRequestOptions);

            // Ideally it should be NotFound
            // BadReqeust bcoz collection is regular and not binary 
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        /// <summary>
        /// Validate that if the EPK is set in the options that only a single range is selected.
        /// </summary>
        [TestMethod]
        public async Task ItemEpkQuerySingleKeyRangeValidation()
        {
            IList<ToDoActivity> deleteList = new List<ToDoActivity>();
            CosmosContainer container = null;
            try
            {
                // Create a container large enough to have at least 2 partitions
                CosmosContainerResponse containerResponse = await this.database.Containers.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk",
                    throughput: 15000);
                container = containerResponse;

                // Get all the partition key ranges to verify there is more than one partition
                IRoutingMapProvider routingMapProvider = await this.cosmosClient.DocumentClient.GetPartitionKeyRangeCacheAsync();
                IReadOnlyList<PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(
                    containerResponse.Resource.ResourceId,
                    new Documents.Routing.Range<string>("00", "FF", isMaxInclusive: true, isMinInclusive: true));

                // If this fails the RUs of the container needs to be increased to ensure at least 2 partitions.
                Assert.IsTrue(ranges.Count > 1, " RUs of the container needs to be increased to ensure at least 2 partitions.");

                FeedOptions options = new FeedOptions()
                {
                    Properties = new Dictionary<string, object>()
                    {
                        {"x-ms-effective-partition-key-string", "AA" }
                    }
                };

                // Create a bad expression. It will not be called. Expression is not allowed to be null.
                IQueryable<int> queryable = new List<int>().AsQueryable();
                Expression expression = queryable.Expression;

                DocumentQueryExecutionContextBase.InitParams inputParams = new DocumentQueryExecutionContextBase.InitParams(
                    new DocumentQueryClient(this.cosmosClient.DocumentClient),
                    ResourceType.Document,
                    typeof(object),
                    expression,
                    options,
                    ((CosmosContainerCore)container).LinkUri.OriginalString,
                    false,
                    Guid.NewGuid());

                DefaultDocumentQueryExecutionContext defaultDocumentQueryExecutionContext = new DefaultDocumentQueryExecutionContext(inputParams, true);

                // There should only be one range since the EPK option is set.
                List<PartitionKeyRange> partitionKeyRanges = await DocumentQueryExecutionContextFactory.GetTargetPartitionKeyRanges(
                    queryExecutionContext: defaultDocumentQueryExecutionContext,
                    partitionedQueryExecutionInfo: null,
                    collection: containerResponse,
                    feedOptions: options);

                Assert.IsTrue(partitionKeyRanges.Count == 1, "Only 1 partition key range should be selected since the EPK option is set.");
            }
            finally
            {
                if (container != null)
                {
                    await container.DeleteAsync();
                }
            }
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemQueryStreamSerializationSetting()
        {
            IList<ToDoActivity> deleteList = await this.CreateRandomItems(101, randomPartitionKey: true);

            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("SELECT * FROM toDoActivity t ORDER BY t.taskNum");
            CosmosSerializationOptions options = new CosmosSerializationOptions(
                ContentSerializationFormat.CosmosBinary.ToString(),
                (content) => JsonNavigator.Create(content),
                () => JsonWriter.Create(JsonSerializationFormat.Binary));

            CosmosQueryRequestOptions requestOptions = new CosmosQueryRequestOptions()
            {
                CosmosSerializationOptions = options
            };

            List<ToDoActivity> resultList = new List<ToDoActivity>();
            double totalRequstCharge = 0;
            CosmosFeedIterator setIterator =
                this.Container.Items.CreateItemQueryAsStream(sql, maxConcurrency: 5, maxItemCount: 5, requestOptions: requestOptions);
            while (setIterator.HasMoreResults)
            {
                CosmosResponseMessage iter = await setIterator.FetchNextSetAsync();
                Assert.IsTrue(iter.IsSuccessStatusCode);
                Assert.IsNull(iter.ErrorMessage);
                totalRequstCharge += iter.Headers.RequestCharge;
                IJsonReader reader = JsonReader.Create(iter.Content);
                IJsonWriter textWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                textWriter.WriteAll(reader);
                string json = Encoding.UTF8.GetString(textWriter.GetResult());
                Assert.IsNotNull(json);
                ToDoActivity[] responseActivities = JsonConvert.DeserializeObject<ToDoActivity[]>(json);
                Assert.IsTrue(responseActivities.Length <= 5);
                resultList.AddRange(responseActivities);
            }

            Assert.AreEqual(deleteList.Count, resultList.Count);
            Assert.IsTrue(totalRequstCharge > 0);

            List<ToDoActivity> verifiedOrderBy = deleteList.OrderBy(x => x.taskNum).ToList();
            for (int i = 0; i < verifiedOrderBy.Count(); i++)
            {
                Assert.AreEqual(verifiedOrderBy[i].taskNum, resultList[i].taskNum);
                Assert.AreEqual(verifiedOrderBy[i].id, resultList[i].id);
            }
        }

        /// <summary>
        /// Validate that the max item count works correctly.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task ValidateMaxItemCountOnItemQuery()
        {
            IList<ToDoActivity> deleteList = await this.CreateRandomItems(6, randomPartitionKey: false);

            ToDoActivity toDoActivity = deleteList.First();
            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition(
                "select * from toDoActivity t where t.status = @status")
                .UseParameter("@status", toDoActivity.status);

            // Test max size at 1
            CosmosFeedIterator<ToDoActivity> setIterator =
                this.Container.Items.CreateItemQuery<ToDoActivity>(sql, toDoActivity.status, maxItemCount: 1);
            while (setIterator.HasMoreResults)
            {
                CosmosFeedResponse<ToDoActivity> iter = await setIterator.FetchNextSetAsync();
                Assert.AreEqual(1, iter.Count());
            }

            // Test max size at 2
            CosmosFeedIterator<ToDoActivity> setIteratorMax2 =
                this.Container.Items.CreateItemQuery<ToDoActivity>(sql, toDoActivity.status, maxItemCount: 2);
            while (setIteratorMax2.HasMoreResults)
            {
                CosmosFeedResponse<ToDoActivity> iter = await setIteratorMax2.FetchNextSetAsync();
                Assert.AreEqual(2, iter.Count());
            }
        }

        /// <summary>
        /// Validate that the max item count works correctly.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task NegativeQueryTest()
        {
            IList<ToDoActivity> items = await this.CreateRandomItems(pkCount: 10, perPKItemCount: 20, randomPartitionKey: true);

            try
            {
                CosmosFeedIterator<dynamic> resultSet = this.Container.Items.CreateItemQuery<dynamic>(
                    sqlQueryText: "SELECT r.id FROM root r WHERE r._ts > 0",
                    maxConcurrency: 1,
                    maxItemCount: 10,
                    requestOptions: new CosmosQueryRequestOptions() { ResponseContinuationTokenLimitInKb = 0 });

                await resultSet.FetchNextSetAsync();
                Assert.Fail("Expected query to fail");
            }
            catch (Exception e)
            {
                CosmosException exception = e.InnerException as CosmosException;

                if (exception == null)
                {
                    throw e;
                }

                if (exception.StatusCode != HttpStatusCode.BadRequest)
                {
                    throw e;
                }

                Assert.IsTrue(exception.Message.Contains("continuation token limit specified is not large enough"));
            }

            try
            {
                CosmosFeedIterator<dynamic> resultSet = this.Container.Items.CreateItemQuery<dynamic>(
                    sqlQueryText: "SELECT r.id FROM root r WHERE r._ts >!= 0",
                    maxConcurrency: 1);

                await resultSet.FetchNextSetAsync();
                Assert.Fail("Expected query to fail");
            }
            catch (AggregateException e)
            {
                CosmosException exception = e.InnerException as CosmosException;

                if (exception == null)
                {
                    throw e;
                }

                if (exception.StatusCode != HttpStatusCode.BadRequest)
                {
                    throw e;
                }

                Assert.IsTrue(exception.Message.Contains("Syntax error, incorrect syntax near"));
            }
        }

        [TestMethod]
        public async Task ItemRequestOptionAccessConditionTest()
        {
            // Create an item
            ToDoActivity testItem = (await this.CreateRandomItems(1, randomPartitionKey: true)).First();

            // Create an access condition that will fail because the etag will be different
            AccessCondition accessCondition = new AccessCondition
            {
                // Random etag
                Condition = Guid.NewGuid().ToString(),
                Type = AccessConditionType.IfMatch
            };

            CosmosItemRequestOptions itemRequestOptions = new CosmosItemRequestOptions()
            {
                AccessCondition = accessCondition
            };

            try
            {
                CosmosItemResponse<ToDoActivity> response = await this.Container.Items.ReplaceItemAsync<ToDoActivity>(
                    partitionKey: testItem.status,
                    id: testItem.id,
                    item: testItem,
                    requestOptions: itemRequestOptions);
                Assert.Fail("Access condition should have failed");
            }
            catch (CosmosException e)
            {
                Assert.IsNotNull(e);
                Assert.AreEqual(HttpStatusCode.PreconditionFailed, e.StatusCode, e.Message);
                Assert.IsNotNull(e.ActivityId);
                Assert.IsTrue(e.RequestCharge > 0);
            }
            finally
            {
                CosmosItemResponse<ToDoActivity> deleteResponse = await this.Container.Items.DeleteItemAsync<ToDoActivity>(partitionKey: testItem.status, id: testItem.id);
                Assert.IsNotNull(deleteResponse);
            }
        }

        // Read write non partition Container item.
        [TestMethod]
        [Ignore] //Temporary ignore till we fix emulator issue
        public async Task ReadNonPartitionItemAsync()
        {
            try
            {
                await this.CreateNonPartitionedContainer();
                await this.CreateItemInNonPartitionedContainer(nonPartitionItemId);
                await this.CreateUndefinedPartitionItem();
                fixedContainer = this.database.Containers[nonPartitionContainerId];

                CosmosContainerResponse containerResponse = await fixedContainer.ReadAsync();
                Assert.IsTrue(containerResponse.Resource.PartitionKey.Paths.Count > 0);
                Assert.AreEqual(PartitionKey.SystemKeyPath, containerResponse.Resource.PartitionKey.Paths[0]);

                //Reading item from fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                CosmosItemResponse<ToDoActivity> response = await fixedContainer.Items.ReadItemAsync<ToDoActivity>(
                    partitionKey: CosmosContainerSettings.NonePartitionKeyValue,
                    id: nonPartitionItemId);

                Assert.IsNotNull(response.Resource);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(nonPartitionItemId, response.Resource.id);

                //Adding item to fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                ToDoActivity itemWithoutPK = CreateRandomToDoActivity();
                CosmosItemResponse<ToDoActivity> createResponseWithoutPk = await fixedContainer.Items.CreateItemAsync<ToDoActivity>(
                 partitionKey: CosmosContainerSettings.NonePartitionKeyValue,
                 item: itemWithoutPK);

                Assert.IsNotNull(createResponseWithoutPk.Resource);
                Assert.AreEqual(HttpStatusCode.Created, createResponseWithoutPk.StatusCode);
                Assert.AreEqual(itemWithoutPK.id, createResponseWithoutPk.Resource.id);

                //Updating item on fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                itemWithoutPK.status = "updatedStatus";
                CosmosItemResponse<ToDoActivity> updateResponseWithoutPk = await fixedContainer.Items.ReplaceItemAsync<ToDoActivity>(
                 partitionKey: CosmosContainerSettings.NonePartitionKeyValue,
                 id: itemWithoutPK.id,
                 item: itemWithoutPK);

                Assert.IsNotNull(updateResponseWithoutPk.Resource);
                Assert.AreEqual(HttpStatusCode.OK, updateResponseWithoutPk.StatusCode);
                Assert.AreEqual(itemWithoutPK.id, updateResponseWithoutPk.Resource.id);

                //Adding item to fixed container with non-none PK.
                ToDoActivityAfterMigration itemWithPK = CreateRandomToDoActivityAfterMigration("TestPk");
                CosmosItemResponse<ToDoActivityAfterMigration> createResponseWithPk = await fixedContainer.Items.CreateItemAsync<ToDoActivityAfterMigration>(
                 partitionKey: itemWithPK.status,
                 item: itemWithPK);

                Assert.IsNotNull(createResponseWithPk.Resource);
                Assert.AreEqual(HttpStatusCode.Created, createResponseWithPk.StatusCode);
                Assert.AreEqual(itemWithPK.id, createResponseWithPk.Resource.id);

                //Quering items on fixed container with cross partition enabled.
                CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("select * from r");
                CosmosFeedIterator<dynamic> setIterator = fixedContainer.Items
                    .CreateItemQuery<dynamic>(sql, maxConcurrency: 1,maxItemCount:10, requestOptions: new CosmosQueryRequestOptions { EnableCrossPartitionQuery = true});
                while (setIterator.HasMoreResults)
                {
                    CosmosFeedResponse<dynamic> queryResponse = await setIterator.FetchNextSetAsync();
                    Assert.AreEqual(3, queryResponse.Count());
                }

                //Reading all items on fixed container.
                setIterator = fixedContainer.Items
                    .GetItemIterator<dynamic>(maxItemCount: 10);
                while (setIterator.HasMoreResults)
                {
                    CosmosFeedResponse<dynamic> queryResponse = await setIterator.FetchNextSetAsync();
                    Assert.AreEqual(3, queryResponse.Count());
                }

                //Quering items on fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                setIterator = fixedContainer.Items
                    .CreateItemQuery<dynamic>(sql, partitionKey: CosmosContainerSettings.NonePartitionKeyValue, maxItemCount: 10);
                while (setIterator.HasMoreResults)
                {
                    CosmosFeedResponse<dynamic> queryResponse = await setIterator.FetchNextSetAsync();
                    Assert.AreEqual(2, queryResponse.Count());
                }

                //Quering items on fixed container with non-none PK.
                setIterator = fixedContainer.Items
                    .CreateItemQuery<dynamic>(sql, partitionKey: itemWithPK.status, maxItemCount: 10);
                while (setIterator.HasMoreResults)
                {
                    CosmosFeedResponse<dynamic> queryResponse = await setIterator.FetchNextSetAsync();
                    Assert.AreEqual(1, queryResponse.Count());
                }

                //Deleting item from fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                CosmosItemResponse<ToDoActivity> deleteResponseWithoutPk = await fixedContainer.Items.DeleteItemAsync<ToDoActivity>(
                 partitionKey: CosmosContainerSettings.NonePartitionKeyValue,
                 id: itemWithoutPK.id);

                Assert.IsNull(deleteResponseWithoutPk.Resource);
                Assert.AreEqual(HttpStatusCode.NoContent, deleteResponseWithoutPk.StatusCode);

                //Deleting item from fixed container with non-none PK.
                CosmosItemResponse<ToDoActivityAfterMigration> deleteResponseWithPk = await fixedContainer.Items.DeleteItemAsync<ToDoActivityAfterMigration>(
                 partitionKey: itemWithPK.status,
                 id: itemWithPK.id);

                Assert.IsNull(deleteResponseWithPk.Resource);
                Assert.AreEqual(HttpStatusCode.NoContent, deleteResponseWithPk.StatusCode);

                //Reading item from partitioned container with CosmosContainerSettings.NonePartitionKeyValue.
                CosmosItemResponse<ToDoActivity> undefinedItemResponse = await Container.Items.ReadItemAsync<ToDoActivity>(
                    partitionKey: CosmosContainerSettings.NonePartitionKeyValue,
                    id: undefinedPartitionItemId);

                Assert.IsNotNull(undefinedItemResponse.Resource);
                Assert.AreEqual(HttpStatusCode.OK, undefinedItemResponse.StatusCode);
                Assert.AreEqual(undefinedPartitionItemId, undefinedItemResponse.Resource.id);
            }
            finally
            {
                if (fixedContainer != null)
                {
                    await fixedContainer.DeleteAsync();
                }
            }
        }

        private async Task<IList<ToDoActivity>> CreateRandomItems(int pkCount, int perPKItemCount = 1, bool randomPartitionKey = true)
        {
            Assert.IsFalse(!randomPartitionKey && perPKItemCount > 1);

            List<ToDoActivity> createdList = new List<ToDoActivity>();
            for (int i = 0; i < pkCount; i++)
            {
                string pk = "TBD";
                if (randomPartitionKey)
                {
                    pk += Guid.NewGuid().ToString();
                }

                for (int j = 0; j < perPKItemCount; j++)
                {
                    ToDoActivity temp = this.CreateRandomToDoActivity(pk);

                    createdList.Add(temp);

                    await this.Container.Items.CreateItemAsync<ToDoActivity>(partitionKey: temp.status, item: temp);
                }
            }

            return createdList;
        }

        private async Task CreateNonPartitionedContainer()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];
            //Creating non partition Container, rest api used instead of .NET SDK api as it is not supported anymore.
            HttpClient client = new System.Net.Http.HttpClient();
            Uri baseUri = new Uri(endpoint);
            string verb = "POST";
            string resourceType = "colls";
            string resourceId = string.Format("dbs/{0}", this.database.Id);
            string resourceLink = string.Format("dbs/{0}/colls", this.database.Id);
            client.DefaultRequestHeaders.Add("x-ms-date", utc_date);
            client.DefaultRequestHeaders.Add("x-ms-version", CosmosItemTests.PreNonPartitionedMigrationApiVersion);

            string authHeader = this.GenerateMasterKeyAuthorizationSignature(verb, resourceId, resourceType, authKey, "master", "1.0");

            client.DefaultRequestHeaders.Add("authorization", authHeader);
            string containerDefinition = "{\n  \"id\": \"" + nonPartitionContainerId + "\"\n}";
            StringContent containerContent = new StringContent(containerDefinition);
            Uri requestUri = new Uri(baseUri, resourceLink);
            await client.PostAsync(requestUri.ToString(), containerContent);
        }

        private async Task CreateItemInNonPartitionedContainer(string itemId)
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];
            //Creating non partition Container item.
            HttpClient client = new System.Net.Http.HttpClient();
            Uri baseUri = new Uri(endpoint);
            string verb = "POST";
            string resourceType = "docs";
            string resourceId = string.Format("dbs/{0}/colls/{1}", this.database.Id, nonPartitionContainerId);
            string resourceLink = string.Format("dbs/{0}/colls/{1}/docs", this.database.Id, nonPartitionContainerId);
            string authHeader = this.GenerateMasterKeyAuthorizationSignature(verb, resourceId, resourceType, authKey, "master", "1.0");

            client.DefaultRequestHeaders.Add("x-ms-date", utc_date);
            client.DefaultRequestHeaders.Add("x-ms-version", CosmosItemTests.PreNonPartitionedMigrationApiVersion);
            client.DefaultRequestHeaders.Add("authorization", authHeader);

            string itemDefinition = JsonConvert.SerializeObject(this.CreateRandomToDoActivity(id: itemId));
            StringContent itemContent = new StringContent(itemDefinition);
            Uri requestUri = new Uri(baseUri, resourceLink);
            await client.PostAsync(requestUri.ToString(), itemContent);
        }

        private async Task CreateUndefinedPartitionItem()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];
            //Creating undefined partition key  item, rest api used instead of .NET SDK api as it is not supported anymore.
            HttpClient client = new System.Net.Http.HttpClient();
            Uri baseUri = new Uri(endpoint);
            string verb = "POST";
            string resourceType = "colls";
            string resourceId = string.Format("dbs/{0}", this.database.Id);
            string resourceLink = string.Format("dbs/{0}/colls", this.database.Id);
            client.DefaultRequestHeaders.Add("x-ms-date", utc_date);
            client.DefaultRequestHeaders.Add("x-ms-version", CosmosItemTests.PreNonPartitionedMigrationApiVersion);
            client.DefaultRequestHeaders.Add("x-ms-documentdb-partitionkey", "[{}]");

            //Creating undefined partition Container item.
            verb = "POST";
            resourceType = "docs";
            resourceId = string.Format("dbs/{0}/colls/{1}", this.database.Id, this.Container.Id);
            resourceLink = string.Format("dbs/{0}/colls/{1}/docs", this.database.Id, this.Container.Id);
            string authHeader = this.GenerateMasterKeyAuthorizationSignature(verb, resourceId, resourceType, authKey, "master", "1.0");

            client.DefaultRequestHeaders.Remove("authorization");
            client.DefaultRequestHeaders.Add("authorization", authHeader);

            var payload = new { id = undefinedPartitionItemId, user = undefinedPartitionItemId };
            string itemDefinition = JsonConvert.SerializeObject(payload);
            StringContent itemContent = new StringContent(itemDefinition);
            Uri requestUri = new Uri(baseUri, resourceLink);
            await client.PostAsync(requestUri.ToString(), itemContent);
        }

        private string GenerateMasterKeyAuthorizationSignature(string verb, string resourceId, string resourceType, string key, string keyType, string tokenVersion)
        {
            System.Security.Cryptography.HMACSHA256 hmacSha256 = new System.Security.Cryptography.HMACSHA256 { Key = Convert.FromBase64String(key) };

            string payLoad = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}\n{1}\n{2}\n{3}\n{4}\n",
                    verb.ToLowerInvariant(),
                    resourceType.ToLowerInvariant(),
                    resourceId,
                    utc_date.ToLowerInvariant(),
                    ""
            );

            byte[] hashPayLoad = hmacSha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payLoad));
            string signature = Convert.ToBase64String(hashPayLoad);

            return System.Web.HttpUtility.UrlEncode(string.Format(System.Globalization.CultureInfo.InvariantCulture, "type={0}&ver={1}&sig={2}",
                keyType,
                tokenVersion,
                signature));
        }

        private ToDoActivity CreateRandomToDoActivity(string pk = null, string id = null)
        {
            if (string.IsNullOrEmpty(pk))
            {
                pk = "TBD" + Guid.NewGuid().ToString();
            }
            if (id == null)
            {
                id = Guid.NewGuid().ToString();
            }
            return new ToDoActivity()
            {
                id = id,
                description = "CreateRandomToDoActivity",
                status = pk,
                taskNum = 42,
                cost = double.MaxValue
            };
        }



        public class ToDoActivity
        {
            public string id { get; set; }
            public int taskNum { get; set; }
            public double cost { get; set; }
            public string description { get; set; }
            public string status { get; set; }
        }

        public class ToDoActivityAfterMigration
        {
            public string id { get; set; }
            public int taskNum { get; set; }
            public double cost { get; set; }
            public string description { get; set; }
            [JsonProperty(PropertyName = "_partitionKey")]
            public string status { get; set; }
        }

        private ToDoActivityAfterMigration CreateRandomToDoActivityAfterMigration(string pk = null, string id = null)
        {
            if (string.IsNullOrEmpty(pk))
            {
                pk = "TBD" + Guid.NewGuid().ToString();
            }
            if (id == null)
            {
                id = Guid.NewGuid().ToString();
            }
            return new ToDoActivityAfterMigration()
            {
                id = id,
                description = "CreateRandomToDoActivity",
                status = pk,
                taskNum = 42,
                cost = double.MaxValue
            };
        }
    }
}
