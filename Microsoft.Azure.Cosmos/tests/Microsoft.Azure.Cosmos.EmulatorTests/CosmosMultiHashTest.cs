namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos;
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

            this.containerProperties = new ContainerProperties("mycoll", new List<string> { "/ZipCode", "/City" });
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
                    new QueryRequestOptions() { PartitionKey = pKey }))
                {
                    Assert.IsTrue(feedIterator.HasMoreResults);

                    FeedResponse<Document> queryDoc = await feedIterator.ReadNextAsync();
                    queryDoc.First<Document>();
                    Assert.IsTrue(queryDoc.Count == 1);
                    feedIterator.Dispose();
                }

                //Using an incomplete partition key with prefix of PK path definition
                pKey = new PartitionKeyBuilder()
                    .Add(document.GetPropertyValue<string>("ZipCode"))
                .Build();
                using (FeedIterator<Document> feedIterator = this.container.GetItemQueryIterator<Document>(
                    query,
                    null,
                    new QueryRequestOptions() { PartitionKey = pKey }))
                {
                    Assert.IsTrue(feedIterator.HasMoreResults);

                    FeedResponse<Document> queryDoc = await feedIterator.ReadNextAsync();
                    queryDoc.First<Document>();
                    Assert.IsTrue(queryDoc.Count == 1);
                    feedIterator.Dispose();
                }

                //Negative test - using incomplete partition key
                using (FeedIterator<Document> badFeedIterator = this.container.GetItemQueryIterator<Document>(
                    query,
                    null,
                    new QueryRequestOptions() { PartitionKey = badPKey}))
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
}