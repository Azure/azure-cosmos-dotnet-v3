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
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class CosmosDiagnosticsTests : BaseCosmosClientHelper
    {
        private Container Container = null;
        private ContainerProperties containerSettings = null;

        private static readonly ItemRequestOptions RequestOptionDisableDiagnostic = new ItemRequestOptions()
        {
            DiagnosticContextFactory = () => EmptyCosmosDiagnosticsContext.Singleton
        };

        private static readonly ChangeFeedRequestOptions ChangeFeedRequestOptionDisableDiagnostic = new ChangeFeedRequestOptions()
        {
            DiagnosticContextFactory = () => EmptyCosmosDiagnosticsContext.Singleton
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
            ItemRequestOptions requestOptions = new ItemRequestOptions();
            if (disableDiagnostics)
            {
                requestOptions.DiagnosticContextFactory = () => EmptyCosmosDiagnosticsContext.Singleton;
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
            ItemRequestOptions requestOptions = new ItemRequestOptions();
            if (disableDiagnostics)
            {
                requestOptions.DiagnosticContextFactory = () => EmptyCosmosDiagnosticsContext.Singleton;
            };

            //Checking point operation diagnostics on typed operations
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> createResponse = await this.Container.CreateItemAsync<ToDoActivity>(
                item: testItem,
                requestOptions: requestOptions);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(
                createResponse.Diagnostics,
                disableDiagnostics);

            ItemResponse<ToDoActivity> readResponse = await this.Container.ReadItemAsync<ToDoActivity>(
                id: testItem.id,
                partitionKey: new PartitionKey(testItem.status),
                requestOptions);

            CosmosDiagnosticsTests.VerifyPointDiagnostics(
                readResponse.Diagnostics,
                disableDiagnostics);

            testItem.description = "NewDescription";
            ItemResponse<ToDoActivity> replaceResponse = await this.Container.ReplaceItemAsync<ToDoActivity>(
                item: testItem,
                id: testItem.id,
                partitionKey: new PartitionKey(testItem.status),
                requestOptions: requestOptions);

            Assert.AreEqual(replaceResponse.Resource.description, "NewDescription");

            CosmosDiagnosticsTests.VerifyPointDiagnostics(
                replaceResponse.Diagnostics,
                disableDiagnostics);

            ItemResponse<ToDoActivity> deleteResponse = await this.Container.DeleteItemAsync<ToDoActivity>(
                partitionKey: new Cosmos.PartitionKey(testItem.status),
                id: testItem.id,
                requestOptions: requestOptions);

            Assert.IsNotNull(deleteResponse);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(
                deleteResponse.Diagnostics,
                disableDiagnostics);

            //Checking point operation diagnostics on stream operations
            ResponseMessage createStreamResponse = await this.Container.CreateItemStreamAsync(
                partitionKey: new PartitionKey(testItem.status),
                streamPayload: TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem),
                requestOptions: requestOptions);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(
                createStreamResponse.Diagnostics,
                disableDiagnostics);

            ResponseMessage readStreamResponse = await this.Container.ReadItemStreamAsync(
                id: testItem.id,
                partitionKey: new PartitionKey(testItem.status),
                requestOptions: requestOptions);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(
                readStreamResponse.Diagnostics,
                disableDiagnostics);

            ResponseMessage replaceStreamResponse = await this.Container.ReplaceItemStreamAsync(
               streamPayload: TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem),
               id: testItem.id,
               partitionKey: new PartitionKey(testItem.status),
               requestOptions: requestOptions);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(
                replaceStreamResponse.Diagnostics,
                disableDiagnostics);

            ResponseMessage deleteStreamResponse = await this.Container.DeleteItemStreamAsync(
               id: testItem.id,
               partitionKey: new PartitionKey(testItem.status),
               requestOptions: requestOptions);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(
                deleteStreamResponse.Diagnostics,
                disableDiagnostics);

            // Ensure diagnostics are set even on failed operations
            testItem.description = new string('x', Microsoft.Azure.Documents.Constants.MaxResourceSizeInBytes + 1);
            ResponseMessage createTooBigStreamResponse = await this.Container.CreateItemStreamAsync(
                partitionKey: new PartitionKey(testItem.status),
                streamPayload: TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem),
                requestOptions: requestOptions);
            Assert.IsFalse(createTooBigStreamResponse.IsSuccessStatusCode);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(
                createTooBigStreamResponse.Diagnostics,
                disableDiagnostics);
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

            RequestOptions requestOptions = disableDiagnostics ? RequestOptionDisableDiagnostic : null;
            TransactionalBatchResponse response = await ((BatchCore)batch).ExecuteAsync(requestOptions);
            
            Assert.IsNotNull(response);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(
                diagnostics: response.Diagnostics,
                disableDiagnostics: disableDiagnostics);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task ChangeFeedDiagnostics(bool disableDiagnostics)
        {
            string pkValue = "ChangeFeedDiagnostics";
            CosmosClient client = TestCommon.CreateCosmosClient();
            Container container = client.GetContainer(this.database.Id, this.Container.Id);
            List<Task<ItemResponse<ToDoActivity>>> createItemsTasks = new List<Task<ItemResponse<ToDoActivity>>>();
            for (int i = 0; i < 100; i++)
            {

                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pkValue);
                createItemsTasks.Add(container.CreateItemAsync<ToDoActivity>(item, new PartitionKey(item.status)));
            }

            await Task.WhenAll(createItemsTasks);

            ChangeFeedRequestOptions requestOptions = disableDiagnostics ? ChangeFeedRequestOptionDisableDiagnostic : null;
            ChangeFeedIterator changeFeedIterator = ((ContainerCore)(container as ContainerInlineCore)).GetChangeFeedStreamIterator(continuationToken: null, changeFeedRequestOptions: requestOptions);
            while (changeFeedIterator.HasMoreResults)
            {
                using (ResponseMessage response = await changeFeedIterator.ReadNextAsync())
                {
                    CosmosDiagnosticsTests.VerifyChangeFeedDiagnostics(
                       diagnostics: response.Diagnostics,
                        disableDiagnostics: disableDiagnostics);
                }
            }
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

                CosmosDiagnosticsTests.VerifyPointDiagnostics(
                    diagnostics: itemResponse.Diagnostics,
                    disableDiagnostics: false);
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

            long readFeedTotalOutputDocumentCount = await this.ExecuteQueryAndReturnOutputDocumentCount(
                queryText: null,
                expectedItemCount: totalItems,
                disableDiagnostics: disableDiagnostics);

            Assert.AreEqual(totalItems, readFeedTotalOutputDocumentCount);

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
            RequestOptions requestOptions = new RequestOptions();
            if (disableDiagnostics)
            {
                requestOptions.DiagnosticContextFactory = () => EmptyCosmosDiagnosticsContext.Singleton;
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

            await databaseResponse.Database.DeleteAsync();

            databaseResponse = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(
              id: Guid.NewGuid().ToString(),
              requestOptions: requestOptions);
            Assert.IsNotNull(databaseResponse.Diagnostics);
            diagnostics = databaseResponse.Diagnostics.ToString();
            if (disableDiagnostics)
            {
                Assert.AreEqual(string.Empty, diagnostics);
                return;
            }

            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));

            await databaseResponse.Database.DeleteAsync();
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

   
            CosmosDiagnosticsContext diagnosticsContext = (diagnostics as CosmosDiagnosticsCore).Context;

            // If all the pages are buffered then several of the normal summary validation will fail.
            if(diagnosticsContext.TotalRequestCount > 0)
            {
                DiagnosticValidator.ValidateCosmosDiagnosticsContext(diagnosticsContext);
            }

            DiagnosticValidator.ValidateQueryDiagnostics(diagnosticsContext, isFirstPage);
        }

        public static void VerifyPointDiagnostics(
            CosmosDiagnostics diagnostics,
            bool disableDiagnostics)
        {
            string info = diagnostics.ToString();

            if (disableDiagnostics)
            {
                Assert.AreEqual(string.Empty, info);
                return;
            }

            CosmosDiagnosticsContext diagnosticsContext = (diagnostics as CosmosDiagnosticsCore).Context;
            DiagnosticValidator.ValidatePointOperationDiagnostics(diagnosticsContext);
        }

        public static void VerifyChangeFeedDiagnostics(
            CosmosDiagnostics diagnostics,
            bool disableDiagnostics)
        {
            string info = diagnostics.ToString();

            if (disableDiagnostics)
            {
                Assert.AreEqual(string.Empty, info);
                return;
            }

            CosmosDiagnosticsContext diagnosticsContext = (diagnostics as CosmosDiagnosticsCore).Context;
            DiagnosticValidator.ValidateChangeFeedOperationDiagnostics(diagnosticsContext);
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


        private async Task<long> ExecuteQueryAndReturnOutputDocumentCount(
            string queryText,
            int expectedItemCount,
            bool disableDiagnostics)
        {
            QueryDefinition sql = null;
            if (queryText != null)
            {
                sql = new QueryDefinition(queryText);
            }

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxItemCount = 1,
                MaxConcurrency = 1,
            };

            if (disableDiagnostics)
            {
                requestOptions.DiagnosticContextFactory = () => EmptyCosmosDiagnosticsContext.Singleton;
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
                if(queryText == null)
                {
                    CosmosDiagnosticsTests.VerifyPointDiagnostics(
                        response.Diagnostics,
                        disableDiagnostics);
                }
                else
                {
                    VerifyQueryDiagnostics(
                       response.Diagnostics,
                       isFirst,
                       disableDiagnostics);
                }
               
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
                if (queryText == null)
                {
                    CosmosDiagnosticsTests.VerifyPointDiagnostics(
                        response.Diagnostics,
                        disableDiagnostics);
                }
                else
                {
                    VerifyQueryDiagnostics(
                       response.Diagnostics,
                       isFirst,
                       disableDiagnostics);
                }

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
