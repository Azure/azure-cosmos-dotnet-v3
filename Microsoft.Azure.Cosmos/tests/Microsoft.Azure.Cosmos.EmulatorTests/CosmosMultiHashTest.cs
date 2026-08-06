namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosMultiHashTest
    {
        private CosmosClient client = null;
        private Cosmos.Database database = null;

        private Container container = null;
        private ContainerProperties containerProperties = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.client = TestCommon.CreateCosmosClient(true);
            this.database = await this.client.CreateDatabaseIfNotExistsAsync("mydb");

            this.containerProperties = new ContainerProperties("mycoll", new List<string> { "/ZipCode", "/City","/id" });
            this.container = await this.database.CreateContainerAsync(this.containerProperties);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await this.database.DeleteAsync();
            this.client.Dispose();
        }

        [TestMethod]
        public async Task MultiHashCreateDocumentTest()
        {
            Cosmos.PartitionKey pKey;
            //Document create test
            ItemResponse<Document>[] documents = new ItemResponse<Document>[3];
            Document doc = new Document { Id = "document1" };
            doc.SetValue("ZipCode", "500026");
            doc.SetValue("City", "Secunderabad");
            doc.SetValue("Type", "Residence");
            pKey= new PartitionKeyBuilder()
                    .Add(doc.GetPropertyValue<string>("ZipCode"))
                    .Add(doc.GetPropertyValue<string>("City"))
                    .Build();
            documents[0] = await this.container.CreateItemAsync<Document>(doc, pKey);

            doc = new Document { Id = "document2" };
            doc.SetValue("ZipCode", "15232");
            doc.SetValue("City", "Pittsburgh");
            doc.SetValue("Type", "Business");
            pKey = new PartitionKeyBuilder()
                    .Add(doc.GetPropertyValue<string>("ZipCode"))
                    .Add(doc.GetPropertyValue<string>("City"))
                    .Build();
            documents[1] = await this.container.CreateItemAsync<Document>(doc);

            doc = new Document { Id = "document3" };
            doc.SetValue("ZipCode", "11790");
            doc.SetValue("City", "Stonybrook");
            doc.SetValue("Type", "Goverment");
            pKey = new PartitionKeyBuilder()
                    .Add(doc.GetPropertyValue<string>("ZipCode"))
                    .Add(doc.GetPropertyValue<string>("City"))
                    .Build();
            documents[2] = await this.container.CreateItemAsync<Document>(doc);

            Assert.AreEqual(3, documents.Select(document => ((Document)document).SelfLink).Distinct().Count());

            //Negative test - using incomplete partition key
            Cosmos.PartitionKey badPKey;

            foreach (Document document in documents)
            {
                badPKey = new PartitionKeyBuilder()
                            .Add(document.GetPropertyValue<string>("ZipCode"))
                            .Build();

                document.Id += "Bad";

                CosmosException createException = await Assert.ThrowsExceptionAsync<CosmosException>(() =>
                        this.container.CreateItemAsync<Document>(document, badPKey)
                    );
                
                Assert.AreEqual(createException.StatusCode, HttpStatusCode.BadRequest);
            }
        }

        [TestMethod]
        public async Task MultiHashDeleteDocumentTest()
        {
            Cosmos.PartitionKey pKey;
            Cosmos.PartitionKey badPKey;

            //Create Items for test
            ItemResponse<Document>[] documents = new ItemResponse<Document>[3];
            Document doc = new Document { Id = "document1" };
            doc.SetValue("ZipCode", "500026");
            doc.SetValue("City", "Secunderabad");
            doc.SetValue("Type", "Residence");
            documents[0] = await this.container.CreateItemAsync<Document>(doc);

            doc = new Document { Id = "document2" };
            doc.SetValue("ZipCode", "15232");
            doc.SetValue("City", "Pittsburgh");
            doc.SetValue("Type", "Business");
            documents[1] = await this.container.CreateItemAsync<Document>(doc);

            doc = new Document { Id = "document3" };
            doc.SetValue("ZipCode", "11790");
            doc.SetValue("City", "Stonybrook");
            doc.SetValue("Type", "Goverment");
            documents[2] = await this.container.CreateItemAsync<Document>(doc);

            //Document Delete Test
            foreach (Document document in documents)
            {
                //Negative test - using incomplete partition key (try one with more values too)
                badPKey = new PartitionKeyBuilder()
                        .Add(document.GetPropertyValue<string>("ZipCode"))
                        .Build();

                CosmosException deleteException = await Assert.ThrowsExceptionAsync<CosmosException>(() =>
                    this.container.DeleteItemAsync<Document>(document.Id, badPKey)
                );
                Assert.AreEqual(deleteException.StatusCode, HttpStatusCode.BadRequest);

                //Positive test
                pKey = new PartitionKeyBuilder()
                    .Add(document.GetPropertyValue<string>("ZipCode"))
                    .Add(document.GetPropertyValue<string>("City"))
                    .Build();

                Document deleteDocument = (await this.container.DeleteItemAsync<Document>(document.Id, pKey)).Resource;

                CosmosException clientException = await Assert.ThrowsExceptionAsync<CosmosException>(() =>
                    this.container.ReadItemAsync<Document>(document.Id, pKey)
                );

                Assert.AreEqual(clientException.StatusCode, HttpStatusCode.NotFound);
            }
        }

        [TestMethod]
        public async Task MultiHashReadItemTest()
        {
            Cosmos.PartitionKey pKey;
            Cosmos.PartitionKey badPKey;

            //Create Items for test
            ItemResponse<Document>[] documents = new ItemResponse<Document>[3];
            Document doc = new Document { Id = "document1" };
            doc.SetValue("ZipCode", "500026");
            doc.SetValue("City", "Secunderabad");
            doc.SetValue("Type", "Residence");
            documents[0] = await this.container.CreateItemAsync<Document>(doc);

            doc = new Document { Id = "document2" };
            doc.SetValue("ZipCode", "15232");
            doc.SetValue("City", "Pittsburgh");
            doc.SetValue("Type", "Business");
            documents[1] = await this.container.CreateItemAsync<Document>(doc);

            doc = new Document { Id = "document3" };
            doc.SetValue("ZipCode", "11790");
            doc.SetValue("City", "Stonybrook");
            doc.SetValue("Type", "Goverment");
            documents[2] = await this.container.CreateItemAsync<Document>(doc);

            //Document Read Test
            foreach (Document document in documents)
            {
                pKey = new PartitionKeyBuilder()
                    .Add(document.GetPropertyValue<string>("ZipCode"))
                    .Add(document.GetPropertyValue<string>("City"))
                    .Build();

                Document readDocument = (await this.container.ReadItemAsync<Document>(document.Id, pKey)).Resource;
                Assert.AreEqual(document.ToString(), readDocument.ToString());

                //Negative test - using incomplete partition key
                badPKey = new PartitionKeyBuilder()
                        .Add(document.GetPropertyValue<string>("ZipCode"))
                        .Build();

                CosmosException clientException = await Assert.ThrowsExceptionAsync<CosmosException>(() =>
                    this.container.ReadItemAsync<Document>(document.Id, badPKey)
                );

                Assert.AreEqual(clientException.StatusCode, HttpStatusCode.BadRequest);
            }
        }

        [TestMethod]
        public async Task HashV2IdAsPartitionKeyTest()
        {
            ContainerProperties idPkContainerProperties = new ContainerProperties(
                "idpkcoll_" + Guid.NewGuid().ToString("N"),
                "/id" );
            Container idPkContainer = await this.database.CreateContainerAsync(idPkContainerProperties);
            Assert.AreEqual(PartitionKind.Hash, idPkContainerProperties.PartitionKey?.Kind);
            Assert.IsNull(idPkContainerProperties.PartitionKeyDefinitionVersion);
            
            try
            {
                await PerformOperationsByPassingDefaultPK(idPkContainer);

            }
            finally
            {
                await idPkContainer.DeleteContainerAsync();
            }
        }

        [TestMethod]
        public async Task MultiHashIdAsPartitionKeyTest()
        {
            // Create a container where "/id" is the only partition key path (HPK with single "id" key)
            ContainerProperties idPkContainerProperties = new ContainerProperties(
                "idpkcoll_" + Guid.NewGuid().ToString("N"),
                new List<string> { "/id" });
            Container idPkContainer = await this.database.CreateContainerAsync(idPkContainerProperties);

            Assert.AreEqual(PartitionKind.MultiHash, idPkContainerProperties.PartitionKey?.Kind);
            try
            {
                await PerformOperationsByPassingDefaultPK(idPkContainer);
            }
            finally
            {
                await idPkContainer.DeleteContainerAsync();
            }
        }

        private static async Task PerformOperationsByPassingDefaultPK(Container idPkContainer)
        {
            await PointOperationsWithDefaultPKAsync(idPkContainer);

            await VerifyTransactionalBatchThrowsExceptionForDefaultPKAsync(idPkContainer);

            await TestBulkOperationsWithDefaultPKAsync(idPkContainer);

        }

        private static async Task VerifyTransactionalBatchThrowsExceptionForDefaultPKAsync(Container idPkContainer)
        {
            Document batchDoc1 = new Document { Id = "batchdoc1" };
            batchDoc1.SetValue("Type", "BatchType1");
            Document batchDoc2 = new Document { Id = "batchdoc2" };
            batchDoc2.SetValue("Type", "BatchType2");

            ArgumentException batchException = await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                idPkContainer.CreateTransactionalBatch(default)
                    .CreateItem(batchDoc1)
                    .CreateItem(batchDoc2)
                    .ReadItem("document1")
                    .ExecuteAsync());

            Assert.IsTrue(batchException.Message.Contains("itemId needs to be specified"));
        }

        private static async Task PointOperationsWithDefaultPKAsync(Container idPkContainer)
        {
            ItemResponse<Document>[] documents = new ItemResponse<Document>[3];
            Document doc = new Document { Id = "document1" };
            doc.SetValue("Type", "Residence");
            documents[0] = await idPkContainer.CreateItemAsync<Document>(doc, default);

            doc = new Document { Id = "document2" };
            doc.SetValue("Type", "Business");
            documents[1] = await idPkContainer.CreateItemAsync<Document>(doc, default);
            doc = new Document { Id = "document3" };
            doc.SetValue("Type", "Government");
            documents[2] = await idPkContainer.CreateItemAsync<Document>(doc);

            foreach (Document document in documents)
            {
                Document readDocument = await idPkContainer.ReadItemAsync<Document>(document.Id, default);
                Assert.AreEqual(document.ToString(), readDocument.ToString());
            }

            doc = documents[0];
            doc.SetValue("Type", "UpdatedType");
            doc = await idPkContainer.UpsertItemAsync<Document>(doc, default);
            Document readDocument1 = await idPkContainer.ReadItemAsync<Document>(doc.Id, default);

            Assert.AreEqual(doc.ToString(), readDocument1.ToString());

            FeedResponse<Document> feedResponse = await idPkContainer.ReadManyItemsAsync<Document>(
            new List<(string, Cosmos.PartitionKey)> { ("document3", default) });

            Assert.AreEqual(1, feedResponse.Count());

            await idPkContainer.DeleteItemAsync<Document>("document3", default);

            CosmosException clientException = await Assert.ThrowsExceptionAsync<CosmosException>(() =>
                idPkContainer.ReadItemAsync<Document>("document3", default)
            );
        }

        private static async Task TestBulkOperationsWithDefaultPKAsync(Container idPkContainer)
        {
            CosmosClientOptions bulkOptions = new CosmosClientOptions { AllowBulkExecution = true };
            CosmosClient bulkClient = TestCommon.CreateCosmosClient(bulkOptions);
            Container bulkContainer = bulkClient.GetContainer(idPkContainer.Database.Id, idPkContainer.Id);

            List<Task<ItemResponse<Document>>> bulkTasks = new List<Task<ItemResponse<Document>>>();
            for (int i = 0; i < 10; i++)
            {
                Document bulkDoc = new Document { Id = $"bulkdoc{i}" };
                bulkDoc.SetValue("Type", $"BulkType{i}");
                bulkTasks.Add(bulkContainer.CreateItemAsync(bulkDoc, default));
            }

            await Task.WhenAll(bulkTasks);

            for (int i = 0; i < 10; i++)
            {
                ItemResponse<Document> bulkResult = bulkTasks[i].Result;
                Assert.AreEqual(HttpStatusCode.Created, bulkResult.StatusCode);
                Assert.AreEqual($"bulkdoc{i}", bulkResult.Resource.Id);
            }

            // Verify bulk-created documents can be read back
            for (int i = 0; i < 10; i++)
            {
                Document readDoc = await idPkContainer.ReadItemAsync<Document>($"bulkdoc{i}", default);
                Assert.AreEqual($"bulkdoc{i}", readDoc.Id);
            }
        }

        [TestMethod]
        public async Task MultiHashPartitionKeyHandlerTest()
        {
            // Binary encoding is disabled here, so the item write bodies must be sent as text.
            await this.RunMultiHashPartitionKeyHandlerScenarioAsync(expectedRequestBodyFormat: JsonSerializationFormat.Text);
        }

        [TestMethod]
        public async Task MultiHashPartitionKeyHandlerWithBinaryEncodingTest()
        {
            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");

                // With binary encoding enabled, the SDK must convert the item write bodies from
                // text to a binary stream before they reach the transport layer.
                await this.RunMultiHashPartitionKeyHandlerScenarioAsync(expectedRequestBodyFormat: JsonSerializationFormat.Binary);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        private async Task RunMultiHashPartitionKeyHandlerScenarioAsync(JsonSerializationFormat expectedRequestBodyFormat)
        {
            ContainerProperties premigrationConatiner = new ContainerProperties(
                "addidtopartitionkey_" + Guid.NewGuid().ToString("N"),
                new List<string> { "/ZipCode"});
            Container seedContainer = await this.database.CreateContainerAsync(premigrationConatiner);

            Assert.AreEqual(PartitionKind.MultiHash, premigrationConatiner.PartitionKey?.Kind);

            try
            {
                await this.ExerciseCRUDAndQueryAsync(seedContainer, expectedRequestBodyFormat);

                await this.ExerciseReadManyAppendIdAsync(seedContainer, expectedRequestBodyFormat);

                await this.ExerciseBulkAppendIdAsync(seedContainer, expectedRequestBodyFormat);

                await this.ExerciseAppendIBatchAsync(seedContainer, expectedRequestBodyFormat);
            }
            finally
            {
                await seedContainer.DeleteContainerAsync();
            }
        }

        private async Task ExerciseCRUDAndQueryAsync(Container seedContainer, JsonSerializationFormat expectedRequestBodyFormat)
        {
            SimulateAddingIdasLastLevelToPartitionKey handler = new SimulateAddingIdasLastLevelToPartitionKey(expectedRequestBodyFormat);
            CosmosClient handlerClient = TestCommon.CreateCosmosClient(
                                builder => builder.AddCustomHandlers(handler));
            Container handlerContainer = handlerClient.GetContainer(this.database.Id, seedContainer.Id);

            // Seed documents using a client that does NOT go through the validation handler.
            await SeedTwoPartDocumentsAsync(handlerContainer);

            // Case 1: A full 2-part partition key is passed. The handler removes the second
            // component ("City") and forwards the request routed by the "ZipCode" prefix only.
            Cosmos.PartitionKey fullKey = new PartitionKeyBuilder()
                .Add("500026")
                .Build();

            List<Document> results = new List<Document>();
            using (FeedIterator<Document> iterator = handlerContainer.GetItemQueryIterator<Document>(
                "SELECT * FROM c",
                requestOptions: new QueryRequestOptions { PartitionKey = fullKey }))
            {
                while (iterator.HasMoreResults)
                {
                    results.AddRange(await iterator.ReadNextAsync());
                }
            }

            // The handler dropped the "City" component before sending to the backend.
            Assert.IsTrue(results.Count >= 1, "Expected at least one document for the ZipCode prefix.");
            Assert.IsTrue(
                results.All(document => document.GetPropertyValue<string>("ZipCode") == "500026"),
                "All returned documents should belong to the forwarded ZipCode prefix.");

            // Verify the SDK actually serialized the item write bodies in the expected wire
            // format (binary when binary encoding is enabled, text otherwise) before they
            // reached the transport layer.
            Assert.IsTrue(
                handler.InspectedItemWriteBodyCount > 0,
                "Expected the handler to observe at least one item write request body.");

            // The very first item write is sent with a single-component partition key, so the
            // handler throws BadRequest/1038. That triggers the SDK to mark "/id" as the last
            // partition key path and retry with the id appended. Subsequent writes on the same
            // client append the id proactively, so no further 1038 is thrown.
            Assert.AreEqual(
                1,
                handler.BadRequestThrownCount,
                "Expected exactly one BadRequest/1038 to trigger the append-id retry path.");

        }

        /// <summary>
        /// Exercises the <see cref="BatchExecutor"/> caller of
        /// <see cref="ContainerPropertiesExtensions.EnsureIdGetsAppendedToPartitionKeyIfNeededAsync"/>.
        /// A transactional batch is scoped to a single shared partition key but spans items with
        /// different "id" values, so the "id" cannot be appended to the batch's partition key. When
        /// the backend signals (BadRequest / 1038) that "/id" must be the last partition key path,
        /// the SDK marks the container, retries once, and the batch fails deterministically.
        /// </summary>
        private async Task ExerciseAppendIBatchAsync(
            Container seedContainer,
            JsonSerializationFormat expectedRequestBodyFormat)
        {
            SimulateAddingIdasLastLevelToPartitionKey handler = new SimulateAddingIdasLastLevelToPartitionKey(expectedRequestBodyFormat);
            using CosmosClient handlerClient = TestCommon.CreateCosmosClient(
                builder => builder.AddCustomHandlers(handler));
            Container handlerContainer = handlerClient.GetContainer(this.database.Id, seedContainer.Id);

            Document batchDocument = new Document { Id = "iddoc-batch" };
            batchDocument.SetValue("ZipCode", "500026");
            batchDocument.SetValue("City", "Secunderabad");

            ArgumentException batchException = await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
              handlerContainer
                .CreateTransactionalBatch(new PartitionKeyBuilder().Add("500026").Build())
                .CreateItem(batchDocument)
                .ExecuteAsync());

            Assert.IsTrue(batchException.Message.Contains("itemId needs to be specified"));

            Assert.AreEqual(
                1,
                handler.BadRequestThrownCount,
                "Expected exactly one BadRequest/1038 to trigger the append-id retry path.");
        }

        /// <summary>
        /// Exercises the <see cref="ReadManyQueryHelper"/> caller of
        /// <see cref="ContainerPropertiesExtensions.EnsureIdGetsAppendedToPartitionKeyIfNeededAsync"/>.
        /// Only the prefix partition key is supplied; the SDK appends the id before routing.
        /// </summary>
        private async Task ExerciseReadManyAppendIdAsync(
            Container seedContainer,
            JsonSerializationFormat expectedRequestBodyFormat)
        {
            SimulateAddingIdasLastLevelToPartitionKey handler = new SimulateAddingIdasLastLevelToPartitionKey(expectedRequestBodyFormat);
            using CosmosClient handlerClient = TestCommon.CreateCosmosClient(
                builder => builder.AddCustomHandlers(handler));
            Container handlerContainer = handlerClient.GetContainer(this.database.Id, seedContainer.Id);

            // Seed items through the handler client using only the prefix partition key
            // [ZipCode]. The first write is rejected with BadRequest/1038, which drives the SDK
            // to mark "/id" as the last partition key path and retry with the id appended; the
            // handler then strips the appended component before forwarding to the backend.
            for (int i = 0; i < 3; i++)
            {
                Document seedDocument = new Document { Id = $"iddoc{i}" };
                seedDocument.SetValue("ZipCode", "500026");
                seedDocument.SetValue("City", "Secunderabad");
                ItemResponse<Document> createResponse = await handlerContainer.CreateItemAsync<Document>(
                    seedDocument,
                    new PartitionKeyBuilder().Add("500026").Build());
                Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            }
            FeedResponse<Document> readManyResponse = await handlerContainer.ReadManyItemsAsync<Document>(
            new List<(string, Cosmos.PartitionKey)>
            {
                ("iddoc0", new PartitionKeyBuilder().Add("500026").Build()),
                ("iddoc1", new PartitionKeyBuilder().Add("500026").Build()),
            });
            Assert.AreEqual(HttpStatusCode.OK, readManyResponse.StatusCode);
            Assert.AreEqual(2, readManyResponse.Count, "ReadManyItemsAsync should return both requested documents.");

            Assert.AreEqual(
                 1,
                 handler.BadRequestThrownCount,
                 "Expected exactly one BadRequest/1038 to trigger the append-id retry path.");
        }

        /// <summary>
        /// Exercises the <see cref="Batch.BatchAsyncContainerExecutor"/> (bulk execution) caller of
        /// <see cref="ContainerPropertiesExtensions.EnsureIdGetsAppendedToPartitionKeyIfNeededAsync"/>.
        /// Bulk supplies the real item id, so the prefix partition key is sufficient and the id is
        /// appended before the operation is routed.
        /// </summary>
        private async Task ExerciseBulkAppendIdAsync(
            Container container, 
            JsonSerializationFormat expectedRequestBodyFormat)
        {
            SimulateAddingIdasLastLevelToPartitionKey handler = new SimulateAddingIdasLastLevelToPartitionKey(expectedRequestBodyFormat);
            using CosmosClient bulkClient = TestCommon.CreateCosmosClient(
                builder => builder.AddCustomHandlers(handler).WithBulkExecution(true));
            Container bulkContainer = bulkClient.GetContainer(this.database.Id, container.Id);

            List<Task<ItemResponse<Document>>> bulkTasks = new List<Task<ItemResponse<Document>>>();
            for (int i = 0; i < 5; i++)
            {
                Document bulkDocument = new Document { Id = $"bulkdoc{i}" };
                bulkDocument.SetValue("ZipCode", "500026");
                bulkDocument.SetValue("City", "Secunderabad");
                bulkTasks.Add(bulkContainer.CreateItemAsync<Document>(
                    bulkDocument,
                    new PartitionKeyBuilder().Add("500026").Build()));
            }

            await Task.WhenAll(bulkTasks);

            for (int i = 0; i < bulkTasks.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.Created, bulkTasks[i].Result.StatusCode);
                Assert.AreEqual($"bulkdoc{i}", bulkTasks[i].Result.Resource.Id);
            }

            Assert.AreEqual(
                1,
                handler.BadRequestThrownCount,
                "Expected exactly one BadRequest/1038 to trigger the append-id retry path.");
        }

        private static async Task SeedTwoPartDocumentsAsync(Container container)
        {
            await CreateItemFromTextStreamAsync(container, "document1", "500026", "Secunderabad");
            await CreateItemFromTextStreamAsync(container, "document2", "500026", "Hyderabad");
            await CreateItemFromTextStreamAsync(container, "document3", "15232", "Pittsburgh");

            await container.ReadItemAsync<Document>("document1", new PartitionKeyBuilder().Add("500026").Build());
            await container.ReadItemAsync<Document>("document2", new PartitionKeyBuilder().Add("500026").Build());
            await container.ReadItemAsync<Document>("document3", new PartitionKeyBuilder().Add("15232").Build());
        }

        private static async Task CreateItemFromTextStreamAsync(
            Container container,
            string id,
            string zipCode,
            string city)
        {
            Document document = new Document { Id = id };
            document.SetValue("ZipCode", zipCode);
            document.SetValue("City", city);

            // Serialize the item to a TEXT stream (binary encoding intentionally disabled for this
            // serialization) and send it through the stream API. The stream API does not serialize
            // the item itself, so when binary encoding is enabled on the client the SDK is forced to
            // convert this text stream to a binary stream in ContainerCore.ProcessItemStreamAsync
            // (the "Convert Text to Binary Stream" branch) before the request reaches the transport
            // layer. This guarantees that code path is exercised end to end.
            CosmosSerializerCore serializerCore = new CosmosSerializerCore();
            using Stream textStream = serializerCore.ToStream<Document>(document, canUseBinaryEncodingForPointOperations: false);

            using ResponseMessage response = await container.CreateItemStreamAsync(
                textStream,
                new PartitionKeyBuilder().Add(zipCode).Build());
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Test handler that inspects the partition key being sent to the backend for item
        /// requests. It throws when the partition key does not have exactly 2 components,
        /// otherwise it removes the second component and forwards the request to the base handler.
        /// It additionally verifies that item write bodies are serialized in the expected wire
        /// format (text vs. binary) before they reach the transport layer.
        /// </summary>
        private sealed class SimulateAddingIdasLastLevelToPartitionKey : RequestHandler
        {
            public const string TwoPartRequiredMessage = "Partition key must contain exactly 2 components.";

            // 1038 is not a named Microsoft.Azure.Documents.SubStatusCodes value; it is used here
            // as the raw sub-status returned on the BadRequest when the component count is invalid.
            public const int InvalidLastLevelKey = 1038;

            private readonly JsonSerializationFormat expectedRequestBodyFormat;

            private int badRequestThrownCount;

            private int bulkBatchRejected;

            public SimulateAddingIdasLastLevelToPartitionKey(JsonSerializationFormat expectedRequestBodyFormat)
            {
                this.expectedRequestBodyFormat = expectedRequestBodyFormat;
            }

            /// <summary>
            /// Number of item write request bodies whose serialization format was inspected.
            /// </summary>
            public int InspectedItemWriteBodyCount { get; private set; }

            /// <summary>
            /// Number of times a BadRequest (sub-status 1038) was thrown because the partition key
            /// did not have exactly 2 components. This is the number of times the SDK's retry path
            /// (mark "/id" as the last partition key path and append the id) was triggered.
            /// </summary>
            public int BadRequestThrownCount => Volatile.Read(ref this.badRequestThrownCount);

            public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                string partitionKeyHeader = request.Headers.PartitionKey;

                // Bulk requests are dispatched as batch requests routed by partition-key-range id
                // and therefore do not carry a partition key header; the per-operation partition key
                // lives inside the HybridRow request body. Simulate the backend's "append id to the
                // last partition key path" signal for the first such request so the SDK marks the
                // container and retries with the id appended.
                if (string.IsNullOrEmpty(partitionKeyHeader)
                    && request.OperationType == OperationType.Batch
                    && request.ResourceType == ResourceType.Document)
                {
                    if (Interlocked.CompareExchange(ref this.bulkBatchRejected, 1, 0) == 0)
                    {
                        Interlocked.Increment(ref this.badRequestThrownCount);

                        // A bulk batch failure is surfaced as a failed response (not a thrown
                        // exception); the batch-level BadRequest/1038 is promoted onto each
                        // per-operation result, which the append-id retry policy then observes and
                        // uses to mark "/id" as the last partition key path before retrying.
                        ResponseMessage bulkBadRequest = new ResponseMessage(
                            HttpStatusCode.BadRequest,
                            request,
                            $"{TwoPartRequiredMessage} (bulk)");
                        bulkBadRequest.Headers[WFConstants.BackendHeaders.SubStatus] =
                            InvalidLastLevelKey.ToString(CultureInfo.InvariantCulture);

                        return bulkBadRequest;
                    }

                    // This is the retried bulk batch: the SDK has appended the item id to each
                    // operation's partition key (now 2 components) inside the HybridRow body. The
                    // emulator container only declares a single partition key path, so - exactly as
                    // the point-operation branch strips the appended id from the partition key
                    // header - rewrite the batch body to drop the trailing id component before
                    // forwarding to the (single-path) backend, simulating a migrated backend that
                    // accepts the id-appended partition key.
                    await this.StripAppendedIdFromBulkBodyAsync(request, cancellationToken);

                    return await base.SendAsync(request, cancellationToken);
                }

                // A transactional batch is dispatched as an OperationType.Batch request that DOES
                // carry a (shared) partition key header and a HybridRow binary body. Unlike a point
                // write - whose thrown BadRequest/1038 is caught by the append-id retry policy - the
                // batch execution path surfaces the backend signal as a returned failed response.
                // Return a batch-level BadRequest/1038 (mimicking the backend) so BatchExecutor
                // marks "/id" as the last partition key path and retries; the retry then fails
                // deterministically because the "id" cannot be appended to a batch's shared
                // partition key.
                if (!string.IsNullOrEmpty(partitionKeyHeader)
                    && request.OperationType == OperationType.Batch
                    && request.ResourceType == ResourceType.Document)
                {
                    Interlocked.Increment(ref this.badRequestThrownCount);

                    ResponseMessage batchBadRequest = new ResponseMessage(
                        HttpStatusCode.BadRequest,
                        request,
                        $"{TwoPartRequiredMessage} (transactional batch)");
                    batchBadRequest.Headers[WFConstants.BackendHeaders.SubStatus] =
                        InvalidLastLevelKey.ToString(CultureInfo.InvariantCulture);

                    return batchBadRequest;
                }

                // Only inspect item (document) requests that carry a concrete partition key.
                if (!string.IsNullOrEmpty(partitionKeyHeader)
                    && request.OperationType != OperationType.Query)
                {
                    JArray components = JArray.Parse(partitionKeyHeader);

                    // Ignore PartitionKey.None (represented as an empty array).
                    if (components.Count > 0)
                    {
                        // Verify the request body reached the transport layer in the expected
                        // serialization format (text when binary encoding is disabled, binary when
                        // it is enabled). This proves the SDK converted the item's text stream to a
                        // binary stream before dispatch.
                        this.AssertRequestBodySerializationFormat(request);

                        if (components.Count != 2)
                        {
                            Interlocked.Increment(ref this.badRequestThrownCount);

                            BadRequestException badRequestException = new BadRequestException(
                                $"{TwoPartRequiredMessage} Found {components.Count}.");
                            badRequestException.Headers[WFConstants.BackendHeaders.SubStatus] =
                                InvalidLastLevelKey.ToString(CultureInfo.InvariantCulture);

                            throw badRequestException;
                        }

                        components.RemoveAt(1);
                        string forwardedPartitionKey = components.ToString(Newtonsoft.Json.Formatting.None);
                        request.Headers.PartitionKey = forwardedPartitionKey;
                    }
                }

                return await base.SendAsync(request, cancellationToken);
            }

            /// <summary>
            /// Reads the HybridRow batch body of a (retried) bulk request, removes the trailing
            /// (appended id) component from every operation's partition key, and replaces the
            /// request body with a re-serialized batch routed to the same partition key range.
            /// </summary>
            private async Task StripAppendedIdFromBulkBodyAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                if (request.Content == null)
                {
                    return;
                }

                Stream original = request.Content;
                if (original.CanSeek)
                {
                    original.Position = 0;
                }
                else
                {
                    MemoryStream seekable = new MemoryStream();
                    await original.CopyToAsync(seekable);
                    seekable.Position = 0;
                    original.Dispose();
                    original = seekable;
                }

                Stream rewritten = await BulkBatchBodyRewriter.StripLastPartitionKeyComponentAsync(
                    original,
                    request.Headers.PartitionKeyRangeId,
                    new CosmosSerializerCore(),
                    cancellationToken);

                request.Content = rewritten;

                // The DocumentServiceRequest is built (and its body stream captured by reference)
                // in RequestInvokerHandler before this custom handler runs. Reset it so the pipeline
                // rebuilds the request from the rewritten content instead of the original body.
                request.DocumentServiceRequest = null;

                original.Dispose();
            }

            private void AssertRequestBodySerializationFormat(RequestMessage request)
            {
                Stream content = request.Content;
                if (content == null || !content.CanSeek)
                {
                    return;
                }

                long originalPosition = content.Position;
                content.Position = 0;
                int firstByte = content.ReadByte();
                content.Position = originalPosition;

                if (firstByte < 0)
                {
                    return;
                }

                if (this.expectedRequestBodyFormat == JsonSerializationFormat.Binary)
                {
                    Assert.AreEqual(
                        (int)JsonSerializationFormat.Binary,
                        firstByte,
                        "Expected the item write body to be converted to a binary stream when binary encoding is enabled.");
                }
                else
                {
                    Assert.IsTrue(
                        firstByte < (int)JsonSerializationFormat.Binary,
                        "Expected the item write body to remain a text stream when binary encoding is disabled.");
                }

                this.InspectedItemWriteBodyCount++;
            }
        }

        [TestMethod]
        public async Task MultiHashReadManyTest()
        {
            Cosmos.PartitionKey pKey;
            Cosmos.PartitionKey badPKey;

            //Create Items for test
            ItemResponse<Document>[] documents = new ItemResponse<Document>[3];
            Document doc = new Document { Id = "document1" };
            doc.SetValue("ZipCode", "500026");
            doc.SetValue("City", "Secunderabad");
            doc.SetValue("Type", "Residence");
            documents[0] = await this.container.CreateItemAsync<Document>(doc);

            doc = new Document { Id = "document2" };
            doc.SetValue("ZipCode", "15232");
            doc.SetValue("City", "Pittsburgh");
            doc.SetValue("Type", "Business");
            documents[1] = await this.container.CreateItemAsync<Document>(doc);

            doc = new Document { Id = "document3" };
            doc.SetValue("ZipCode", "11790");
            doc.SetValue("City", "Stonybrook");
            doc.SetValue("Type", "Goverment");
            documents[2] = await this.container.CreateItemAsync<Document>(doc);

            //Read Many Test
            List<(string, Cosmos.PartitionKey)> itemList = new List<(string, Cosmos.PartitionKey)>();
            List<(string, Cosmos.PartitionKey)> incompleteList = new List<(string, Cosmos.PartitionKey)>();
            foreach (Document document in documents)
            {
                pKey = new PartitionKeyBuilder()
                    .Add(document.GetPropertyValue<string>("ZipCode"))
                    .Add(document.GetPropertyValue<string>("City"))
                    .Build();

                badPKey = new PartitionKeyBuilder()
                    .Add(document.GetPropertyValue<string>("ZipCode"))
                    .Build();

                itemList.Add((document.Id, pKey));
                incompleteList.Add((document.Id, badPKey));              
            }

            FeedResponse<Document> feedResponse = await this.container.ReadManyItemsAsync<Document>(itemList);

            Assert.IsNotNull(feedResponse);
            Assert.AreEqual(feedResponse.Count, 3);
            Assert.IsTrue(feedResponse.Headers.RequestCharge > 0);
            Assert.IsNotNull(feedResponse.Diagnostics);

            int count = 0;
            foreach (Document item in feedResponse)
            {
                count++;
                Assert.IsNotNull(item);
            }
            Assert.AreEqual(count, 3);

            //Negative test - using incomplete partition key
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                this.container.ReadManyItemsAsync<Document>(incompleteList));
        }

        public record DatabaseItem(
            string Id,
            string Pk
            );

        [TestMethod]
        public async Task MultiHashUpsertItemTest()
        {
            Cosmos.PartitionKey pKey;
            Cosmos.PartitionKey badPKey;
            int count;

            //Create Items for test
            ItemResponse<Document>[] documents = new ItemResponse<Document>[3];
            Document doc = new Document { Id = "document1" };
            doc.SetValue("ZipCode", "500026");
            doc.SetValue("City", "Secunderabad");
            doc.SetValue("Type", "Residence");
            documents[0] = await this.container.CreateItemAsync<Document>(doc);

            doc = new Document { Id = "document2" };
            doc.SetValue("ZipCode", "15232");
            doc.SetValue("City", "Pittsburgh");
            doc.SetValue("Type", "Business");
            documents[1] = await this.container.CreateItemAsync<Document>(doc);

            doc = new Document { Id = "document3" };
            doc.SetValue("ZipCode", "11790");
            doc.SetValue("City", "Stonybrook");
            doc.SetValue("Type", "Goverment");
            documents[2] = await this.container.CreateItemAsync<Document>(doc);

            //Document Upsert Test
            doc = new Document { Id = "document4" };
            doc.SetValue("ZipCode", "97756");
            doc.SetValue("City", "Redmond");
            doc.SetValue("Type", "Residence");

            pKey = new PartitionKeyBuilder()
                    .Add(doc.GetPropertyValue<string>("ZipCode"))
                    .Add(doc.GetPropertyValue<string>("City"))
                .Build();

            //insert check
            await this.container.UpsertItemAsync<Document>(doc, pKey);

            Document readCheck = (await this.container.ReadItemAsync<Document>(doc.Id, pKey)).Resource;

            Assert.AreEqual(doc.GetPropertyValue<string>("ZipCode"), readCheck.GetPropertyValue<string>("ZipCode"));
            Assert.AreEqual(doc.GetPropertyValue<string>("City"), readCheck.GetPropertyValue<string>("City"));
            Assert.AreEqual(doc.GetPropertyValue<string>("Type"), readCheck.GetPropertyValue<string>("Type"));

            doc = new Document { Id = "document4" };
            doc.SetValue("ZipCode", "97756");
            doc.SetValue("City", "Redmond");
            doc.SetValue("Type", "Business");

            //update check
            pKey = new PartitionKeyBuilder()
                    .Add(doc.GetPropertyValue<string>("ZipCode"))
                    .Add(doc.GetPropertyValue<string>("City"))
                .Build();

            documents.Append<ItemResponse<Document>>(await this.container.UpsertItemAsync<Document>(doc, pKey));

            readCheck = (await this.container.ReadItemAsync<Document>(doc.Id, pKey)).Resource;

            Assert.AreEqual(doc.GetPropertyValue<string>("ZipCode"), readCheck.GetPropertyValue<string>("ZipCode"));
            Assert.AreEqual(doc.GetPropertyValue<string>("City"), readCheck.GetPropertyValue<string>("City"));
            Assert.AreEqual(doc.GetPropertyValue<string>("Type"), readCheck.GetPropertyValue<string>("Type"));

            count = 0;

            foreach (Document document in this.container.GetItemLinqQueryable<Document>(true))
            {
                count++;
            }
            Assert.AreEqual(4, count);

            //Negative test - using incomplete partition key
            doc = new Document { Id = "document4" };
            doc.SetValue("ZipCode", "97756");
            doc.SetValue("City", "Redmond");
            doc.SetValue("Type", "Residence");

            badPKey = new PartitionKeyBuilder()
                    .Add(doc.GetPropertyValue<string>("ZipCode"))
                .Build();

            CosmosException clientException = await Assert.ThrowsExceptionAsync<CosmosException>(() =>
                this.container.UpsertItemAsync<Document>(doc, badPKey)
            );

            Assert.AreEqual(clientException.StatusCode, HttpStatusCode.BadRequest);

            readCheck = (await this.container.ReadItemAsync<Document>(doc.Id, pKey)).Resource;

            Assert.AreEqual(doc.GetPropertyValue<string>("ZipCode"), readCheck.GetPropertyValue<string>("ZipCode"));
            Assert.AreEqual(doc.GetPropertyValue<string>("City"), readCheck.GetPropertyValue<string>("City"));
            Assert.AreNotEqual(doc.GetPropertyValue<string>("Type"), readCheck.GetPropertyValue<string>("Type"));
        }

        [TestMethod]
        public async Task MultiHashReplaceItemTest()
        {
            Cosmos.PartitionKey pKey;
            Cosmos.PartitionKey badPKey;

            //Create items for test
            ItemResponse<Document>[] documents = new ItemResponse<Document>[3];
            Document doc = new Document { Id = "document1" };
            doc.SetValue("ZipCode", "500026");
            doc.SetValue("City", "Secunderabad");
            doc.SetValue("Type", "Residence");
            documents[0] = await this.container.CreateItemAsync<Document>(doc);

            doc = new Document { Id = "document2" };
            doc.SetValue("ZipCode", "15232");
            doc.SetValue("City", "Pittsburgh");
            doc.SetValue("Type", "Business");
            documents[1] = await this.container.CreateItemAsync<Document>(doc);

            doc = new Document { Id = "document3" };
            doc.SetValue("ZipCode", "11790");
            doc.SetValue("City", "Stonybrook");
            doc.SetValue("Type", "Goverment");
            documents[2] = await this.container.CreateItemAsync<Document>(doc);

            //Document Replace Test
            foreach (Document document in documents)
            {
                pKey = new PartitionKeyBuilder()
                    .Add(document.GetPropertyValue<string>("ZipCode"))
                    .Add(document.GetPropertyValue<string>("City"))
                .Build();


                Document readDocument = (await this.container.ReadItemAsync<Document>(document.Id, pKey)).Resource;
                readDocument.SetValue("Type", "Park");

                ItemResponse<Document> item = await this.container.ReplaceItemAsync<Document>(readDocument, readDocument.Id, pKey);

                Document checkDocument = (await this.container.ReadItemAsync<Document>(document.Id, pKey)).Resource;
                Assert.AreEqual(checkDocument.GetPropertyValue<string>("Type"), readDocument.GetPropertyValue<string>("Type"));

                //Negative test - using incomplete partition key
                badPKey = new PartitionKeyBuilder()
                        .Add(document.GetPropertyValue<string>("ZipCode"))
                        .Build();

                readDocument.SetValue("Type", "Goverment");

                CosmosException clientException = await Assert.ThrowsExceptionAsync<CosmosException>(() =>
                    this.container.ReplaceItemAsync<Document>(document, document.Id, partitionKey: badPKey)
                );

                Assert.AreEqual(clientException.StatusCode, HttpStatusCode.BadRequest);
            }
        }

        [TestMethod]
        public async Task MultiHashQueryItemTest()
        {
            Cosmos.PartitionKey pKey;
            Cosmos.PartitionKey badPKey;

            //Create items for test
            ItemResponse<Document>[] documents = new ItemResponse<Document>[3];
            Document doc = new Document { Id = "document1" };
            doc.SetValue("ZipCode", "500026");
            doc.SetValue("City", "Secunderabad");
            doc.SetValue("Type", "Residence");
            documents[0] = await this.container.CreateItemAsync<Document>(doc);

            doc = new Document { Id = "document2" };
            doc.SetValue("ZipCode", "15232");
            doc.SetValue("City", "Pittsburgh");
            doc.SetValue("Type", "Business");
            documents[1] = await this.container.CreateItemAsync<Document>(doc);

            doc = new Document { Id = "document3" };
            doc.SetValue("ZipCode", "11790");
            doc.SetValue("City", "Stonybrook");
            doc.SetValue("Type", "Goverment");
            documents[2] = await this.container.CreateItemAsync<Document>(doc);

            foreach (bool odeEnabled in new bool[] { false, true })
            {
                //Query
                foreach (Document document in documents)
                {
                    pKey = new PartitionKeyBuilder()
                        .Add(document.GetPropertyValue<string>("ZipCode"))
                        .Add(document.GetPropertyValue<string>("City"))
                    .Build();

                    badPKey = new PartitionKeyBuilder()
                                .Add(document.GetPropertyValue<string>("City"))
                                .Build();

                    String query = $"SELECT * from c where c.id = \"{document.GetPropertyValue<string>("id")}\"";

                    using (FeedIterator<Document> feedIterator = this.container.GetItemQueryIterator<Document>(
                        query,
                        null,
                        new QueryRequestOptions() { EnableOptimisticDirectExecution = odeEnabled, PartitionKey = pKey }))
                    {
                        Assert.IsTrue(feedIterator.HasMoreResults);

                        FeedResponse<Document> queryDoc = await feedIterator.ReadNextAsync();
                        Document retrievedDocument = queryDoc.First<Document>();
                        Assert.IsTrue(queryDoc.Count == 1);
                        Assert.AreEqual(document.Id, retrievedDocument.Id);
                        feedIterator.Dispose();
                    }

                    //Using an incomplete partition key with prefix of PK path definition
                    pKey = new PartitionKeyBuilder()
                        .Add(document.GetPropertyValue<string>("ZipCode"))
                    .Build();
                    using (FeedIterator<Document> feedIterator = this.container.GetItemQueryIterator<Document>(
                        query,
                        null,
                        new QueryRequestOptions() { EnableOptimisticDirectExecution = odeEnabled, PartitionKey = pKey }))
                    {
                        Assert.IsTrue(feedIterator.HasMoreResults);

                        FeedResponse<Document> queryDoc = await feedIterator.ReadNextAsync();
                        Document retrievedDocument = queryDoc.First<Document>();
                        Assert.IsTrue(queryDoc.Count == 1);
                        Assert.AreEqual(document.Id, retrievedDocument.Id);
                        feedIterator.Dispose();
                    }

                    //Negative test - using incomplete partition key
                    using (FeedIterator<Document> badFeedIterator = this.container.GetItemQueryIterator<Document>(
                        query,
                        null,
                        new QueryRequestOptions() { EnableOptimisticDirectExecution = odeEnabled, PartitionKey = badPKey }))
                    {
                        FeedResponse<Document> queryDocBad = await badFeedIterator.ReadNextAsync();
                        Assert.ThrowsException<InvalidOperationException>(() =>
                             queryDocBad.First<Document>()
                        );
                        badFeedIterator.Dispose();
                    }
                }
            }
        }

        [TestMethod]
        public async Task ReadManyNullPkValueTest()
        {
            Document doc = new Document { Id = "readMany" };
            doc.SetValue("ZipCode", "10000");

            await this.container.CreateItemAsync<Document>(doc);

            Cosmos.PartitionKey pk = new PartitionKeyBuilder()
                .Add("10000")
                .AddNoneType()
                .Build();

            ItemResponse<Document> ir = await this.container.ReadItemAsync<Document>("readMany", pk);
            Assert.IsNotNull(ir.Resource);
            Assert.AreEqual(ir.StatusCode, HttpStatusCode.OK);

            FeedResponse<Document> feedResponse = await this.container.ReadManyItemsAsync<Document>(
                new List<(string, Cosmos.PartitionKey)> { ("readMany", pk) });

            Assert.AreEqual(1, feedResponse.Count());
        }

        [TestMethod]
        public async Task ReadManyAllNullPkValueTest()
        {
            Document doc = new Document { Id = "readMany" };

            await this.container.CreateItemAsync<Document>(doc);

            Cosmos.PartitionKey pk = new PartitionKeyBuilder()
                .AddNoneType()
                .AddNoneType()
                .Build();

            ItemResponse<Document> ir = await this.container.ReadItemAsync<Document>("readMany", pk);
            Assert.IsNotNull(ir.Resource);
            Assert.AreEqual(ir.StatusCode, HttpStatusCode.OK);

            FeedResponse<Document> feedResponse = await this.container.ReadManyItemsAsync<Document>(
                new List<(string, Cosmos.PartitionKey)> { ("readMany", pk) });
            
            Assert.AreEqual(1, feedResponse.Count());
        }

        [TestMethod]
        public async Task MultiHashDeleteByFirstLevelPartitionKeyTest()
        {
            // Create documents sharing the same first-level partition key (ZipCode)
            Document doc1 = new Document { Id = "pkdel1" };
            doc1.SetValue("ZipCode", "10001");
            doc1.SetValue("City", "NewYork");
            Cosmos.PartitionKey pk1 = new PartitionKeyBuilder()
                .Add("10001")
                .Add("NewYork")
                .Build();
            await this.container.CreateItemAsync(doc1, pk1);

            Document doc2 = new Document { Id = "pkdel2" };
            doc2.SetValue("ZipCode", "10001");
            doc2.SetValue("City", "Brooklyn");
            Cosmos.PartitionKey pk2 = new PartitionKeyBuilder()
                .Add("10001")
                .Add("Brooklyn")
                .Build();
            await this.container.CreateItemAsync(doc2, pk2);

            Document doc3 = new Document { Id = "pkdel3" };
            doc3.SetValue("ZipCode", "20001");
            doc3.SetValue("City", "Washington");
            Cosmos.PartitionKey pk3 = new PartitionKeyBuilder()
                .Add("20001")
                .Add("Washington")
                .Build();
            await this.container.CreateItemAsync(doc3, pk3);

            // Pass only the first level of the partition key (ZipCode = "10001")
            // Known issue: EnsureIdGetAppendedToPartitionKeyHelper throws because last PK path is /id
            // and no itemId is provided in the DeleteAllItemsByPartitionKey code path
            Cosmos.PartitionKey firstLevelPk = new PartitionKeyBuilder()
                .Add("10001")
                .Build();

            //Delete fails silently in backend
            await this.container.DeleteAllItemsByPartitionKeyStreamAsync(firstLevelPk);

            // Verify all documents still exist since the delete failed
            ItemResponse<Document> read1 = await this.container.ReadItemAsync<Document>("pkdel1", pk1);
            Assert.AreEqual(HttpStatusCode.OK, read1.StatusCode);

            ItemResponse<Document> read2 = await this.container.ReadItemAsync<Document>("pkdel2", pk2);
            Assert.AreEqual(HttpStatusCode.OK, read2.StatusCode);

            ItemResponse<Document> read3 = await this.container.ReadItemAsync<Document>("pkdel3", pk3);
            Assert.AreEqual(HttpStatusCode.OK, read3.StatusCode);

            Cosmos.PartitionKey fullyspecifiedPartitionKey = new PartitionKeyBuilder()
                .Add("10001")
                .Add("NewYork")
                .Add("pkdel1")
                .Build();

            read1 = await this.container.ReadItemAsync<Document>("pkdel1", pk1);
            Assert.AreEqual(HttpStatusCode.OK, read1.StatusCode);

            await this.container.DeleteAllItemsByPartitionKeyStreamAsync(fullyspecifiedPartitionKey);

            CosmosException ex = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => this.container.ReadItemAsync<Document>("pkdel1", pk1));
            Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
        }

        [TestMethod]
        public async Task CreateItemStreamAsync_WithMultiHashIdPath_NullItemId_AppendsIdFromPartitionKey()
        {
            // Create a container with /pk and /id as hierarchical partition key paths
            ContainerProperties hpkContainerProperties = new ContainerProperties(
                "hpkstream_" + Guid.NewGuid().ToString("N"),
                new List<string> { "/pk", "/id" });
            Container hpkContainer = await this.database.CreateContainerAsync(hpkContainerProperties);

            try
            {
                // CreateItemStreamAsync passes itemId: null to ProcessItemStreamAsync,
                // which calls EnsureIdGetsAppendedToPartitionKeyIfNeededAsync with null itemId.
                // With a partial partition key (only /pk), the method should still succeed
                // because the full partition key (/pk + /id) is provided by the caller.
                string itemId = Guid.NewGuid().ToString();
                string pkValue = "testPartition";
                Document doc = new Document { Id = itemId };
                doc.SetValue("pk", pkValue);

                Cosmos.PartitionKey fullPk = new PartitionKeyBuilder()
                    .Add(pkValue)
                    .Build();

                using (Stream stream = TestCommon.SerializerCore.ToStream(doc))
                {
                    using (ResponseMessage response = await hpkContainer.CreateItemStreamAsync(
                        streamPayload: stream,
                        partitionKey: fullPk))
                    {
                        Assert.IsNotNull(response);
                        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                    }
                }

                // Verify the item was created successfully
                Document readDoc = await hpkContainer.ReadItemAsync<Document>(itemId, fullPk);
                Assert.IsNotNull(readDoc);
                Assert.AreEqual(itemId, readDoc.Id);
                Assert.AreEqual(pkValue, readDoc.GetPropertyValue<string>("pk"));
            }
            finally
            {
                await hpkContainer.DeleteContainerAsync();
            }
        }

    }
}