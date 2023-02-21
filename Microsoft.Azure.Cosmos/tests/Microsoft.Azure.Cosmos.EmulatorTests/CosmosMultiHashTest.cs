#if PREVIEW
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
        private Cosmos.Database database = null;

        private CosmosClient client = null;
        private Container container = null;
        private ContainerProperties containerProperties = null;

        private readonly string currentVersion = HttpConstants.Versions.CurrentVersion;


        [TestInitialize]
        public async Task TestInitialize()
        {
            HttpConstants.Versions.CurrentVersion = "2020-07-15";
            this.client = TestCommon.CreateCosmosClient(true);
            this.database = await this.client.CreateDatabaseIfNotExistsAsync("mydb");

            this.containerProperties = new ContainerProperties("mycoll", new List<string> { "/ZipCode", "/Address" });
            this.container = await this.database.CreateContainerAsync(this.containerProperties);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await this.database.DeleteAsync();
            HttpConstants.Versions.CurrentVersion = this.currentVersion;
            this.client.Dispose();
        }

        [TestMethod]
        public async Task MultiHashCreateDocumentTest()
        {
            //Document create test
            ItemResponse<Document>[] documents = new ItemResponse<Document>[3];
            Document doc1 = new Document { Id = "document1" };
            doc1.SetValue("ZipCode", "500026");
            doc1.SetValue("Address", "Secunderabad");
            doc1.SetValue("Type", "Residence");
            documents[0] = await this.container.CreateItemAsync<Document>(doc1);

            doc1 = new Document { Id = "document2" };
            doc1.SetValue("ZipCode", "15232");
            doc1.SetValue("Address", "Pittsburgh");
            doc1.SetValue("Type", "Business");
            documents[1] = await this.container.CreateItemAsync<Document>(doc1);

            doc1 = new Document { Id = "document3" };
            doc1.SetValue("ZipCode", "11790");
            doc1.SetValue("Address", "Stonybrook");
            doc1.SetValue("Type", "Goverment");
            documents[2] = await this.container.CreateItemAsync<Document>(doc1);

            Assert.AreEqual(3, documents.Select(document => ((Document)document).SelfLink).Distinct().Count());

            //Negative test - using incomplete partition key
            Cosmos.PartitionKey badPKey;

            foreach (Document document in documents)
            {
                badPKey = new PartitionKeyBuilder()
                            .Add(document.GetPropertyValue<string>("Address"))
                            .Build();

                document.Id += "Bad";

                ArgumentException createException = await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                        this.container.CreateItemAsync<Document>(document, badPKey)
                    );
            }
        }

        [TestMethod]
        public async Task MultiHashDeleteDocumentTest()
        {
            Cosmos.PartitionKey pKey;
            Cosmos.PartitionKey badPKey;

            //Create Items for test
            ItemResponse<Document>[] documents = new ItemResponse<Document>[3];
            Document doc1 = new Document { Id = "document1" };
            doc1.SetValue("ZipCode", "500026");
            doc1.SetValue("Address", "Secunderabad");
            doc1.SetValue("Type", "Residence");
            documents[0] = await this.container.CreateItemAsync<Document>(doc1);

            doc1 = new Document { Id = "document2" };
            doc1.SetValue("ZipCode", "15232");
            doc1.SetValue("Address", "Pittsburgh");
            doc1.SetValue("Type", "Business");
            documents[1] = await this.container.CreateItemAsync<Document>(doc1);

            doc1 = new Document { Id = "document3" };
            doc1.SetValue("ZipCode", "11790");
            doc1.SetValue("Address", "Stonybrook");
            doc1.SetValue("Type", "Goverment");
            documents[2] = await this.container.CreateItemAsync<Document>(doc1);

            //Document Delete Test
            foreach (Document document in documents)
            {
                //Negative test - using incomplete partition key
                badPKey = new PartitionKeyBuilder()
                        .Add(document.GetPropertyValue<string>("Address"))
                        .Build();

                CosmosException deleteException = await Assert.ThrowsExceptionAsync<CosmosException>(() =>
                    this.container.DeleteItemAsync<Document>(document.Id, badPKey)
                );

                Assert.AreEqual(deleteException.StatusCode, HttpStatusCode.BadRequest);

                //Positive test
                pKey = new PartitionKeyBuilder()
                    .Add(document.GetPropertyValue<string>("ZipCode"))
                    .Add(document.GetPropertyValue<string>("Address"))
                    .Build();

                Document readDocument = (await this.container.DeleteItemAsync<Document>(document.Id, pKey)).Resource;

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
            Document doc1 = new Document { Id = "document1" };
            doc1.SetValue("ZipCode", "500026");
            doc1.SetValue("Address", "Secunderabad");
            doc1.SetValue("Type", "Residence");
            documents[0] = await this.container.CreateItemAsync<Document>(doc1);

            doc1 = new Document { Id = "document2" };
            doc1.SetValue("ZipCode", "15232");
            doc1.SetValue("Address", "Pittsburgh");
            doc1.SetValue("Type", "Business");
            documents[1] = await this.container.CreateItemAsync<Document>(doc1);

            doc1 = new Document { Id = "document3" };
            doc1.SetValue("ZipCode", "11790");
            doc1.SetValue("Address", "Stonybrook");
            doc1.SetValue("Type", "Goverment");
            documents[2] = await this.container.CreateItemAsync<Document>(doc1);

            //Document Read Test
            foreach (Document document in documents)
            {
                pKey = new PartitionKeyBuilder()
                    .Add(document.GetPropertyValue<string>("ZipCode"))
                    .Add(document.GetPropertyValue<string>("Address"))
                    .Build();

                Document readDocument = (await this.container.ReadItemAsync<Document>(document.Id, pKey)).Resource;
                Assert.AreEqual(document.ToString(), readDocument.ToString());

                //Negative test - using incomplete partition key
                badPKey = new PartitionKeyBuilder()
                        .Add(document.GetPropertyValue<string>("Address"))
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

            //Create Items for test
            ItemResponse<Document>[] documents = new ItemResponse<Document>[3];
            Document doc1 = new Document { Id = "document1" };
            doc1.SetValue("ZipCode", "500026");
            doc1.SetValue("Address", "Secunderabad");
            doc1.SetValue("Type", "Residence");
            documents[0] = await this.container.CreateItemAsync<Document>(doc1);

            doc1 = new Document { Id = "document2" };
            doc1.SetValue("ZipCode", "15232");
            doc1.SetValue("Address", "Pittsburgh");
            doc1.SetValue("Type", "Business");
            documents[1] = await this.container.CreateItemAsync<Document>(doc1);

            doc1 = new Document { Id = "document3" };
            doc1.SetValue("ZipCode", "11790");
            doc1.SetValue("Address", "Stonybrook");
            doc1.SetValue("Type", "Goverment");
            documents[2] = await this.container.CreateItemAsync<Document>(doc1);

            //Read Many Test
            List<(string, Cosmos.PartitionKey)> itemList = new List<(string, Cosmos.PartitionKey)>();
            foreach (Document document in documents)
            {
                pKey = new PartitionKeyBuilder()
                    .Add(document.GetPropertyValue<string>("ZipCode"))
                    .Add(document.GetPropertyValue<string>("Address"))
                    .Build();

                itemList.Add((document.Id, pKey));
            }

            FeedResponse<ToDoActivity> feedResponse = await this.container.ReadManyItemsAsync<ToDoActivity>(itemList);

            Assert.IsNotNull(feedResponse);
            Assert.AreEqual(feedResponse.Count, 3);
            Assert.IsTrue(feedResponse.Headers.RequestCharge > 0);
            Assert.IsNotNull(feedResponse.Diagnostics);

            int count = 0;
            foreach (ToDoActivity item in feedResponse)
            {
                count++;
                Assert.IsNotNull(item);
                Assert.IsNotNull(item.pk);
            }
            Assert.AreEqual(count, 3);
        }

        [TestMethod]
        public async Task MultiHashUpsetItemTest()
        {
            Cosmos.PartitionKey pKey;
            Cosmos.PartitionKey badPKey;
            int count;

            //Create Items for test
            ItemResponse<Document>[] documents = new ItemResponse<Document>[3];
            Document doc1 = new Document { Id = "document1" };
            doc1.SetValue("ZipCode", "500026");
            doc1.SetValue("Address", "Secunderabad");
            doc1.SetValue("Type", "Residence");
            documents[0] = await this.container.CreateItemAsync<Document>(doc1);

            doc1 = new Document { Id = "document2" };
            doc1.SetValue("ZipCode", "15232");
            doc1.SetValue("Address", "Pittsburgh");
            doc1.SetValue("Type", "Business");
            documents[1] = await this.container.CreateItemAsync<Document>(doc1);

            doc1 = new Document { Id = "document3" };
            doc1.SetValue("ZipCode", "11790");
            doc1.SetValue("Address", "Stonybrook");
            doc1.SetValue("Type", "Goverment");
            documents[2] = await this.container.CreateItemAsync<Document>(doc1);

            //Document Upsert Test
            doc1 = new Document { Id = "document4" };
            doc1.SetValue("ZipCode", "97756");
            doc1.SetValue("Address", "Redmond");
            doc1.SetValue("Type", "Residence");

            pKey = new PartitionKeyBuilder()
                    .Add(doc1.GetPropertyValue<string>("ZipCode"))
                    .Add(doc1.GetPropertyValue<string>("Address"))
                .Build();

            //insert check
            await this.container.UpsertItemAsync<Document>(doc1, pKey);

            Document readCheck = (await this.container.ReadItemAsync<Document>(doc1.Id, pKey)).Resource;

            Assert.AreEqual(doc1.GetPropertyValue<string>("ZipCode"), readCheck.GetPropertyValue<string>("ZipCode"));
            Assert.AreEqual(doc1.GetPropertyValue<string>("Address"), readCheck.GetPropertyValue<string>("Address"));
            Assert.AreEqual(doc1.GetPropertyValue<string>("Type"), readCheck.GetPropertyValue<string>("Type"));

            doc1 = new Document { Id = "document4" };
            doc1.SetValue("ZipCode", "97756");
            doc1.SetValue("Address", "Redmond");
            doc1.SetValue("Type", "Business");

            //update check
            pKey = new PartitionKeyBuilder()
                    .Add(doc1.GetPropertyValue<string>("ZipCode"))
                    .Add(doc1.GetPropertyValue<string>("Address"))
                .Build();

            documents.Append<ItemResponse<Document>>(await this.container.UpsertItemAsync<Document>(doc1, pKey));

            readCheck = (await this.container.ReadItemAsync<Document>(doc1.Id, pKey)).Resource;

            Assert.AreEqual(doc1.GetPropertyValue<string>("ZipCode"), readCheck.GetPropertyValue<string>("ZipCode"));
            Assert.AreEqual(doc1.GetPropertyValue<string>("Address"), readCheck.GetPropertyValue<string>("Address"));
            Assert.AreEqual(doc1.GetPropertyValue<string>("Type"), readCheck.GetPropertyValue<string>("Type"));

            count = 0;

            foreach (Document doc in this.container.GetItemLinqQueryable<Document>(true))
            {
                count++;
            }
            Assert.AreEqual(4, count);

            //Negative test - using incomplete partition key
            doc1 = new Document { Id = "document4" };
            doc1.SetValue("ZipCode", "97756");
            doc1.SetValue("Address", "Redmond");
            doc1.SetValue("Type", "Residence");

            badPKey = new PartitionKeyBuilder()
                    .Add(doc1.GetPropertyValue<string>("ZipCode"))
                .Build();

            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                this.container.UpsertItemAsync<Document>(doc1, badPKey)
            );

            readCheck = (await this.container.ReadItemAsync<Document>(doc1.Id, pKey)).Resource;

            Assert.AreEqual(doc1.GetPropertyValue<string>("ZipCode"), readCheck.GetPropertyValue<string>("ZipCode"));
            Assert.AreEqual(doc1.GetPropertyValue<string>("Address"), readCheck.GetPropertyValue<string>("Address"));
            Assert.AreNotEqual(doc1.GetPropertyValue<string>("Type"), readCheck.GetPropertyValue<string>("Type"));
        }

        [TestMethod]
        public async Task MultiHashReplaceItemTest()
        {
            Cosmos.PartitionKey pKey;
            Cosmos.PartitionKey badPKey;

            //Create items for test
            ItemResponse<Document>[] documents = new ItemResponse<Document>[3];
            Document doc1 = new Document { Id = "document1" };
            doc1.SetValue("ZipCode", "500026");
            doc1.SetValue("Address", "Secunderabad");
            doc1.SetValue("Type", "Residence");
            documents[0] = await this.container.CreateItemAsync<Document>(doc1);

            doc1 = new Document { Id = "document2" };
            doc1.SetValue("ZipCode", "15232");
            doc1.SetValue("Address", "Pittsburgh");
            doc1.SetValue("Type", "Business");
            documents[1] = await this.container.CreateItemAsync<Document>(doc1);

            doc1 = new Document { Id = "document3" };
            doc1.SetValue("ZipCode", "11790");
            doc1.SetValue("Address", "Stonybrook");
            doc1.SetValue("Type", "Goverment");
            documents[2] = await this.container.CreateItemAsync<Document>(doc1);

            //Document Replace Test
            foreach (Document document in documents)
            {
                pKey = new PartitionKeyBuilder()
                    .Add(document.GetPropertyValue<string>("ZipCode"))
                    .Add(document.GetPropertyValue<string>("Address"))
                .Build();


                Document readDocument = (await this.container.ReadItemAsync<Document>(document.Id, pKey)).Resource;
                readDocument.SetValue("Type", "Park");

                ItemResponse<Document> item = await this.container.ReplaceItemAsync<Document>(readDocument, readDocument.Id, pKey);

                Document checkDocument = (await this.container.ReadItemAsync<Document>(document.Id, pKey)).Resource;
                Assert.AreEqual(checkDocument.GetPropertyValue<string>("Type"), readDocument.GetPropertyValue<string>("Type"));

                //Negative test - using incomplete partition key
                badPKey = new PartitionKeyBuilder()
                        .Add(document.GetPropertyValue<string>("Address"))
                        .Build();

                readDocument.SetValue("Type", "Goverment");

                await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                    this.container.ReplaceItemAsync<Document>(document, document.Id, partitionKey: badPKey)
                );
            }
        }

        [TestMethod]
        public async Task MultiHashQueryItemTest()
        {
            Cosmos.PartitionKey pKey;

            //Create items for test
            ItemResponse<Document>[] documents = new ItemResponse<Document>[3];
            Document doc1 = new Document { Id = "document1" };
            doc1.SetValue("ZipCode", "500026");
            doc1.SetValue("Address", "Secunderabad");
            doc1.SetValue("Type", "Residence");
            documents[0] = await this.container.CreateItemAsync<Document>(doc1);

            doc1 = new Document { Id = "document2" };
            doc1.SetValue("ZipCode", "15232");
            doc1.SetValue("Address", "Pittsburgh");
            doc1.SetValue("Type", "Business");
            documents[1] = await this.container.CreateItemAsync<Document>(doc1);

            doc1 = new Document { Id = "document3" };
            doc1.SetValue("ZipCode", "11790");
            doc1.SetValue("Address", "Stonybrook");
            doc1.SetValue("Type", "Goverment");
            documents[2] = await this.container.CreateItemAsync<Document>(doc1);

            //Query
            foreach (Document document in documents)
            {
                pKey = new PartitionKeyBuilder()
                    .Add(document.GetPropertyValue<string>("ZipCode"))
                    .Add(document.GetPropertyValue<string>("Address"))
                .Build();

                String query = $"SELECT * from c where c.id = {document.GetPropertyValue<string>("Id")}";

                using (FeedIterator<Document> feedIterator = this.container.GetItemQueryIterator<Document>(
                    query,
                    null,
                    new QueryRequestOptions() { PartitionKey = pKey }))
                {
                    Assert.IsTrue(feedIterator.HasMoreResults);

                    FeedResponse<Document> queryDoc = await feedIterator.ReadNextAsync();
                }

            }
        }

    }
}
#endif
