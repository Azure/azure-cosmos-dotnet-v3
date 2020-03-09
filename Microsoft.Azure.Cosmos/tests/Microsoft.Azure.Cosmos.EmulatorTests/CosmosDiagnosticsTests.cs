//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class CosmosDiagnosticsTests : BaseCosmosClientHelper
    {
        private Container Container = null;
        private ContainerProperties containerSettings = null;
        private static RequestOptions DisableDiagnosticOptions = new RequestOptions()
        {
            DiagnosticContext = EmptyCosmosDiagnosticsContext.Singleton
        };

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
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
        public async Task CustomHandlersDiagnostic()
        {
            TimeSpan delayTime = TimeSpan.FromSeconds(2);
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(builder =>
                builder.AddCustomHandlers(new RequestHandlerSleepHelper(delayTime)));

            DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            string diagnostics = databaseResponse.Diagnostics.ToString();
            Assert.IsNotNull(diagnostics);
            JObject jObject = JObject.Parse(diagnostics);
            JArray contextList = jObject["Context"].ToObject<JArray>();
            JObject customHandler = GetJObjectInContextList(contextList, typeof(RequestHandlerSleepHelper).FullName);
            Assert.IsNotNull(customHandler);
            TimeSpan elapsedTime = customHandler["ElapsedTime"].ToObject<TimeSpan>();
            Assert.IsTrue(elapsedTime.TotalSeconds > 1);

            customHandler = GetJObjectInContextList(contextList, typeof(RequestHandlerSleepHelper).FullName);
            Assert.IsNotNull(customHandler);
            elapsedTime = customHandler["ElapsedTime"].ToObject<TimeSpan>();
            Assert.IsTrue(elapsedTime > delayTime);

            await databaseResponse.Database.DeleteAsync();
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task PointOperationRequestTimeoutDiagnostic(bool disableDiagnostics)
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                DiagnosticContext = disableDiagnostics ? EmptyCosmosDiagnosticsContext.Singleton : null
            };

            Guid exceptionActivityId = Guid.NewGuid();
            string transportExceptionDescription = "transportExceptionDescription" + Guid.NewGuid();
            Container containerWithTransportException = TransportClientHelper.GetContainerWithItemTransportException(
                this.database.Id,
                this.Container.Id,
                exceptionActivityId,
                transportExceptionDescription);

            //Checking point operation diagnostics on typed operations
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            try
            {
                ItemResponse<ToDoActivity> createResponse = await containerWithTransportException.CreateItemAsync<ToDoActivity>(
                  item: testItem,
                  requestOptions: requestOptions);
                Assert.Fail("Should have thrown a request timeout exception");
            }
            catch(CosmosException ce) when (ce.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            {
                string exception = ce.ToString();
                Assert.IsNotNull(exception);
                Assert.IsTrue(exception.Contains(exceptionActivityId.ToString()));
                Assert.IsTrue(exception.Contains(transportExceptionDescription));

                string diagnosics = ce.Diagnostics.ToString();
                if (disableDiagnostics)
                {
                    Assert.IsTrue(string.IsNullOrEmpty(diagnosics));
                }
                else
                {
                    Assert.IsFalse(string.IsNullOrEmpty(diagnosics));
                    Assert.IsTrue(exception.Contains(diagnosics));
                }
            }
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task PointOperationDiagnostic(bool disableDiagnostics)
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                DiagnosticContext = disableDiagnostics ? EmptyCosmosDiagnosticsContext.Singleton : null
            };

            //Checking point operation diagnostics on typed operations
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> createResponse = await this.Container.CreateItemAsync<ToDoActivity>(
                item: testItem,
                requestOptions: requestOptions);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(createResponse.Diagnostics, disableDiagnostics);

            ItemResponse<ToDoActivity> readResponse = await this.Container.ReadItemAsync<ToDoActivity>(
                id: testItem.id,
                partitionKey: new PartitionKey(testItem.status),
                requestOptions);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(readResponse.Diagnostics, disableDiagnostics);
            Assert.IsNotNull(readResponse.Diagnostics);

            testItem.description = "NewDescription";
            ItemResponse<ToDoActivity> replaceResponse = await this.Container.ReplaceItemAsync<ToDoActivity>(
                item: testItem,
                id: testItem.id,
                partitionKey: new PartitionKey(testItem.status),
                requestOptions: requestOptions);

            Assert.AreEqual(replaceResponse.Resource.description, "NewDescription");
            CosmosDiagnosticsTests.VerifyPointDiagnostics(replaceResponse.Diagnostics, disableDiagnostics);

            ItemResponse<ToDoActivity> deleteResponse = await this.Container.DeleteItemAsync<ToDoActivity>(
                partitionKey: new Cosmos.PartitionKey(testItem.status),
                id: testItem.id,
                requestOptions: requestOptions);

            Assert.IsNotNull(deleteResponse);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(deleteResponse.Diagnostics, disableDiagnostics);

            //Checking point operation diagnostics on stream operations
            ResponseMessage createStreamResponse = await this.Container.CreateItemStreamAsync(
                partitionKey: new PartitionKey(testItem.status),
                streamPayload: TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem),
                requestOptions: requestOptions);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(createStreamResponse.Diagnostics, disableDiagnostics);

            ResponseMessage readStreamResponse = await this.Container.ReadItemStreamAsync(
                id: testItem.id,
                partitionKey: new PartitionKey(testItem.status),
                requestOptions: requestOptions);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(readStreamResponse.Diagnostics, disableDiagnostics);

            ResponseMessage replaceStreamResponse = await this.Container.ReplaceItemStreamAsync(
               streamPayload: TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem),
               id: testItem.id,
               partitionKey: new PartitionKey(testItem.status),
               requestOptions: requestOptions);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(replaceStreamResponse.Diagnostics, disableDiagnostics);

            ResponseMessage deleteStreamResponse = await this.Container.DeleteItemStreamAsync(
               id: testItem.id,
               partitionKey: new PartitionKey(testItem.status),
               requestOptions: requestOptions);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(deleteStreamResponse.Diagnostics, disableDiagnostics);

            // Ensure diagnostics are set even on failed operations
            testItem.description = new string('x', Microsoft.Azure.Documents.Constants.MaxResourceSizeInBytes + 1);
            ResponseMessage createTooBigStreamResponse = await this.Container.CreateItemStreamAsync(
                partitionKey: new PartitionKey(testItem.status),
                streamPayload: TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem),
                requestOptions: requestOptions);
            Assert.IsFalse(createTooBigStreamResponse.IsSuccessStatusCode);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(createTooBigStreamResponse.Diagnostics, disableDiagnostics);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task BatchOperationDiagnostic(bool disableDiagnostics)
        {
            string pkValue = "DiagnosticTestPk";
            TransactionalBatch batch = this.Container.CreateTransactionalBatch(new PartitionKey(pkValue));

            List<ToDoActivity> createItems = new List<ToDoActivity>();
            for(int i = 0; i < 50; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pkValue);
                createItems.Add(item);
                batch.CreateItem<ToDoActivity>(item);
            }

            for (int i = 0; i < 20; i++)
            {
                batch.ReadItem(createItems[i].id);
            }

            RequestOptions requestOptions = disableDiagnostics ? DisableDiagnosticOptions : null;
            TransactionalBatchResponse response = await ((BatchCore)batch).ExecuteAsync(requestOptions);
            
            Assert.IsNotNull(response);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(response.Diagnostics, disableDiagnostics);
        }

        [TestMethod]
        public async Task BulkOperationDiagnostic()
        {
            string pkValue = "DiagnosticBulkTestPk";
            CosmosClient bulkClient = TestCommon.CreateCosmosClient(builder => builder.WithBulkExecution(true));
            Container bulkContainer = bulkClient.GetContainer(this.database.Id, this.Container.Id);
            List<Task<ItemResponse<ToDoActivity>>> createItemsTasks = new List<Task<ItemResponse<ToDoActivity>>>();
            for (int i = 0; i < 100; i++)
            {

                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pkValue);
                createItemsTasks.Add(bulkContainer.CreateItemAsync<ToDoActivity>(item, new PartitionKey(item.status)));
            }

            await Task.WhenAll(createItemsTasks);

            foreach (Task<ItemResponse<ToDoActivity>> createTask in createItemsTasks)
            {
                ItemResponse<ToDoActivity> itemResponse = await createTask;
                Assert.IsNotNull(itemResponse);
                CosmosDiagnosticsTests.VerifyBulkPointDiagnostics(itemResponse.Diagnostics);
            }
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task QueryOperationDiagnostic(bool disableDiagnostics)
        {
            int totalItems = 3;
            IList<ToDoActivity> itemList = await ToDoActivity.CreateRandomItems(
                this.Container,
                pkCount: totalItems,
                perPKItemCount: 1,
                randomPartitionKey: true);

            //Checking query metrics on typed query
            long totalOutputDocumentCount = await this.ExecuteQueryAndReturnOutputDocumentCount(
                queryText: "select * from ToDoActivity",
                expectedItemCount: totalItems,
                disableDiagnostics: disableDiagnostics);

            Assert.AreEqual(totalItems, totalOutputDocumentCount);

            totalOutputDocumentCount = await this.ExecuteQueryAndReturnOutputDocumentCount(
                queryText: "select * from ToDoActivity t ORDER BY t.cost",
                expectedItemCount: totalItems,
                disableDiagnostics: disableDiagnostics);

            Assert.AreEqual(totalItems, totalOutputDocumentCount);

            totalOutputDocumentCount = await this.ExecuteQueryAndReturnOutputDocumentCount(
                queryText: "select DISTINCT t.cost from ToDoActivity t",
                expectedItemCount: 1,
                disableDiagnostics: disableDiagnostics);

            Assert.IsTrue(totalOutputDocumentCount >= 1);

            totalOutputDocumentCount = await this.ExecuteQueryAndReturnOutputDocumentCount(
                queryText: "select * from ToDoActivity OFFSET 1 LIMIT 1",
                expectedItemCount: 1,
                disableDiagnostics: disableDiagnostics);

            Assert.IsTrue(totalOutputDocumentCount >= 1);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task NonDataPlaneDiagnosticTest(bool disableDiagnostics)
        {
            RequestOptions requestOptions = new RequestOptions()
            {
                DiagnosticContext = disableDiagnostics ? EmptyCosmosDiagnosticsContext.Singleton : null
            };

            DatabaseResponse databaseResponse = await this.cosmosClient.CreateDatabaseAsync(
                id: Guid.NewGuid().ToString(),
                requestOptions: requestOptions);
            Assert.IsNotNull(databaseResponse.Diagnostics);
            string diagnostics = databaseResponse.Diagnostics.ToString();
            if (disableDiagnostics)
            {
                Assert.AreEqual(string.Empty, diagnostics);
                return;
            }

            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
            Assert.IsTrue(diagnostics.Contains("SubStatusCode"));
            Assert.IsTrue(diagnostics.Contains("RequestUri"));
        }

        public static void VerifyQueryDiagnostics(
            CosmosDiagnostics diagnostics,
            bool isFirstPage,
            bool disableDiagnostics)
        {
            string info = diagnostics.ToString();
            if (disableDiagnostics)
            {
                Assert.AreEqual(string.Empty, info);
                return;
            }

            Assert.IsNotNull(info);
            JObject jObject = JObject.Parse(info);
            JToken summary = jObject["Summary"];
            Assert.IsNotNull(summary["UserAgent"].ToString());
            Assert.IsNotNull(summary["StartUtc"].ToString());

            JArray contextList = jObject["Context"].ToObject<JArray>();
            Assert.IsTrue(contextList.Count > 0);

            // Find the PointOperationStatistics object
            JObject page = GetJObjectInContextList(
                contextList,
                "0",
                "PKRangeId");

            // First page will have a request
            // Query might use cache pages which don't have the following info. It was returned in the previous call.
            if(isFirstPage || page != null)
            {
                string queryMetrics = page["QueryMetric"].ToString();
                Assert.IsNotNull(queryMetrics);
                Assert.IsNotNull(page["IndexUtilization"].ToString());
                Assert.IsNotNull(page["PKRangeId"].ToString());
                JArray requestDiagnostics = page["Context"].ToObject<JArray>();
                Assert.IsNotNull(requestDiagnostics);
            }
        }

        public static void VerifyBulkPointDiagnostics(CosmosDiagnostics diagnostics)
        {
            string info = diagnostics.ToString();
            Assert.IsNotNull(info);
            JObject jObject = JObject.Parse(info);

            JToken summary = jObject["Summary"];
            Assert.IsNotNull(summary["UserAgent"].ToString());
            Assert.IsNotNull(summary["StartUtc"].ToString());

            Assert.IsNotNull(jObject["Context"].ToString());
            JArray contextList = jObject["Context"].ToObject<JArray>();
            Assert.IsTrue(contextList.Count > 2);

            // Find the PointOperationStatistics object
            JObject pointStatistics = GetJObjectInContextList(
                contextList,
                "PointOperationStatistics");

            ValidatePointOperation(pointStatistics);
        }

        public static void VerifyPointDiagnostics(CosmosDiagnostics diagnostics, bool disableDiagnostics)
        {
            string info = diagnostics.ToString();

            if (disableDiagnostics)
            {
                Assert.AreEqual(string.Empty, info);
                return;
            }

            Assert.IsNotNull(info);
            JObject jObject = JObject.Parse(info);
            JToken summary = jObject["Summary"];
            Assert.IsNotNull(summary["UserAgent"].ToString());
            Assert.IsNotNull(summary["StartUtc"].ToString());
            Assert.IsNotNull(summary["ElapsedTime"].ToString());

            Assert.IsNotNull(jObject["Context"].ToString());
            JArray contextList = jObject["Context"].ToObject<JArray>();
            Assert.IsTrue(contextList.Count > 3);

            // Find the PointOperationStatistics object
            JObject pointStatistics = GetJObjectInContextList(
                contextList,
                "PointOperationStatistics");

            ValidatePointOperation(pointStatistics);
        }

        private static void ValidatePointOperation(JObject pointStatistics)
        {
            Assert.IsNotNull(pointStatistics, $"Context list does not contain PointOperationStatistics.");
            int statusCode = pointStatistics["StatusCode"].ToObject<int>();
            Assert.IsNotNull(pointStatistics["ActivityId"].ToString());
            Assert.IsNotNull(pointStatistics["StatusCode"].ToString());
            Assert.IsNotNull(pointStatistics["RequestCharge"].ToString());
            Assert.IsNotNull(pointStatistics["RequestUri"].ToString());
            Assert.IsNotNull(pointStatistics["ClientRequestStats"].ToString());
            JObject clientJObject = pointStatistics["ClientRequestStats"].ToObject<JObject>();
            Assert.IsNotNull(clientJObject["RequestStartTimeUtc"].ToString());
            Assert.IsNotNull(clientJObject["ContactedReplicas"].ToString());
            Assert.IsNotNull(clientJObject["RequestLatency"].ToString());

            // Not all request have these fields. If the field exists then it should not be null
            if (clientJObject["EndpointToAddressResolutionStatistics"] != null)
            {
                Assert.IsNotNull(clientJObject["EndpointToAddressResolutionStatistics"].ToString());
            }

            if (clientJObject["SupplementalResponseStatisticsListLast10"] != null)
            {
                Assert.IsNotNull(clientJObject["SupplementalResponseStatisticsListLast10"].ToString());
            }

            if (clientJObject["FailedReplicas"] != null)
            {
                Assert.IsNotNull(clientJObject["FailedReplicas"].ToString());
            }

            // Session token only expected on success
            if (statusCode >= 200 && statusCode < 300)
            {
                Assert.IsNotNull(clientJObject["ResponseStatisticsList"].ToString());
                Assert.IsNotNull(clientJObject["RegionsContacted"].ToString());
                Assert.IsNotNull(clientJObject["RequestEndTimeUtc"].ToString());
                Assert.IsNotNull(pointStatistics["ResponseSessionToken"].ToString());
            }
        }

        private static JObject GetJObjectInContextList(JArray contextList, string value, string key = "Id")
        {
            foreach (JObject tempJObject in contextList)
            {
                JToken jsonId = tempJObject[key];
                string name = jsonId?.Value<string>();
                if (string.Equals(value, name))
                {
                    return tempJObject;
                }
            }

            return null;
        }

        private static JObject GetPropertyInContextList(JArray contextList, string id)
        {
            JObject jObject = GetJObjectInContextList(contextList, id);
            if (jObject == null)
            {
                return null;
            }

            return jObject["Value"].ToObject<JObject>();
        }

        private async Task<long> ExecuteQueryAndReturnOutputDocumentCount(string queryText, int expectedItemCount, bool disableDiagnostics)
        {
            QueryDefinition sql = new QueryDefinition(queryText);

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxItemCount = 1,
                MaxConcurrency = 1,
                DiagnosticContext = disableDiagnostics ? EmptyCosmosDiagnosticsContext.Singleton : null
            };

            // Verify the typed query iterator
            FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
                    sql,
                    requestOptions: requestOptions);

            List<ToDoActivity> results = new List<ToDoActivity>();
            long totalOutDocumentCount = 0;
            bool isFirst = true;
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> response = await feedIterator.ReadNextAsync();
                results.AddRange(response);
                VerifyQueryDiagnostics(response.Diagnostics, isFirst, disableDiagnostics);
                isFirst = false;
            }

            Assert.AreEqual(expectedItemCount, results.Count);

            // Verify the stream query iterator
            FeedIterator streamIterator = this.Container.GetItemQueryStreamIterator(
                   sql,
                   requestOptions: requestOptions);

            List<ToDoActivity> streamResults = new List<ToDoActivity>();
            long streamTotalOutDocumentCount = 0;
            isFirst = true;
            while (streamIterator.HasMoreResults)
            {
                ResponseMessage response = await streamIterator.ReadNextAsync();
                Collection<ToDoActivity> result = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(response.Content).Data;
                streamResults.AddRange(result);
                VerifyQueryDiagnostics(response.Diagnostics, isFirst, disableDiagnostics);
                isFirst = false;
            }

            Assert.AreEqual(expectedItemCount, streamResults.Count);
            Assert.AreEqual(totalOutDocumentCount, streamTotalOutDocumentCount);

            return results.Count;
        }

        private class RequestHandlerSleepHelper : RequestHandler
        {
            TimeSpan timeToSleep;

            public RequestHandlerSleepHelper(TimeSpan timeToSleep)
            {
                this.timeToSleep = timeToSleep;
            }

            public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(this.timeToSleep);
                return await base.SendAsync(request, cancellationToken);
            }
        }
    }
}
