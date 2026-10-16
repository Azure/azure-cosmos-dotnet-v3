namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

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