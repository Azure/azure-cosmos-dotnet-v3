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
    using Microsoft.Azure.Cosmos.Encryption;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using JsonWriter = Json.JsonWriter;
    using JsonReader = Json.JsonReader;

    [TestClass]
    public class EncryptionTests
    {
        private static EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata("metadata1");
        private const string metadataUpdateSuffix = "updated";
        private static TimeSpan cacheTTL = TimeSpan.FromDays(1);
        private const string dekId = "mydek";
        private static CosmosClient client;
        private static DatabaseInternal databaseCore;
        private static DataEncryptionKeyProperties dekProperties;
        private static ContainerInternal itemContainerCore;
        private static Container itemContainer;
        private static Container keyContainer;
        private static CosmosDataEncryptionKeyProvider dekProvider;
        private static TestEncryptor encryptor;
        private static EncryptionStreamTransformer encryptionStreamTransformerForWrite;
        private static EncryptionStreamTransformer encryptionStreamTransformerForRead;
        private static string decryptionFailedDocId;
        private static readonly CosmosSerializer cosmosSerializer = new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer());

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            EncryptionTests.dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            EncryptionTests.encryptor = new TestEncryptor(EncryptionTests.dekProvider);

            EncryptionTests.client = EncryptionTests.GetClient();
            EncryptionTests.databaseCore = (DatabaseInlineCore)await EncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());

            EncryptionTests.keyContainer = await EncryptionTests.databaseCore.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            await EncryptionTests.dekProvider.InitializeAsync(EncryptionTests.databaseCore, EncryptionTests.keyContainer.Id);


            EncryptionTests.itemContainer = await EncryptionTests.databaseCore.CreateContainerAsync(Guid.NewGuid().ToString(), "/PK", 400);
            EncryptionTests.itemContainerCore = (ContainerInlineCore)EncryptionTests.itemContainer;

            EncryptionTests.dekProperties = await EncryptionTests.CreateDekAsync(EncryptionTests.dekProvider, EncryptionTests.dekId);

            EncryptionTests.encryptionStreamTransformerForWrite = EncryptionTests.GetEncryptionStreamTransformer(
                EncryptionTests.encryptor,
                EncryptionTests.GetEncryptionOptions(dekId, TestDoc.PathsToEncrypt));

            EncryptionTests.encryptionStreamTransformerForRead = EncryptionTests.GetEncryptionStreamTransformer(
                EncryptionTests.encryptor,
                encryptionOptions: null,
                EncryptionTests.ErrorHandler);
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
            DataEncryptionKeyProperties dekProperties = await EncryptionTests.CreateDekAsync(EncryptionTests.dekProvider, dekId);
            Assert.IsNotNull(dekProperties);
            Assert.IsNotNull(dekProperties.CreatedTime);
            Assert.IsNotNull(dekProperties.LastModified);
            Assert.IsNotNull(dekProperties.SelfLink);

            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(EncryptionTests.metadata1.Value + EncryptionTests.metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            // Use different DEK provider to avoid (unintentional) cache impact
            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            await dekProvider.InitializeAsync(EncryptionTests.databaseCore, EncryptionTests.keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        [TestMethod]
        public async Task EncryptionDekReadFeed()
        {
            Container newKeyContainer = await EncryptionTests.databaseCore.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            try
            {
                CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
                await dekProvider.InitializeAsync(EncryptionTests.databaseCore, newKeyContainer.Id);

                string contosoV1 = "Contoso_v001";
                string contosoV2 = "Contoso_v002";
                string fabrikamV1 = "Fabrikam_v001";
                string fabrikamV2 = "Fabrikam_v002";

                await EncryptionTests.CreateDekAsync(dekProvider, contosoV1);
                await EncryptionTests.CreateDekAsync(dekProvider, contosoV2);
                await EncryptionTests.CreateDekAsync(dekProvider, fabrikamV1);
                await EncryptionTests.CreateDekAsync(dekProvider, fabrikamV2);

                // Test getting all keys
                await EncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: false,
                    "SELECT * from c");

                // Test getting specific subset of keys
                await EncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV2 },
                    isExpectedDeksCompleteSetForRequest: false,
                    isResultOrderExpected: true,
                    "SELECT TOP 1 * from c where c.id >= 'Contoso_v000' and c.id <= 'Contoso_v999' ORDER BY c.id DESC");

                // Ensure only required results are returned
                await EncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: true,
                    "SELECT * from c where c.id >= 'Contoso_v000' and c.id <= 'Contoso_v999' ORDER BY c.id ASC");

                // Test pagination
                await EncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: false,
                    "SELECT * from c",
                    itemCountInPage: 3);
            }
            finally
            {
                await newKeyContainer.DeleteContainerStreamAsync();
            }
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithoutEncryptionStreamTransformer()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await EncryptionTests.itemContainerCore.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
        }

        [TestMethod]
        public async Task EncryptionCreateItem()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainerCore, testDoc);

            await EncryptionTests.VerifyItemByReadStreamAsync(EncryptionTests.itemContainerCore, testDoc);

            TestDoc expectedDoc = new TestDoc(testDoc);

            // Read feed (null query)
            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainerCore,
                query: null,
                expectedDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainerCore,
                "SELECT * FROM c",
                expectedDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainerCore,
                string.Format(
                    "SELECT * FROM c where c.PK = '{0}' and c.id = '{1}' and c.NonSensitive = '{2}'",
                    expectedDoc.PK,
                    expectedDoc.Id,
                    expectedDoc.NonSensitive),
                expectedDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainerCore,
                string.Format("SELECT * FROM c where c.Sensitive = '{0}'", testDoc.Sensitive),
                expectedDoc: null);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainerCore,
                queryDefinition: new QueryDefinition(
                    "select * from c where c.id = @theId and c.PK = @thePK")
                         .WithParameter("@theId", expectedDoc.Id)
                         .WithParameter("@thePK", expectedDoc.PK),
                expectedDoc: expectedDoc);

            expectedDoc.Sensitive = null;

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainerCore,
                "SELECT c.id, c.PK, c.Sensitive, c.NonSensitive FROM c",
                expectedDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainerCore,
                "SELECT c.id, c.PK, c.NonSensitive FROM c",
                expectedDoc);

            await EncryptionTests.ValidateSprocResultsAsync(
                EncryptionTests.itemContainerCore,
                expectedDoc);
        }

        [TestMethod]
        public async Task EncryptionChangeFeedDecryptionSuccessful()
        {
            string dek2 = "dek2ForChangeFeed";
            await EncryptionTests.CreateDekAsync(EncryptionTests.dekProvider, dek2);

            TestDoc testDoc1 = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite);
            TestDoc testDoc2 = await EncryptionTests.CreateItemAsync(
                EncryptionTests.itemContainerCore,
                EncryptionTests.GetEncryptionStreamTransformer(EncryptionTests.encryptor, EncryptionTests.GetEncryptionOptions(dek2, TestDoc.PathsToEncrypt)));
                        
            // change feed iterator
            await this.ValidateChangeFeedIteratorResponse(EncryptionTests.itemContainerCore, testDoc1, testDoc2);

            // change feed processor
            await this.ValidateChangeFeedProcessorResponse(EncryptionTests.itemContainerCore, testDoc1, testDoc2);
        }

        [TestMethod]
        public async Task EncryptionHandleDecryptionFailure()
        {
            string dek2 = "failDek";
            await EncryptionTests.CreateDekAsync(EncryptionTests.dekProvider, dek2);

            TestDoc testDoc1 = await EncryptionTests.CreateItemAsync(
                EncryptionTests.itemContainerCore,
                EncryptionTests.GetEncryptionStreamTransformer(EncryptionTests.encryptor, EncryptionTests.GetEncryptionOptions(dek2, TestDoc.PathsToEncrypt)));
            TestDoc testDoc2 = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";

            // success
            await EncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(EncryptionTests.itemContainerCore, testDoc1, testDoc2, query);

            // induce failure
            EncryptionTests.encryptor.FailDecryption = true;
            EncryptionTests.decryptionFailedDocId = testDoc1.Id;

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainerCore, testDoc1, false);

            await EncryptionTests.VerifyItemByReadStreamAsync(EncryptionTests.itemContainerCore, testDoc1, false);

            await EncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(EncryptionTests.itemContainerCore, testDoc1, testDoc2, query, false);

            await this.ValidateChangeFeedIteratorResponse(EncryptionTests.itemContainerCore, testDoc1, testDoc2, false);

            await this.ValidateChangeFeedProcessorResponse(EncryptionTests.itemContainerCore, testDoc1, testDoc2, false);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleDocs()
        {
            TestDoc testDoc1 = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite);
            TestDoc testDoc2 = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite);

            // test GetItemLinqQueryable
            await EncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(EncryptionTests.itemContainerCore, testDoc1, testDoc2, null);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";
            await EncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(EncryptionTests.itemContainerCore, testDoc1, testDoc2, query);

            // ORDER BY query
            query = query + " ORDER BY c._ts";
            await EncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(EncryptionTests.itemContainerCore, testDoc1, testDoc2, query);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleEncryptedProperties()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(
                EncryptionTests.itemContainerCore,
                EncryptionTests.GetEncryptionStreamTransformer(EncryptionTests.encryptor, EncryptionTests.GetEncryptionOptions(EncryptionTests.dekId, new List<string>() { "/Sensitive", "/NonSensitive" })));

            TestDoc expectedDoc = new TestDoc(testDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainerCore,
                "SELECT * FROM c",
                expectedDoc);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryBinaryResponse()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite);

            CosmosSerializationFormatOptions options = new CosmosSerializationFormatOptions(
                Documents.ContentSerializationFormat.CosmosBinary.ToString(),
                (content) => JsonNavigator.Create(content),
                () => JsonWriter.Create(JsonSerializationFormat.Binary));

            QueryRequestOptions requestOptions = new QueryRequestOptions
            {
                CosmosStreamTransformer = EncryptionTests.encryptionStreamTransformerForRead,
                CosmosSerializationFormatOptions = options
            };

            TestDoc expectedDoc = new TestDoc(testDoc);

            string query = "SELECT * FROM c";

            FeedIterator feedIterator = EncryptionTests.itemContainerCore.GetItemQueryStreamIterator(
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
        public async Task EncryptionDecryptQueryValueResponse()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite);
            string query = "SELECT VALUE COUNT(1) FROM c";

            await EncryptionTests.ValidateQueryResponseAsync(EncryptionTests.itemContainerCore, query);
        }

        [TestMethod]
        public async Task EncryptionDecryptGroupByQueryResultTest()
        {
            string partitionKey = Guid.NewGuid().ToString();

            TestDoc testDoc1 = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite, partitionKey);
            TestDoc testDoc2 = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite, partitionKey);

            string query = $"SELECT COUNT(c.Id), c.PK " +
                           $"FROM c WHERE c.PK = '{partitionKey}' " +
                           $"GROUP BY c.PK ";

            await EncryptionTests.ValidateQueryResponseAsync(EncryptionTests.itemContainerCore, query);
        }

        [TestMethod]
        public async Task EncryptionRudItem()
        {
            TestDoc testDoc = await EncryptionTests.UpsertItemAsync(
                EncryptionTests.itemContainerCore,
                TestDoc.Create(),
                EncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.Created);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainerCore, testDoc);

            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            ItemResponse<TestDoc> upsertResponse = await EncryptionTests.UpsertItemAsync(
                EncryptionTests.itemContainerCore,
                testDoc,
                EncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.OK);
            TestDoc updatedDoc = upsertResponse.Resource;

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainerCore, updatedDoc);

            updatedDoc.NonSensitive = Guid.NewGuid().ToString();
            updatedDoc.Sensitive = Guid.NewGuid().ToString();

            TestDoc replacedDoc = await EncryptionTests.ReplaceItemAsync(
                EncryptionTests.itemContainerCore,
                updatedDoc,
                EncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                upsertResponse.ETag);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainerCore, replacedDoc);

            await EncryptionTests.DeleteItemAsync(EncryptionTests.itemContainerCore, replacedDoc);
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuthRestricted()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite);

            User restrictedUser = EncryptionTests.databaseCore.GetUser(Guid.NewGuid().ToString());
            await EncryptionTests.databaseCore.CreateUserAsync(restrictedUser.Id);

            PermissionProperties restrictedUserPermission = await restrictedUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, EncryptionTests.itemContainer));

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());

            (string endpoint, string _) = TestCommon.GetAccountInfo();
            CosmosClient clientForRestrictedUser = new CosmosClientBuilder(endpoint, restrictedUserPermission.Token).Build();

            Database databaseForRestrictedUser = clientForRestrictedUser.GetDatabase(EncryptionTests.databaseCore.Id);
            Container containerForRestrictedUser = databaseForRestrictedUser.GetContainer(EncryptionTests.itemContainer.Id);

            await EncryptionTests.PerformForbiddenOperationAsync(() =>
                dekProvider.InitializeAsync(databaseForRestrictedUser, EncryptionTests.keyContainer.Id), "CosmosDekProvider.InitializeAsync");

            await EncryptionTests.PerformOperationOnUninitializedDekProviderAsync(() =>
                dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(EncryptionTests.dekId), "DEK.ReadAsync");

            int failureCount = 0;
            TestEncryptor encryptor = new TestEncryptor(dekProvider);
            Action<Stream, Exception> errorHandler = (stream, exception) =>
            {
                exception.Message.Equals("The CosmosDataEncryptionKeyProvider was not initialized.");
                failureCount++;
            };
            ItemRequestOptions requestOptionsWithEncryptor = EncryptionTests.GetRequestOptions(
                EncryptionTests.GetEncryptionStreamTransformer(encryptor, null, errorHandler));

            ItemResponse<TestDoc> result = await containerForRestrictedUser.ReadItemAsync<TestDoc>(
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                requestOptionsWithEncryptor);
            Assert.AreEqual(failureCount, 1);

            ResponseMessage response = await containerForRestrictedUser.ReadItemStreamAsync(
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                requestOptionsWithEncryptor);
            Assert.AreEqual(failureCount, 2);
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuthAllowed()
        {
            User keyManagerUser = EncryptionTests.databaseCore.GetUser(Guid.NewGuid().ToString());
            await EncryptionTests.databaseCore.CreateUserAsync(keyManagerUser.Id);

            PermissionProperties keyManagerUserPermission = await keyManagerUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, EncryptionTests.keyContainer));

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            TestEncryptor encryptor = new TestEncryptor(dekProvider);
            (string endpoint, string _) = TestCommon.GetAccountInfo();
            CosmosClient clientForKeyManagerUser = new CosmosClientBuilder(endpoint, keyManagerUserPermission.Token).Build();

            Database databaseForKeyManagerUser = clientForKeyManagerUser.GetDatabase(EncryptionTests.databaseCore.Id);

            await dekProvider.InitializeAsync(databaseForKeyManagerUser, EncryptionTests.keyContainer.Id);

            DataEncryptionKeyProperties readDekProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(EncryptionTests.dekId);
            Assert.AreEqual(EncryptionTests.dekProperties, readDekProperties);
        }

        [TestMethod]
        public async Task EncryptionRestrictedProperties()
        {
            TestDoc testDoc = TestDoc.Create();

            try
            {
                EncryptionStreamTransformer encryptionTransformer = EncryptionTests.GetEncryptionStreamTransformer(EncryptionTests.encryptor, EncryptionTests.GetEncryptionOptions(dekId, new List<string>() { "/id" }));
                await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, encryptionTransformer);
                Assert.Fail("Expected item creation with id specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }

            try
            {
                await EncryptionTests.CreateItemAsync(
                    EncryptionTests.itemContainerCore,
                     EncryptionTests.GetEncryptionStreamTransformer(EncryptionTests.encryptor, EncryptionTests.GetEncryptionOptions(EncryptionTests.dekId, new List<string>() { "/PK" })));
                Assert.Fail("Expected item creation with PK specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }
        }

        [TestMethod]
        public async Task EncryptionBulkCrud()
        {
            TestDoc docToReplace = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite);
            docToReplace.NonSensitive = Guid.NewGuid().ToString();
            docToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToUpsert = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite);
            docToUpsert.NonSensitive = Guid.NewGuid().ToString();
            docToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite);

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            CosmosClient clientWithBulk = new CosmosClientBuilder(endpoint, authKey)
                .WithBulkExecution(true)
                .Build();

            DatabaseInternal databaseWithBulk = (DatabaseInlineCore)clientWithBulk.GetDatabase(EncryptionTests.databaseCore.Id);
            ContainerInternal containerWithBulk = (ContainerInlineCore)databaseWithBulk.GetContainer(EncryptionTests.itemContainer.Id);

            List<Task> tasks = new List<Task>();
            tasks.Add(EncryptionTests.CreateItemAsync(containerWithBulk, EncryptionTests.encryptionStreamTransformerForWrite));
            tasks.Add(EncryptionTests.UpsertItemAsync(containerWithBulk, TestDoc.Create(), EncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.Created));
            tasks.Add(EncryptionTests.ReplaceItemAsync(containerWithBulk, docToReplace, EncryptionTests.dekId, TestDoc.PathsToEncrypt));
            tasks.Add(EncryptionTests.UpsertItemAsync(containerWithBulk, docToUpsert, EncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.OK));
            tasks.Add(EncryptionTests.DeleteItemAsync(containerWithBulk, docToDelete));
            await Task.WhenAll(tasks);
        }

        [TestMethod]
        public async Task EncryptionTransactionalBatchCrud()
        {
            string partitionKey = "thePK";
            string dek2 = "dek2ForBatch";
            await EncryptionTests.CreateDekAsync(EncryptionTests.dekProvider, dek2);
            EncryptionStreamTransformer encryptionTransformerForBatch = EncryptionTests.GetEncryptionStreamTransformer(
                EncryptionTests.encryptor, EncryptionTests.GetEncryptionOptions(dek2, TestDoc.PathsToEncrypt));

            TestDoc doc1ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc2ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc3ToCreate = TestDoc.Create(partitionKey);

            ItemResponse<TestDoc> doc1ToReplaceCreateResponse = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite, partitionKey);
            TestDoc doc1ToReplace = doc1ToReplaceCreateResponse.Resource;
            doc1ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc1ToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc2ToReplace = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, encryptionTransformerForBatch, partitionKey);
            doc2ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc2ToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc1ToUpsert = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, encryptionTransformerForBatch, partitionKey);
            doc1ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc1ToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc2ToUpsert = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite, partitionKey);
            doc2ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc2ToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainerCore, EncryptionTests.encryptionStreamTransformerForWrite, partitionKey);

            TransactionalBatchResponse batchResponse = await EncryptionTests.itemContainer.CreateTransactionalBatch(new Cosmos.PartitionKey(partitionKey))
                .CreateItem(doc1ToCreate, EncryptionTests.GetBatchItemRequestOptions(EncryptionTests.encryptionStreamTransformerForWrite))
                .CreateItemStream(doc2ToCreate.ToStream(), EncryptionTests.GetBatchItemRequestOptions(encryptionTransformerForBatch))
                .ReplaceItem(doc1ToReplace.Id, doc1ToReplace, EncryptionTests.GetBatchItemRequestOptions(encryptionTransformerForBatch, doc1ToReplaceCreateResponse.ETag))
                .CreateItem(doc3ToCreate)
                .ReplaceItemStream(doc2ToReplace.Id, doc2ToReplace.ToStream(), EncryptionTests.GetBatchItemRequestOptions(encryptionTransformerForBatch))
                .UpsertItem(doc1ToUpsert, EncryptionTests.GetBatchItemRequestOptions(EncryptionTests.encryptionStreamTransformerForWrite))
                .DeleteItem(docToDelete.Id)
                .UpsertItemStream(doc2ToUpsert.ToStream(), EncryptionTests.GetBatchItemRequestOptions(encryptionTransformerForBatch))
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainerCore, doc1ToCreate);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainerCore, doc2ToCreate);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainerCore, doc3ToCreate);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainerCore, doc1ToReplace);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainerCore, doc2ToReplace);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainerCore, doc1ToUpsert);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainerCore, doc2ToUpsert);

            ResponseMessage readResponseMessage = await EncryptionTests.itemContainer.ReadItemStreamAsync(docToDelete.Id, new PartitionKey(docToDelete.PK));
            Assert.AreEqual(HttpStatusCode.NotFound, readResponseMessage.StatusCode);
        }

        private static async Task ValidateSprocResultsAsync(ContainerInternal containerCore, TestDoc expectedDoc)
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
            ContainerInternal containerCore,
            string query = null,
            TestDoc expectedDoc = null,
            QueryDefinition queryDefinition = null)
        {
            QueryRequestOptions requestOptions = expectedDoc != null ?
                new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey(expectedDoc.PK),
                    CosmosStreamTransformer = EncryptionTests.encryptionStreamTransformerForRead
                }
                : null;

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
            ContainerInternal containerCore,
            TestDoc testDoc1,
            TestDoc testDoc2,
            string query,
            bool wasDecryptionSuccessful = true)
        {
            FeedIterator<TestDoc> queryResponseIterator;
            if (query != null)
            {
                queryResponseIterator = containerCore.GetItemQueryIterator<TestDoc>(
                    query,
                    requestOptions: new QueryRequestOptions { CosmosStreamTransformer = EncryptionTests.encryptionStreamTransformerForRead });
            }
            else
            {
                queryResponseIterator = containerCore.GetItemLinqQueryable<TestDoc>(requestOptions: new QueryRequestOptions { CosmosStreamTransformer = EncryptionTests.encryptionStreamTransformerForRead }).ToFeedIterator<TestDoc>();
            }

            FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();
            Assert.AreEqual(null, readDocs.ContinuationToken);

            if (query == null)
            {
                Assert.IsTrue(readDocs.Count >= 2);
            }
            else
            {
                if(!wasDecryptionSuccessful)
                {
                    testDoc1.Sensitive = null;
                }

                Assert.AreEqual(2, readDocs.Count);
                foreach (TestDoc readDoc in readDocs)
                {
                    Assert.AreEqual(readDoc, readDoc.Id.Equals(testDoc1.Id) ? testDoc1 : testDoc2);
                }
            }            
        }

        private static async Task ValidateQueryResponseAsync(ContainerInternal containerCore, string query)
        {
            FeedIterator feedIterator = containerCore.GetItemQueryStreamIterator(query);
            while (feedIterator.HasMoreResults)
            {
                ResponseMessage response = await feedIterator.ReadNextAsync();
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.IsNull(response.ErrorMessage);
            }
        }

        private async Task ValidateChangeFeedIteratorResponse(
            ContainerInternal containerCore,
            TestDoc testDoc1,
            TestDoc testDoc2,
            bool wasDecryptionSuccessful = true)
        {
            FeedIterator<TestDoc> changeIterator = containerCore.GetChangeFeedIterator<TestDoc>(
                continuationToken: null,
                new ChangeFeedRequestOptions()
                {
                    StartTime = DateTime.MinValue.ToUniversalTime(),
                    CosmosStreamTransformer = EncryptionTests.encryptionStreamTransformerForRead
                });

            List<TestDoc> changeFeedReturnedDocs = new List<TestDoc>();
            while (changeIterator.HasMoreResults)
            {
                FeedResponse<TestDoc> testDocs = await changeIterator.ReadNextAsync();
                for (int index = 0; index < testDocs.Count; index++)
                {
                    if (testDocs.ElementAt(index).Id.Equals(testDoc1.Id) || testDocs.ElementAt(index).Id.Equals(testDoc2.Id))
                    {
                        changeFeedReturnedDocs.Add(testDocs.ElementAt(index));
                    }
                }
            }

            if (!wasDecryptionSuccessful)
            {
                testDoc1.Sensitive = null;
            }

            Assert.AreEqual(changeFeedReturnedDocs.Count, 2);
            Assert.AreEqual(testDoc1, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 2]);
            Assert.AreEqual(testDoc2, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 1]);
        }

        private async Task ValidateChangeFeedProcessorResponse(
            ContainerInternal containerCore,
            TestDoc testDoc1,
            TestDoc testDoc2,
            bool wasDecryptionSuccessful = true)
        {
            List<TestDoc> changeFeedReturnedDocs = new List<TestDoc>();
            ChangeFeedProcessor cfp = containerCore.GetChangeFeedProcessorBuilder(
                "testCFP",
                (IReadOnlyCollection<TestDoc> changes, CancellationToken cancellationToken)
                =>
                {
                    changeFeedReturnedDocs.AddRange(changes);
                    return Task.CompletedTask;
                })
                .WithInMemoryLeaseContainer()
                .WithStartFromBeginning()
                .WithCosmosStreamTransformer(EncryptionTests.encryptionStreamTransformerForRead)
                .Build();

            await cfp.StartAsync();
            await Task.Delay(2000);
            await cfp.StopAsync();

            Assert.IsTrue(changeFeedReturnedDocs.Count >= 2);
            if (!wasDecryptionSuccessful)
            {
                testDoc1.Sensitive = null;
            }

            foreach (TestDoc testDoc in changeFeedReturnedDocs)
            {
                if (testDoc.Id.Equals(testDoc1.Id))
                {
                    Assert.AreEqual(testDoc1, testDoc);
                }
                else if (testDoc.Id.Equals(testDoc2.Id))
                {
                    Assert.AreEqual(testDoc2, testDoc);
                }
            }
        }

        private static void ErrorHandler(Stream input, Exception exception)
        {
            Assert.AreEqual(exception.Message, "Null DataEncryptionKey returned from FetchDataEncryptionKeyAsync.");
            JObject itemJObj = EncryptionTests.cosmosSerializer.FromStream<JObject>(input);
            JProperty encryptionPropertiesJProp = itemJObj.Property("_ei");
            Assert.IsNotNull(encryptionPropertiesJProp);
            Assert.AreEqual(itemJObj.Property("id").Value.ToString(), EncryptionTests.decryptionFailedDocId);
        }

        private static CosmosClient GetClient()
        {
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            return new CosmosClientBuilder(endpoint, authKey).Build();
        }

        private static async Task IterateDekFeedAsync(
            CosmosDataEncryptionKeyProvider dekProvider,
            List<string> expectedDekIds,
            bool isExpectedDeksCompleteSetForRequest,
            bool isResultOrderExpected,
            string query,
            int? itemCountInPage = null)
        {
            int remainingItemCount = expectedDekIds.Count;
            QueryRequestOptions requestOptions = null;
            if (itemCountInPage.HasValue)
            {
                requestOptions = new QueryRequestOptions()
                {
                    MaxItemCount = itemCountInPage
                };
            }

            FeedIterator<DataEncryptionKeyProperties> dekIterator = dekProvider.DataEncryptionKeyContainer
                .GetDataEncryptionKeyQueryIterator<DataEncryptionKeyProperties>(
                    query,
                    requestOptions: requestOptions);

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
            ContainerInternal containerCore,
            TestDoc testDoc,
            string dekId,
            List<string> pathsToEncrypt,
            HttpStatusCode expectedStatusCode)
        {
            ItemResponse<TestDoc> upsertResponse = await containerCore.UpsertItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                EncryptionTests.GetRequestOptions(EncryptionTests.encryptionStreamTransformerForWrite));
            Assert.AreEqual(expectedStatusCode, upsertResponse.StatusCode);
            Assert.AreEqual(testDoc, upsertResponse.Resource);
            return upsertResponse;
        }

        private static async Task<ItemResponse<TestDoc>> CreateItemAsync(
            ContainerInternal containerCore,
            EncryptionStreamTransformer encryptionStreamTransformer,
            string partitionKey = null)
        {
            TestDoc testDoc = TestDoc.Create(partitionKey);
            ItemResponse<TestDoc> createResponse = await containerCore.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                EncryptionTests.GetRequestOptions(encryptionStreamTransformer));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
            return createResponse;
        }

        private static async Task<ItemResponse<TestDoc>> ReplaceItemAsync(
            ContainerInternal containerCore,
            TestDoc testDoc,
            string dekId,
            List<string> pathsToEncrypt,
            string etag = null)
        {
            ItemResponse<TestDoc> replaceResponse = await containerCore.ReplaceItemAsync(
                testDoc,
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                EncryptionTests.GetRequestOptions(EncryptionTests.encryptionStreamTransformerForWrite, etag));

            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            Assert.AreEqual(testDoc, replaceResponse.Resource);
            return replaceResponse;
        }

        private static async Task<ItemResponse<TestDoc>> DeleteItemAsync(
            ContainerInternal containerCore,
            TestDoc testDoc)
        {
            ItemResponse<TestDoc> deleteResponse = await containerCore.DeleteItemAsync<TestDoc>(
                testDoc.Id,
                new PartitionKey(testDoc.PK));

            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
            Assert.IsNull(deleteResponse.Resource);
            return deleteResponse;
        }

        private static EncryptionStreamTransformer GetEncryptionStreamTransformer(
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            Action<Stream, Exception> errorHandler = null)
        {
            return new EncryptionStreamTransformer(encryptor, encryptionOptions, errorHandler);
        }

        private static EncryptionOptions GetEncryptionOptions(string dekId, List<string> pathsToEncrypt)
        {
            return new EncryptionOptions
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = pathsToEncrypt
            };
        }

        private static ItemRequestOptions GetRequestOptions(
            EncryptionStreamTransformer encryptionStreamTransformer,
            string ifMatchEtag = null)
        {
            return new ItemRequestOptions
            {
                CosmosStreamTransformer = encryptionStreamTransformer,
                IfMatchEtag = ifMatchEtag
            };
        }

        private static TransactionalBatchItemRequestOptions GetBatchItemRequestOptions(
            EncryptionStreamTransformer encryptionStreamTransformer,
            string ifMatchEtag = null)
        {
            return new TransactionalBatchItemRequestOptions
            {
                CosmosStreamTransformer = encryptionStreamTransformer,
                IfMatchEtag = ifMatchEtag
            };
        }

        private static async Task VerifyItemByReadStreamAsync(Container container, TestDoc testDoc, bool wasDecryptionSuccessful = true)
        {
            ResponseMessage readResponseMessage = await container.ReadItemStreamAsync(
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                EncryptionTests.GetRequestOptions(EncryptionTests.encryptionStreamTransformerForRead));
            Assert.AreEqual(HttpStatusCode.OK, readResponseMessage.StatusCode);

            Assert.IsNotNull(readResponseMessage.Content);
            TestDoc readDoc = TestCommon.SerializerCore.FromStream<TestDoc>(readResponseMessage.Content);
            if (!wasDecryptionSuccessful)
            {
                testDoc.Sensitive = null;
            }
            Assert.AreEqual(testDoc, readDoc);
        }

        private static async Task VerifyItemByReadAsync(Container container, TestDoc testDoc, bool wasDecryptionSuccessful = true)
        {
            ItemResponse<TestDoc> readResponse = await container.ReadItemAsync<TestDoc>(
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                EncryptionTests.GetRequestOptions(EncryptionTests.encryptionStreamTransformerForRead));
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);

            if (!wasDecryptionSuccessful)
            {
                testDoc.Sensitive = null;
            }
            Assert.AreEqual(testDoc, readResponse.Resource);
        }

        private static async Task<DataEncryptionKeyProperties> CreateDekAsync(CosmosDataEncryptionKeyProvider dekProvider, string dekId)
        {
            ItemResponse<DataEncryptionKeyProperties> dekResponse = await dekProvider.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                dekId,
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                EncryptionTests.metadata1);

            Assert.AreEqual(HttpStatusCode.Created, dekResponse.StatusCode);
            Assert.IsTrue(dekResponse.RequestCharge > 0);
            Assert.IsNotNull(dekResponse.ETag);

            DataEncryptionKeyProperties dekProperties = dekResponse.Resource;
            Assert.AreEqual(dekResponse.ETag, dekProperties.ETag);
            Assert.AreEqual(dekId, dekProperties.Id);
            return dekProperties;
        }

        private static async Task PerformForbiddenOperationAsync(Func<Task> func, string operationName)
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

        private static async Task PerformOperationOnUninitializedDekProviderAsync(Func<Task> func, string operationName)
        {
            try
            {
                await func();
                Assert.Fail($"Expected {operationName} to not work on uninitialized CosmosDataEncryptionKeyProvider.");
            }
            catch (InvalidOperationException ex)
            {
                Assert.IsTrue(ex.Message.Contains("The CosmosDataEncryptionKeyProvider was not initialized."));
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

            public static TestDoc Create(string partitionKey = null)
            {
                return new TestDoc()
                {
                    Id = Guid.NewGuid().ToString(),
                    PK = partitionKey ?? Guid.NewGuid().ToString(),
                    NonSensitive = Guid.NewGuid().ToString(),
                    Sensitive = Guid.NewGuid().ToString()
                };
            }

            public Stream ToStream()
            {
                return TestCommon.SerializerCore.ToStream(this);
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

        // This class is same as CosmosEncryptor but copied since the emulator tests don't
        // have internal visibility into Cosmos.Encryption assembly.
        private class TestEncryptor : Encryptor
        {
            public DataEncryptionKeyProvider DataEncryptionKeyProvider { get; }

            public bool FailDecryption { get; set; }

            public TestEncryptor(DataEncryptionKeyProvider dataEncryptionKeyProvider)
            {
                this.DataEncryptionKeyProvider = dataEncryptionKeyProvider;
                this.FailDecryption = false;
            }

            public override async Task<byte[]> DecryptAsync(
                byte[] cipherText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                if (this.FailDecryption && dataEncryptionKeyId.Equals("failDek"))
                {
                    throw new InvalidOperationException($"Null {nameof(DataEncryptionKey)} returned from {nameof(this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync)}.");
                }

                DataEncryptionKey dek = await this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync(
                    dataEncryptionKeyId,
                    encryptionAlgorithm,
                    cancellationToken);

                if (dek == null)
                {
                    throw new InvalidOperationException($"Null {nameof(DataEncryptionKey)} returned from {nameof(this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync)}.");
                }

                return dek.DecryptData(cipherText);
            }

            public override async Task<byte[]> EncryptAsync(
                byte[] plainText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                DataEncryptionKey dek = await this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync(
                    dataEncryptionKeyId,
                    encryptionAlgorithm,
                    cancellationToken);

                return dek.EncryptData(plainText);
            }
        }
    }
}
