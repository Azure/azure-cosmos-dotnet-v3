//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using JsonWriter = Json.JsonWriter;
    using JsonReader = Json.JsonReader;

    [TestClass]
    public class EncryptionTests
    {
        private static EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata("metadata1");
        private static EncryptionKeyWrapMetadata metadata2 = new EncryptionKeyWrapMetadata("metadata2");
        private const string metadataUpdateSuffix = "updated";
        private static TimeSpan cacheTTL = TimeSpan.FromDays(1);

        private const string dekId = "mydek";

        private static CosmosClient client;

        private static DatabaseCore databaseCore;
        private static DataEncryptionKeyProperties dekProperties;
        private static ContainerCore containerCore;
        private static Container container;

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            EncryptionTests.client = EncryptionTests.GetClient();
            EncryptionTests.databaseCore = (DatabaseInlineCore)await EncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            EncryptionTests.container = await EncryptionTests.databaseCore.CreateContainerAsync(Guid.NewGuid().ToString(), "/PK", 400);
            EncryptionTests.containerCore = (ContainerInlineCore)EncryptionTests.container;
            EncryptionTests.dekProperties = await CreateDekAsync(EncryptionTests.databaseCore, EncryptionTests.dekId);
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            if (EncryptionTests.databaseCore != null)
            {
                using (await EncryptionTests.databaseCore.DeleteStreamAsync()) { }
            }

            if (EncryptionTests.client != null)
            {
                EncryptionTests.client.Dispose();
            }
        }

        [TestMethod]
        public async Task EncryptionCreateDek()
        {
            string dekId = "anotherDek";
            DataEncryptionKeyProperties dekProperties = await EncryptionTests.CreateDekAsync(EncryptionTests.databaseCore, dekId);
            Assert.IsNotNull(dekProperties);
            Assert.IsNotNull(dekProperties.CreatedTime);
            Assert.IsNotNull(dekProperties.LastModified);
            Assert.IsNotNull(dekProperties.SelfLink);
            Assert.IsNotNull(dekProperties.ResourceId);

            // Assert.AreEqual(dekProperties.LastModified, dekProperties.CreatedTime);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(EncryptionTests.metadata1.Value + EncryptionTests.metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            // Use a different client instance to avoid (unintentional) cache impact
            using (CosmosClient client = EncryptionTests.GetClient())
            {
                DataEncryptionKeyProperties readProperties =
                    await ((DatabaseCore)(DatabaseInlineCore)client.GetDatabase(EncryptionTests.databaseCore.Id)).GetDataEncryptionKey(dekId).ReadAsync();
                Assert.AreEqual(dekProperties, readProperties);
            }
        }

        [TestMethod]
        public async Task EncryptionDekReadFeed()
        {
            DatabaseCore databaseCore = null;
            try
            {

                databaseCore = (DatabaseInlineCore)await EncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());
                ContainerCore containerCore = (ContainerInlineCore)await EncryptionTests.databaseCore.CreateContainerAsync(Guid.NewGuid().ToString(), "/PK", 400);

                string contosoV1 = "Contoso_v001";
                string contosoV2 = "Contoso_v002";
                string fabrikamV1 = "Fabrikam_v001";
                string fabrikamV2 = "Fabrikam_v002";

                await EncryptionTests.CreateDekAsync(databaseCore, contosoV1);
                await EncryptionTests.CreateDekAsync(databaseCore, contosoV2);
                await EncryptionTests.CreateDekAsync(databaseCore, fabrikamV1);
                await EncryptionTests.CreateDekAsync(databaseCore, fabrikamV2);

                // Test getting all keys
                await EncryptionTests.IterateDekFeedAsync(
                    databaseCore,
                    new List<string> { contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: false);

                // Test getting specific subset of keys
                await EncryptionTests.IterateDekFeedAsync(
                    databaseCore,
                    new List<string> { contosoV2 },
                    isExpectedDeksCompleteSetForRequest: false,
                    isResultOrderExpected: true,
                    startId: "Contoso_v000",
                    endId: "Contoso_v999",
                    isDescending: true,
                    itemCountInPage: 1);

                // Ensure only required results are returned (ascending)
                await EncryptionTests.IterateDekFeedAsync(
                    databaseCore,
                    new List<string> { contosoV1, contosoV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: true,
                    startId: "Contoso_v000",
                    endId: "Contoso_v999",
                    isDescending: false);

                // Test startId inclusive and endId inclusive (ascending)
                await EncryptionTests.IterateDekFeedAsync(
                    databaseCore,
                    new List<string> { contosoV1, contosoV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: true,
                    startId: "Contoso_v001",
                    endId: "Contoso_v002",
                    isDescending: false);

                // Ensure only required results are returned (descending)
                await EncryptionTests.IterateDekFeedAsync(
                    databaseCore,
                    new List<string> { contosoV2, contosoV1 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: true,
                    startId: "Contoso_v000",
                    endId: "Contoso_v999",
                    isDescending: true);

                // Test startId inclusive and endId inclusive (descending)
                await EncryptionTests.IterateDekFeedAsync(
                    databaseCore,
                    new List<string> { contosoV2, contosoV1 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: true,
                    startId: "Contoso_v001",
                    endId: "Contoso_v002",
                    isDescending: true);

                // Test pagination
                await EncryptionTests.IterateDekFeedAsync(
                    databaseCore,
                    new List<string> { contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: false,
                    itemCountInPage: 3);
            }
            finally
            {
                if(databaseCore != null)
                {
                    await databaseCore.DeleteStreamAsync();
                }
            }
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithoutEncryptionOptions()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await EncryptionTests.containerCore.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
        }

        [TestMethod]
        public async Task EncryptionCreateItem()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(EncryptionTests.containerCore, EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.containerCore, testDoc);

            await EncryptionTests.VerifyItemByReadStreamAsync(EncryptionTests.containerCore, testDoc);

            TestDoc expectedDoc = new TestDoc(testDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.containerCore,
                "SELECT * FROM c",
                expectedDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.containerCore,
                string.Format(
                    "SELECT * FROM c where c.PK = '{0}' and c.id = '{1}' and c.NonSensitive = '{2}'",
                    expectedDoc.PK,
                    expectedDoc.Id,
                    expectedDoc.NonSensitive),
                expectedDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.containerCore,
                string.Format("SELECT * FROM c where c.Sensitive = '{0}'", testDoc.Sensitive),
                expectedDoc: null);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.containerCore,
                queryDefinition: new QueryDefinition(
                    "select * from c where c.id = @theId and c.PK = @thePK")
                         .WithParameter("@theId", expectedDoc.Id)
                         .WithParameter("@thePK", expectedDoc.PK),
                expectedDoc: expectedDoc);

            expectedDoc.Sensitive = null;

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.containerCore,
                "SELECT c.id, c.PK, c.Sensitive, c.NonSensitive FROM c",
                expectedDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.containerCore,
                "SELECT c.id, c.PK, c.NonSensitive FROM c",
                expectedDoc);

            await EncryptionTests.ValidateSprocResultsAsync(
                EncryptionTests.containerCore,
                expectedDoc);
        }

        [TestMethod]
        public async Task DecryptQueryResultMultipleDocsTest()
        {
            TestDoc testDoc1 = await EncryptionTests.CreateItemAsync(EncryptionTests.containerCore, EncryptionTests.dekId, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await EncryptionTests.CreateItemAsync(EncryptionTests.containerCore, EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            await ValidateQueryResultsMultipleDocumentsAsync(EncryptionTests.containerCore, testDoc1, testDoc2);
        }

        [TestMethod]
        public async Task DecryptQueryResultDifferentDeksTest()
        {
            string dekId1 = "mydek1";
            EncryptionTests.dekProperties = await CreateDekAsync(EncryptionTests.databaseCore, dekId1);

            TestDoc testDoc1 = await EncryptionTests.CreateItemAsync(EncryptionTests.containerCore, EncryptionTests.dekId, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await EncryptionTests.CreateItemAsync(EncryptionTests.containerCore, dekId1, TestDoc.PathsToEncrypt);

            await ValidateQueryResultsMultipleDocumentsAsync(EncryptionTests.containerCore, testDoc1, testDoc2);
        }

        [TestMethod]
        public async Task DecryptQueryResultMultipleEncryptedPropertiesTest()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(
                EncryptionTests.containerCore,
                EncryptionTests.dekId,
                new List<string>(){ "/Sensitive", "/NonSensitive" });

            TestDoc expectedDoc = new TestDoc(testDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.containerCore,
                "SELECT * FROM c",
                expectedDoc);
        }

        [TestMethod]
        public async Task DecryptQueryBinaryResponse()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(EncryptionTests.containerCore, EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            CosmosSerializationFormatOptions options = new CosmosSerializationFormatOptions(
                Documents.ContentSerializationFormat.CosmosBinary.ToString(),
                (content) => JsonNavigator.Create(content),
                () => JsonWriter.Create(JsonSerializationFormat.Binary));

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                CosmosSerializationFormatOptions = options
            };

            TestDoc expectedDoc = new TestDoc(testDoc);

            string query = "SELECT * FROM c";

            FeedIterator feedIterator = EncryptionTests.containerCore.GetItemQueryStreamIterator(
                query,
                requestOptions: requestOptions);

            while (feedIterator.HasMoreResults)
            {
                ResponseMessage response = await feedIterator.ReadNextAsync();
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.IsNull(response.ErrorMessage);

                // Copy the stream and check that the first byte is the correct value
                MemoryStream memoryStream = new MemoryStream();
                response.Content.CopyTo(memoryStream);
                byte[] content = memoryStream.ToArray();
                response.Content.Position = 0;

                // Examine the first buffer byte to determine the serialization format
                byte firstByte = content[0];
                Assert.AreEqual(128, firstByte);
                Assert.AreEqual(JsonSerializationFormat.Binary, (JsonSerializationFormat)firstByte);

                IJsonReader reader = JsonReader.Create(content);
                IJsonWriter textWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                textWriter.WriteAll(reader);
                string json = Encoding.UTF8.GetString(textWriter.GetResult().ToArray());
                Assert.IsNotNull(json);
                Assert.IsTrue(json.Contains(testDoc.Sensitive));
            }
        }

        [TestMethod]
        public async Task EncryptionRudItem()
        {
            TestDoc testDoc = await EncryptionTests.UpsertItemAsync(
                EncryptionTests.containerCore,
                TestDoc.Create(),
                EncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.Created);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.containerCore, testDoc);

            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            ItemResponse<TestDoc> upsertResponse = await EncryptionTests.UpsertItemAsync(
                EncryptionTests.containerCore,
                testDoc,
                EncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.OK);
            TestDoc updatedDoc = upsertResponse.Resource;

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.containerCore, updatedDoc);

            updatedDoc.NonSensitive = Guid.NewGuid().ToString();
            updatedDoc.Sensitive = Guid.NewGuid().ToString();

            TestDoc replacedDoc = await EncryptionTests.ReplaceItemAsync(
                EncryptionTests.containerCore,
                updatedDoc,
                EncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                upsertResponse.ETag);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.containerCore, replacedDoc);

            await EncryptionTests.DeleteItemAsync(EncryptionTests.containerCore, replacedDoc);
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuth()
        {
            User user = EncryptionTests.databaseCore.GetUser(Guid.NewGuid().ToString());
            await EncryptionTests.databaseCore.CreateUserAsync(user.Id);

            PermissionProperties permission = await user.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, EncryptionTests.container));

            TestDoc testDoc = await EncryptionTests.CreateItemAsync(EncryptionTests.containerCore, EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            (string endpoint, string _) = TestCommon.GetAccountInfo();
            CosmosClient resourceTokenBasedClient = new CosmosClientBuilder(endpoint, permission.Token)
                .WithEncryptionKeyWrapProvider(new TestKeyWrapProvider())
                .Build();

            DatabaseCore databaseForTokenClient = (DatabaseInlineCore)resourceTokenBasedClient.GetDatabase(EncryptionTests.databaseCore.Id);
            Container containerForTokenClient = databaseForTokenClient.GetContainer(EncryptionTests.container.Id);

            await EncryptionTests.PerformForbiddenOperationAsync(() =>
                databaseForTokenClient.GetDataEncryptionKey(EncryptionTests.dekId).ReadAsync(), "DEK.ReadAsync");

            await EncryptionTests.PerformForbiddenOperationAsync(() =>
                containerForTokenClient.ReadItemAsync<TestDoc>(testDoc.Id, new PartitionKey(testDoc.PK)), "ReadItemAsync");

            await EncryptionTests.PerformForbiddenOperationAsync(() =>
                containerForTokenClient.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK)), "ReadItemStreamAsync");
        }

        [TestMethod]
        public async Task EncryptionRestrictedProperties()
        {
            TestDoc testDoc = TestDoc.Create();

            try
            {
                await EncryptionTests.CreateItemAsync(EncryptionTests.containerCore, EncryptionTests.dekId, new List<string>() { "/id" });
                Assert.Fail("Expected item creation with id specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }

            try
            {
                await EncryptionTests.CreateItemAsync(EncryptionTests.containerCore, EncryptionTests.dekId, new List<string>() { "/PK" });
                Assert.Fail("Expected item creation with PK specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }
        }

        [TestMethod]
        public async Task EncryptionBulkCrud()
        {
            TestDoc docToReplace = await EncryptionTests.CreateItemAsync(EncryptionTests.containerCore, EncryptionTests.dekId, TestDoc.PathsToEncrypt);
            docToReplace.NonSensitive = Guid.NewGuid().ToString();
            docToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToUpsert = await EncryptionTests.CreateItemAsync(EncryptionTests.containerCore, EncryptionTests.dekId, TestDoc.PathsToEncrypt);
            docToUpsert.NonSensitive = Guid.NewGuid().ToString();
            docToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await EncryptionTests.CreateItemAsync(EncryptionTests.containerCore, EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            CosmosClient clientWithBulk = new CosmosClientBuilder(endpoint, authKey)
                .WithEncryptionKeyWrapProvider(new TestKeyWrapProvider())
                .WithBulkExecution(true)
                .Build();

            DatabaseCore databaseWithBulk = (DatabaseInlineCore)clientWithBulk.GetDatabase(EncryptionTests.databaseCore.Id);
            ContainerCore containerWithBulk = (ContainerInlineCore)databaseWithBulk.GetContainer(EncryptionTests.container.Id);

            List<Task> tasks = new List<Task>();
            tasks.Add(EncryptionTests.CreateItemAsync(containerWithBulk, EncryptionTests.dekId, TestDoc.PathsToEncrypt));
            tasks.Add(EncryptionTests.UpsertItemAsync(containerWithBulk, TestDoc.Create(), EncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.Created));
            tasks.Add(EncryptionTests.ReplaceItemAsync(containerWithBulk, docToReplace, EncryptionTests.dekId, TestDoc.PathsToEncrypt));
            tasks.Add(EncryptionTests.UpsertItemAsync(containerWithBulk, docToUpsert, EncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.OK));
            tasks.Add(EncryptionTests.DeleteItemAsync(containerWithBulk, docToDelete));
            await Task.WhenAll(tasks);
        }

        private static async Task ValidateSprocResultsAsync(ContainerCore containerCore, TestDoc expectedDoc)
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(docId) {
                var context = getContext();
                var collection = context.getCollection();
                var docUri =  collection.getAltLink() + '/docs/' + docId;
                var response = context.getResponse();

                collection.readDocument(docUri, { },
                    function(error, resource, options) {
                        response.setBody(resource);
                    });
            }";

            StoredProcedureResponse storedProcedureResponse =
                await containerCore.Scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);

            StoredProcedureExecuteResponse<TestDoc> sprocResponse = await containerCore.Scripts.ExecuteStoredProcedureAsync<TestDoc>(
                sprocId,
                new PartitionKey(expectedDoc.PK),
                parameters: new dynamic[] { expectedDoc.Id });

            Assert.AreEqual(expectedDoc, sprocResponse.Resource);
        }

        // One of query or queryDefinition is to be passed in non-null
        private static async Task ValidateQueryResultsAsync(
            ContainerCore containerCore,
            string query = null,
            TestDoc expectedDoc = null,
            QueryDefinition queryDefinition = null)
        {
            QueryRequestOptions requestOptions = expectedDoc != null ? new QueryRequestOptions() { PartitionKey = new PartitionKey(expectedDoc.PK) } : null;
            FeedIterator<TestDoc> queryResponseIterator;
            if (query != null)
            {
                queryResponseIterator = containerCore.GetItemQueryIterator<TestDoc>(query, requestOptions: requestOptions);
            }
            else
            {
                queryResponseIterator = containerCore.GetItemQueryIterator<TestDoc>(queryDefinition, requestOptions: requestOptions);
            }

            FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();
            Assert.AreEqual(null, readDocs.ContinuationToken);

            if (expectedDoc != null)
            {
                Assert.AreEqual(1, readDocs.Count);
                TestDoc readDoc = readDocs.Single();
                Assert.AreEqual(expectedDoc, readDoc);
            }
            else
            {
                Assert.AreEqual(0, readDocs.Count);
            }
        }

        private static async Task ValidateQueryResultsMultipleDocumentsAsync(
            ContainerCore containerCore,
            TestDoc testDoc1,
            TestDoc testDoc2)
        {
            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";
            FeedIterator<TestDoc> queryResponseIterator = containerCore.GetItemQueryIterator<TestDoc>(query);
            FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();
            Assert.AreEqual(null, readDocs.ContinuationToken);
            Assert.AreEqual(2, readDocs.Count);
            foreach (TestDoc readDoc in readDocs)
            {
                Assert.AreEqual(readDoc, readDoc.Id.Equals(testDoc1.Id) ? testDoc1 : testDoc2);
            }
        }

        private static CosmosClient GetClient()
        {
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            return new CosmosClientBuilder(endpoint, authKey)
                .WithEncryptionKeyWrapProvider(new TestKeyWrapProvider())
                .Build();
        }

        private static async Task IterateDekFeedAsync(
            DatabaseCore databaseCore,
            List<string> expectedDekIds,
            bool isExpectedDeksCompleteSetForRequest,
            bool isResultOrderExpected,
            string startId = null,
            string endId = null,
            bool isDescending = false,
            int? itemCountInPage = null)
        {
            int remainingItemCount = expectedDekIds.Count;
            QueryRequestOptions options = null;
            if (itemCountInPage.HasValue)
            {
                options = new QueryRequestOptions()
                {
                    MaxItemCount = itemCountInPage
                };
            }

            FeedIterator<DataEncryptionKeyProperties> dekIterator = databaseCore.GetDataEncryptionKeyIterator(
                startId, endId, isDescending, requestOptions: options);

            Assert.IsTrue(dekIterator.HasMoreResults);

            List<string> readDekIds = new List<string>();
            while (remainingItemCount > 0)
            {
                FeedResponse<DataEncryptionKeyProperties> page = await dekIterator.ReadNextAsync();
                if (itemCountInPage.HasValue)
                {
                    // last page
                    if (remainingItemCount < itemCountInPage.Value)
                    {
                        Assert.AreEqual(remainingItemCount, page.Count);
                    }
                    else
                    {
                        Assert.AreEqual(itemCountInPage.Value, page.Count);
                    }
                }
                else
                {
                    Assert.AreEqual(expectedDekIds.Count, page.Count);
                }

                remainingItemCount -= page.Count;
                if (isExpectedDeksCompleteSetForRequest)
                {
                    Assert.AreEqual(remainingItemCount > 0, dekIterator.HasMoreResults);
                }

                foreach (DataEncryptionKeyProperties dek in page.Resource)
                {
                    readDekIds.Add(dek.Id);
                }
            }

            if (isResultOrderExpected)
            {
                Assert.IsTrue(expectedDekIds.SequenceEqual(readDekIds));
            }
            else
            {
                Assert.IsTrue(expectedDekIds.ToHashSet().SetEquals(readDekIds));
            }
        }

        private static async Task<ItemResponse<TestDoc>> UpsertItemAsync(
            ContainerCore containerCore,
            TestDoc testDoc,
            string dekId,
            List<string> pathsToEncrypt,
            HttpStatusCode expectedStatusCode)
        {
            ItemResponse<TestDoc> upsertResponse = await containerCore.UpsertItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                EncryptionTests.GetRequestOptions(containerCore, dekId, pathsToEncrypt));
            Assert.AreEqual(expectedStatusCode, upsertResponse.StatusCode);
            Assert.AreEqual(testDoc, upsertResponse.Resource);
            return upsertResponse;
        }

        private static async Task<ItemResponse<TestDoc>> CreateItemAsync(
            ContainerCore containerCore,
            string dekId,
            List<string> pathsToEncrypt)
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await containerCore.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                EncryptionTests.GetRequestOptions(containerCore, dekId, pathsToEncrypt));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
            return createResponse;
        }

        private static async Task<ItemResponse<TestDoc>> ReplaceItemAsync(
            ContainerCore containerCore,
            TestDoc testDoc,
            string dekId,
            List<string> pathsToEncrypt,
            string etag = null)
        {
            ItemResponse<TestDoc> replaceResponse = await containerCore.ReplaceItemAsync(
                testDoc,
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                EncryptionTests.GetRequestOptions(containerCore, dekId, pathsToEncrypt, etag));

            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            Assert.AreEqual(testDoc, replaceResponse.Resource);
            return replaceResponse;
        }

        private static async Task<ItemResponse<TestDoc>> DeleteItemAsync(
            ContainerCore containerCore,
            TestDoc testDoc)
        {
            ItemResponse<TestDoc> deleteResponse = await containerCore.DeleteItemAsync<TestDoc>(
                testDoc.Id,
                new PartitionKey(testDoc.PK));

            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
            Assert.IsNull(deleteResponse.Resource);
            return deleteResponse;
        }

        private static ItemRequestOptions GetRequestOptions(
            ContainerCore containerCore,
            string dekId,
            List<string> pathsToEncrypt,
            string ifMatchEtag = null)
        {
            return new ItemRequestOptions
            {
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKey = ((DatabaseCore)containerCore.Database).GetDataEncryptionKey(dekId),
                    PathsToEncrypt = pathsToEncrypt
                },
                IfMatchEtag = ifMatchEtag
            };
        }

        private static async Task VerifyItemByReadStreamAsync(Container container, TestDoc testDoc)
        {
            ResponseMessage readResponseMessage = await container.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.OK, readResponseMessage.StatusCode);
            Assert.IsNotNull(readResponseMessage.Content);
            TestDoc readDoc = TestCommon.SerializerCore.FromStream<TestDoc>(readResponseMessage.Content);
            Assert.AreEqual(testDoc, readDoc);
        }

        private static async Task VerifyItemByReadAsync(Container container, TestDoc testDoc)
        {
            ItemResponse<TestDoc> readResponse = await container.ReadItemAsync<TestDoc>(testDoc.Id, new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.AreEqual(testDoc, readResponse.Resource);
        }

        private static async Task<DataEncryptionKeyProperties> CreateDekAsync(DatabaseCore databaseCore, string dekId)
        {
            DataEncryptionKeyResponse dekResponse = await databaseCore.CreateDataEncryptionKeyAsync(
                dekId,
                CosmosEncryptionAlgorithm.AE_AES_256_CBC_HMAC_SHA_256_RANDOMIZED,
                EncryptionTests.metadata1);

            Assert.AreEqual(HttpStatusCode.Created, dekResponse.StatusCode);
            Assert.IsTrue(dekResponse.RequestCharge > 0);
            Assert.IsNotNull(dekResponse.ETag);

            DataEncryptionKeyProperties dekProperties = dekResponse.Resource;
            Assert.AreEqual(dekResponse.ETag, dekProperties.ETag);
            Assert.AreEqual(dekId, dekProperties.Id);
            return dekProperties;
        }

        private static async Task PerformForbiddenOperationAsync<T>(Func<Task<T>> func, string operationName)
        {
            try
            {
                await func();
                Assert.Fail($"Expected resource token based client to not be able to perform {operationName}");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
            }
        }

        public class TestDoc
        {
            public static List<string> PathsToEncrypt { get; } = new List<string>() { "/Sensitive" };

            [JsonProperty("id")]
            public string Id { get; set; }

            public string PK { get; set; }

            public string NonSensitive { get; set; }

            public string Sensitive { get; set; }

            public TestDoc()
            {
            }

            public TestDoc(TestDoc other)
            {
                this.Id = other.Id;
                this.PK = other.PK;
                this.NonSensitive = other.NonSensitive;
                this.Sensitive = other.Sensitive;
            }

            public override bool Equals(object obj)
            {
                return obj is TestDoc doc
                       && this.Id == doc.Id
                       && this.PK == doc.PK
                       && this.NonSensitive == doc.NonSensitive
                       && this.Sensitive == doc.Sensitive;
            }

            public override int GetHashCode()
            {
                int hashCode = 1652434776;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Id);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.PK);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.NonSensitive);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Sensitive);
                return hashCode;
            }

            public static TestDoc Create()
            {
                return new TestDoc()
                {
                    Id = Guid.NewGuid().ToString(),
                    PK = Guid.NewGuid().ToString(),
                    NonSensitive = Guid.NewGuid().ToString(),
                    Sensitive = Guid.NewGuid().ToString()
                };
            }
        }

        private class TestKeyWrapProvider : EncryptionKeyWrapProvider
        {
            public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(byte[] wrappedKey, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken)
            {
                int moveBy = metadata.Value == EncryptionTests.metadata1.Value + EncryptionTests.metadataUpdateSuffix ? 1 : 2;
                return Task.FromResult(new EncryptionKeyUnwrapResult(wrappedKey.Select(b => (byte)(b - moveBy)).ToArray(), EncryptionTests.cacheTTL));
            }

            public override Task<EncryptionKeyWrapResult> WrapKeyAsync(byte[] key, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken)
            {
                EncryptionKeyWrapMetadata responseMetadata = new EncryptionKeyWrapMetadata(metadata.Value + EncryptionTests.metadataUpdateSuffix);
                int moveBy = metadata.Value == EncryptionTests.metadata1.Value ? 1 : 2;
                return Task.FromResult(new EncryptionKeyWrapResult(key.Select(b => (byte)(b + moveBy)).ToArray(), responseMetadata));
            }
        }
    }
}
