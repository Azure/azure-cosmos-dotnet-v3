//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionTests
    {
        private static readonly EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata("metadata1");
        private const string metadataUpdateSuffix = "updated";
        private const string dekId = "mydek";
        private static CosmosClient client;
        private static Database database;
        private static DataEncryptionKeyProperties dekProperties;
        private static Container itemContainer;
        private static Container encryptionContainer;
        private static Container keyContainer;
        private static CosmosDataEncryptionKeyProvider dekProvider;
        private static TestEncryptor encryptor;
        private static string decryptionFailedDocId;

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            EncryptionTests.dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            EncryptionTests.encryptor = new TestEncryptor(EncryptionTests.dekProvider);

            EncryptionTests.client = TestCommon.CreateCosmosClient();
            EncryptionTests.database = await EncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());

            EncryptionTests.keyContainer = await EncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            await EncryptionTests.dekProvider.InitializeAsync(EncryptionTests.database, EncryptionTests.keyContainer.Id);

            EncryptionTests.itemContainer = await EncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/PK", 400);
            EncryptionTests.encryptionContainer = EncryptionTests.itemContainer.WithEncryptor(encryptor);
            EncryptionTests.dekProperties = await EncryptionTests.CreateDekAsync(EncryptionTests.dekProvider, EncryptionTests.dekId);
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            if (EncryptionTests.database != null)
            {
                using (await EncryptionTests.database.DeleteStreamAsync()) { }
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
            await dekProvider.InitializeAsync(EncryptionTests.database, EncryptionTests.keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        [TestMethod]
        public async Task EncryptionDekReadFeed()
        {
            Container newKeyContainer = await EncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            try
            {
                CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
                await dekProvider.InitializeAsync(EncryptionTests.database, newKeyContainer.Id);

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
        public async Task EncryptionCleanupRawDekOnExpiry()
        {
            TestKeyWrapProvider testKeyWrapProvider = new TestKeyWrapProvider();
            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(
                testKeyWrapProvider,
                dekPropertiesTimeToLive: null,
                cleanupIterationInterval: TimeSpan.FromSeconds(1),
                cleanupBufferTimeAfterExpiry: TimeSpan.FromSeconds(0));
            TestEncryptor encryptor = new TestEncryptor(dekProvider);
            CosmosClient client = TestCommon.CreateCosmosClient();
            Database database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            await dekProvider.InitializeAsync(database, Guid.NewGuid().ToString());
            Container itemContainer = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/PK", 400);
            Container encryptionContainer = itemContainer.WithEncryptor(encryptor);

            const string dekId1 = "dekId1", dekId2 = "dekId2";
            testKeyWrapProvider.UnwrapCallCount = 0;
            testKeyWrapProvider.cacheTTL = TimeSpan.FromSeconds(1.5);
            
            await EncryptionTests.CreateDekAsync(dekProvider, dekId1); // dekId1 TTL is set to 1.5 sec & raw DEK for dekId1 is added in cache
            Assert.IsTrue(testKeyWrapProvider.UnwrapCallCount.Equals(1));

            TestDoc testDoc1 = await EncryptionTests.CreateItemAsync(encryptionContainer, dekId1, TestDoc.PathsToEncrypt);
            Assert.IsTrue(testKeyWrapProvider.UnwrapCallCount.Equals(1)); // Raw DEK for dekId1 exists in cache, hence no new Unwrap call is needed

            Thread.Sleep(TimeSpan.FromSeconds(0.5));

            await EncryptionTests.VerifyItemByReadAsync(encryptionContainer, testDoc1);
            Assert.IsTrue(testKeyWrapProvider.UnwrapCallCount.Equals(1)); // Raw DEK for dekId1 exists in cache

            testKeyWrapProvider.cacheTTL = TimeSpan.FromSeconds(0.4);
            await EncryptionTests.CreateDekAsync(dekProvider, dekId2); // dekId2 TTL is set to 0.4 sec & raw DEK for dekId2 is added in cache
            Assert.IsTrue(testKeyWrapProvider.UnwrapCallCount.Equals(2));

            TestDoc testDoc2 = await EncryptionTests.CreateItemAsync(encryptionContainer, dekId2, TestDoc.PathsToEncrypt);
            Assert.IsTrue(testKeyWrapProvider.UnwrapCallCount.Equals(2)); // Raw DEK for dekId2 exists in cache

            Thread.Sleep(TimeSpan.FromSeconds(0.75)); // Raw DEK for dekId2 should be cleaned up from memory

            testKeyWrapProvider.cacheTTL = TimeSpan.FromSeconds(0.5); // now cache TTL will be set to 0.5 sec

            await EncryptionTests.VerifyItemByReadAsync(encryptionContainer, testDoc1);
            Assert.IsTrue(testKeyWrapProvider.UnwrapCallCount.Equals(2)); // Raw DEK for dekId1 exists in cache

            await EncryptionTests.VerifyItemByReadAsync(encryptionContainer, testDoc2);
            Assert.IsTrue(testKeyWrapProvider.UnwrapCallCount.Equals(3)); // Raw DEK for dekId2 doesn't exist in cache, hence new Unwrap call is needed
            // raw DEK for dekId2 is added to cache with TTL 0.5 sec

            Thread.Sleep(TimeSpan.FromSeconds(1)); // allow cleanup process to run and delete raw DEKs from memory

            // Raw DEK should have been cleaned up, hence another Unwrap call is needed now for read request
            await EncryptionTests.VerifyItemByReadAsync(encryptionContainer, testDoc1);
            Assert.IsTrue(testKeyWrapProvider.UnwrapCallCount.Equals(4));

            await EncryptionTests.VerifyItemByReadAsync(encryptionContainer, testDoc2);
            Assert.IsTrue(testKeyWrapProvider.UnwrapCallCount.Equals(5)); // Raw DEK for dekId2 exists in cache, hence new Unwrap call is needed
        }
        
        [TestMethod]
        public async Task EncryptionCreateItemWithoutEncryptionOptions()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await EncryptionTests.encryptionContainer.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithNullEncryptionOptions()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await EncryptionTests.encryptionContainer.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                new EncryptionItemRequestOptions());
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithoutPrimaryKey()
        {
            TestDoc testDoc = TestDoc.Create();
            try
            {
                await EncryptionTests.encryptionContainer.CreateItemAsync(
                    testDoc,
                    requestOptions: EncryptionTests.GetRequestOptions(EncryptionTests.dekId, TestDoc.PathsToEncrypt));
                Assert.Fail("CreateItem should've failed because PartitionKey was not provided.");
            }
            catch (NotSupportedException ex)
            {
                Assert.AreEqual("partitionKey cannot be null for operations using EncryptionContainer.", ex.Message);
            }
        }

        [TestMethod]
        public async Task EncryptionCreateItem()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.encryptionContainer, testDoc);

            await EncryptionTests.VerifyItemByReadStreamAsync(EncryptionTests.encryptionContainer, testDoc);

            TestDoc expectedDoc = new TestDoc(testDoc);

            // Read feed (null query)
            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.encryptionContainer,
                query: null,
                expectedDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.encryptionContainer,
                "SELECT * FROM c",
                expectedDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.encryptionContainer,
                string.Format(
                    "SELECT * FROM c where c.PK = '{0}' and c.id = '{1}' and c.NonSensitive = '{2}'",
                    expectedDoc.PK,
                    expectedDoc.Id,
                    expectedDoc.NonSensitive),
                expectedDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.encryptionContainer,
                string.Format("SELECT * FROM c where c.Sensitive = '{0}'", testDoc.Sensitive),
                expectedDoc: null);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.encryptionContainer,
                queryDefinition: new QueryDefinition(
                    "select * from c where c.id = @theId and c.PK = @thePK")
                         .WithParameter("@theId", expectedDoc.Id)
                         .WithParameter("@thePK", expectedDoc.PK),
                expectedDoc: expectedDoc);

            expectedDoc.Sensitive = null;

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.encryptionContainer,
                "SELECT c.id, c.PK, c.Sensitive, c.NonSensitive FROM c",
                expectedDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.encryptionContainer,
                "SELECT c.id, c.PK, c.NonSensitive FROM c",
                expectedDoc);

            await EncryptionTests.ValidateSprocResultsAsync(
                EncryptionTests.encryptionContainer,
                expectedDoc);
        }

        [TestMethod]
        public async Task EncryptionChangeFeedDecryptionSuccessful()
        {
            string dek2 = "dek2ForChangeFeed";
            await EncryptionTests.CreateDekAsync(EncryptionTests.dekProvider, dek2);

            TestDoc testDoc1 = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt);
            
            // change feed iterator
            await this.ValidateChangeFeedIteratorResponse(EncryptionTests.encryptionContainer, testDoc1, testDoc2);

            // change feed processor
            // await this.ValidateChangeFeedProcessorResponse(EncryptionTests.encryptionContainer, testDoc1, testDoc2);
        }

        [TestMethod]
        public async Task EncryptionHandleDecryptionFailure()
        {
            string dek2 = "failDek";
            await EncryptionTests.CreateDekAsync(EncryptionTests.dekProvider, dek2);

            TestDoc testDoc1 = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";

            // success
            await EncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(EncryptionTests.encryptionContainer, testDoc1, testDoc2, query);

            // induce failure
            EncryptionTests.encryptor.FailDecryption = true;
            EncryptionTests.decryptionFailedDocId = testDoc1.Id;
            testDoc1.Sensitive = null;

            await EncryptionTests.VerifyItemByReadAsync(
                EncryptionTests.encryptionContainer,
                testDoc1,
                EncryptionTests.GetItemRequestOptionsWithDecryptionResultHandler());

            await EncryptionTests.VerifyItemByReadStreamAsync(
                EncryptionTests.encryptionContainer,
                testDoc1,
                EncryptionTests.GetItemRequestOptionsWithDecryptionResultHandler());

            EncryptionQueryRequestOptions queryRequestOptions = new EncryptionQueryRequestOptions
            {
                DecryptionResultHandler = EncryptionTests.ErrorHandler
            };

            await EncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(
                EncryptionTests.encryptionContainer,
                testDoc1,
                testDoc2,
                query,
                queryRequestOptions);

            // GetItemLinqQueryable
            await EncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(
                EncryptionTests.encryptionContainer, 
                testDoc1, 
                testDoc2, 
                query: null,
                queryRequestOptions);

            await this.ValidateChangeFeedIteratorResponse(
                EncryptionTests.encryptionContainer,
                testDoc1,
                testDoc2,
                EncryptionTests.ErrorHandler);

            // await this.ValidateChangeFeedProcessorResponse(EncryptionTests.itemContainerCore, testDoc1, testDoc2, false);
            EncryptionTests.encryptor.FailDecryption = false;
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleDocs()
        {
            TestDoc testDoc1 = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            // test GetItemLinqQueryable
            await EncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(EncryptionTests.encryptionContainer, testDoc1, testDoc2, null);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";
            await EncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(EncryptionTests.encryptionContainer, testDoc1, testDoc2, query);

            // ORDER BY query
            query = query + " ORDER BY c._ts";
            await EncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(EncryptionTests.encryptionContainer, testDoc1, testDoc2, query);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleEncryptedProperties()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(
                EncryptionTests.encryptionContainer,
                EncryptionTests.dekId,
                new List<string>() { "/Sensitive", "/NonSensitive" });

            TestDoc expectedDoc = new TestDoc(testDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.encryptionContainer,
                "SELECT * FROM c",
                expectedDoc);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryValueResponse()
        {
            await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);
            string query = "SELECT VALUE COUNT(1) FROM c";

            await EncryptionTests.ValidateQueryResponseAsync(EncryptionTests.encryptionContainer, query);
        }

        [TestMethod]
        public async Task EncryptionDecryptGroupByQueryResultTest()
        {
            string partitionKey = Guid.NewGuid().ToString();

            await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt, partitionKey);
            await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt, partitionKey);

            string query = $"SELECT COUNT(c.Id), c.PK " +
                           $"FROM c WHERE c.PK = '{partitionKey}' " +
                           $"GROUP BY c.PK ";

            await EncryptionTests.ValidateQueryResponseAsync(EncryptionTests.encryptionContainer, query);
        }

        [TestMethod]
        public async Task EncryptionRudItem()
        {
            TestDoc testDoc = await EncryptionTests.UpsertItemAsync(
                EncryptionTests.encryptionContainer,
                TestDoc.Create(),
                EncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.Created);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.encryptionContainer, testDoc);

            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            ItemResponse<TestDoc> upsertResponse = await EncryptionTests.UpsertItemAsync(
                EncryptionTests.encryptionContainer,
                testDoc,
                EncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.OK);
            TestDoc updatedDoc = upsertResponse.Resource;

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.encryptionContainer, updatedDoc);

            updatedDoc.NonSensitive = Guid.NewGuid().ToString();
            updatedDoc.Sensitive = Guid.NewGuid().ToString();

            TestDoc replacedDoc = await EncryptionTests.ReplaceItemAsync(
                EncryptionTests.encryptionContainer,
                updatedDoc,
                EncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                upsertResponse.ETag);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.encryptionContainer, replacedDoc);

            await EncryptionTests.DeleteItemAsync(EncryptionTests.encryptionContainer, replacedDoc);
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuthRestricted()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            User restrictedUser = EncryptionTests.database.GetUser(Guid.NewGuid().ToString());
            await EncryptionTests.database.CreateUserAsync(restrictedUser.Id);

            PermissionProperties restrictedUserPermission = await restrictedUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, EncryptionTests.itemContainer));

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            TestEncryptor encryptor = new TestEncryptor(dekProvider);

            CosmosClient clientForRestrictedUser = TestCommon.CreateCosmosClient(
                restrictedUserPermission.Token);

            int failureCount = 0;
            Database databaseForRestrictedUser = clientForRestrictedUser.GetDatabase(EncryptionTests.database.Id);
            Container containerForRestrictedUser = databaseForRestrictedUser.GetContainer(EncryptionTests.itemContainer.Id);
            Action<DecryptionResult> errorHandler = (decryptionErrorDetails) =>
            {
                Assert.AreEqual(decryptionErrorDetails.Exception.Message, $"The CosmosDataEncryptionKeyProvider was not initialized.");
                failureCount++;
            };
            Container encryptionContainerForRestrictedUser = containerForRestrictedUser.WithEncryptor(encryptor);

            await EncryptionTests.PerformForbiddenOperationAsync(() =>
                dekProvider.InitializeAsync(databaseForRestrictedUser, EncryptionTests.keyContainer.Id), "CosmosDekProvider.InitializeAsync");

            await EncryptionTests.PerformOperationOnUninitializedDekProviderAsync(() =>
                dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(EncryptionTests.dekId), "DEK.ReadAsync");

            await encryptionContainerForRestrictedUser.ReadItemAsync<TestDoc>(
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                new EncryptionItemRequestOptions
                {
                    DecryptionResultHandler = errorHandler
                });
            Assert.AreEqual(failureCount, 1);

            await encryptionContainerForRestrictedUser.ReadItemStreamAsync(
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                new EncryptionItemRequestOptions
                {
                    DecryptionResultHandler = errorHandler
                });
            Assert.AreEqual(failureCount, 2);
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuthAllowed()
        {
            User keyManagerUser = EncryptionTests.database.GetUser(Guid.NewGuid().ToString());
            await EncryptionTests.database.CreateUserAsync(keyManagerUser.Id);

            PermissionProperties keyManagerUserPermission = await keyManagerUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, EncryptionTests.keyContainer));

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            TestEncryptor encryptor = new TestEncryptor(dekProvider);
            CosmosClient clientForKeyManagerUser = TestCommon.CreateCosmosClient(keyManagerUserPermission.Token);

            Database databaseForKeyManagerUser = clientForKeyManagerUser.GetDatabase(EncryptionTests.database.Id);

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
                await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, EncryptionTests.dekId, new List<string>() { "/id" });
                Assert.Fail("Expected item creation with id specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }

            try
            {
                await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, EncryptionTests.dekId, new List<string>() { "/PK" });
                Assert.Fail("Expected item creation with PK specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }
        }

        [TestMethod]
        public async Task EncryptionBulkCrud()
        {
            TestDoc docToReplace = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);
            docToReplace.NonSensitive = Guid.NewGuid().ToString();
            docToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToUpsert = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);
            docToUpsert.NonSensitive = Guid.NewGuid().ToString();
            docToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            CosmosClient clientWithBulk = TestCommon.CreateCosmosClient(builder => builder
                .WithBulkExecution(true)
                .Build());

            Database databaseWithBulk = clientWithBulk.GetDatabase(EncryptionTests.database.Id);
            Container containerWithBulk = databaseWithBulk.GetContainer(EncryptionTests.itemContainer.Id);
            Container encryptionContainerWithBulk = containerWithBulk.WithEncryptor(EncryptionTests.encryptor);

            List<Task> tasks = new List<Task>()
            {
                EncryptionTests.CreateItemAsync(encryptionContainerWithBulk, EncryptionTests.dekId, TestDoc.PathsToEncrypt),
                EncryptionTests.UpsertItemAsync(encryptionContainerWithBulk, TestDoc.Create(), EncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.Created),
                EncryptionTests.ReplaceItemAsync(encryptionContainerWithBulk, docToReplace, EncryptionTests.dekId, TestDoc.PathsToEncrypt),
                EncryptionTests.UpsertItemAsync(encryptionContainerWithBulk, docToUpsert, EncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.OK),
                EncryptionTests.DeleteItemAsync(encryptionContainerWithBulk, docToDelete)
            };

            await Task.WhenAll(tasks);
        }

        [TestMethod]
        public async Task EncryptionTransactionBatchCrud()
        {
            string partitionKey = "thePK";
            string dek1 = EncryptionTests.dekId;
            string dek2 = "dek2Forbatch";
            await EncryptionTests.CreateDekAsync(EncryptionTests.dekProvider, dek2);

            TestDoc doc1ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc2ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc3ToCreate = TestDoc.Create(partitionKey);

            ItemResponse<TestDoc> doc1ToReplaceCreateResponse = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            TestDoc doc1ToReplace = doc1ToReplaceCreateResponse.Resource;
            doc1ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc1ToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc2ToReplace = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt, partitionKey);
            doc2ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc2ToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc1ToUpsert = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt, partitionKey);
            doc1ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc1ToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc2ToUpsert = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            doc2ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc2ToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await EncryptionTests.CreateItemAsync(EncryptionTests.encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);

            TransactionalBatchResponse batchResponse = await EncryptionTests.encryptionContainer.CreateTransactionalBatch(new Cosmos.PartitionKey(partitionKey))
                .CreateItem(doc1ToCreate, EncryptionTests.GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt))
                .CreateItemStream(doc2ToCreate.ToStream(), EncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
                .ReplaceItem(doc1ToReplace.Id, doc1ToReplace, EncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt, doc1ToReplaceCreateResponse.ETag))
                .CreateItem(doc3ToCreate)
                .ReplaceItemStream(doc2ToReplace.Id, doc2ToReplace.ToStream(), EncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
                .UpsertItem(doc1ToUpsert, EncryptionTests.GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt))
                .DeleteItem(docToDelete.Id)
                .UpsertItemStream(doc2ToUpsert.ToStream(), EncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.encryptionContainer, doc1ToCreate);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.encryptionContainer, doc2ToCreate);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.encryptionContainer, doc3ToCreate);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.encryptionContainer, doc1ToReplace);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.encryptionContainer, doc2ToReplace);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.encryptionContainer, doc1ToUpsert);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.encryptionContainer, doc2ToUpsert);

            ResponseMessage readResponseMessage = await EncryptionTests.encryptionContainer.ReadItemStreamAsync(docToDelete.Id, new PartitionKey(docToDelete.PK));
            Assert.AreEqual(HttpStatusCode.NotFound, readResponseMessage.StatusCode);

            // Validate that the documents are encrypted as expected by trying to retrieve through regular (non-encryption) container
            doc1ToCreate.Sensitive = null;
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, doc1ToCreate);

            doc2ToCreate.Sensitive = null;
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, doc2ToCreate);

            // doc3ToCreate wasn't encrypted
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, doc3ToCreate);

            doc1ToReplace.Sensitive = null;
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, doc1ToReplace);

            doc2ToReplace.Sensitive = null;
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, doc2ToReplace);

            doc1ToUpsert.Sensitive = null;
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, doc1ToUpsert);

            doc2ToUpsert.Sensitive = null;
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, doc2ToUpsert);
        }

        private static async Task ValidateSprocResultsAsync(Container container, TestDoc expectedDoc)
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
                await container.Scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);

            StoredProcedureExecuteResponse<TestDoc> sprocResponse = await container.Scripts.ExecuteStoredProcedureAsync<TestDoc>(
                sprocId,
                new PartitionKey(expectedDoc.PK),
                parameters: new dynamic[] { expectedDoc.Id });

            Assert.AreEqual(expectedDoc, sprocResponse.Resource);
        }

        // One of query or queryDefinition is to be passed in non-null
        private static async Task ValidateQueryResultsAsync(
            Container container,
            string query = null,
            TestDoc expectedDoc = null,
            QueryDefinition queryDefinition = null)
        {
            QueryRequestOptions requestOptions = expectedDoc != null
                ? new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey(expectedDoc.PK),
                }
                : null;

            FeedIterator<TestDoc> queryResponseIterator;
            if (query != null)
            {
                queryResponseIterator = container.GetItemQueryIterator<TestDoc>(query, requestOptions: requestOptions);
            }
            else
            {
                queryResponseIterator = container.GetItemQueryIterator<TestDoc>(queryDefinition, requestOptions: requestOptions);
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
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2,
            string query,
            QueryRequestOptions requestOptions = null)
        {
            FeedIterator<TestDoc> queryResponseIterator;

            if (query == null)
            {
                IOrderedQueryable<TestDoc> linqQueryable = container.GetItemLinqQueryable<TestDoc>();
                queryResponseIterator = container.ToEncryptionFeedIterator<TestDoc>(linqQueryable, requestOptions);
            }
            else
            {
                queryResponseIterator = container.GetItemQueryIterator<TestDoc>(query, requestOptions: requestOptions);
            }

            FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();
            Assert.AreEqual(null, readDocs.ContinuationToken);

            if (query == null)
            {
                Assert.IsTrue(readDocs.Count >= 2);
            }
            else
            {
                Assert.AreEqual(2, readDocs.Count);
            }

            for (int index = 0; index < readDocs.Count; index++)
            {
                if (readDocs.ElementAt(index).Id.Equals(testDoc1.Id))
                {
                    Assert.AreEqual(readDocs.ElementAt(index), testDoc1);
                }
                else if (readDocs.ElementAt(index).Id.Equals(testDoc2.Id))
                {
                    Assert.AreEqual(readDocs.ElementAt(index), testDoc2);
                }
            }
        }

        private static async Task ValidateQueryResponseAsync(Container container, string query)
        {
            FeedIterator feedIterator = container.GetItemQueryStreamIterator(query);
            while (feedIterator.HasMoreResults)
            {
                ResponseMessage response = await feedIterator.ReadNextAsync();
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.IsNull(response.ErrorMessage);
            }
        }

        private async Task ValidateChangeFeedIteratorResponse(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2,
            Action<DecryptionResult> DecryptionResultHandler = null)
        {
            FeedIterator<TestDoc> changeIterator = container.GetChangeFeedIterator<TestDoc>(
                continuationToken: null,
                new EncryptionChangeFeedRequestOptions()
                {
                    StartTime = DateTime.MinValue.ToUniversalTime(),
                    DecryptionResultHandler = DecryptionResultHandler
                });

            List<TestDoc> changeFeedReturnedDocs = new List<TestDoc>();
            while (changeIterator.HasMoreResults)
            {
                FeedResponse<TestDoc> testDocs = await changeIterator.ReadNextAsync();
                for (int index = 0; index < testDocs.Count; index++)
                {
                    if (testDocs.Resource.ElementAt(index).Id.Equals(testDoc1.Id) || testDocs.Resource.ElementAt(index).Id.Equals(testDoc2.Id))
                    {
                        changeFeedReturnedDocs.Add(testDocs.Resource.ElementAt(index));
                    }
                }
            }

            Assert.AreEqual(changeFeedReturnedDocs.Count, 2);
            Assert.AreEqual(testDoc1, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 2]);
            Assert.AreEqual(testDoc2, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 1]);
        }
        
        private async Task ValidateChangeFeedProcessorResponse(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2)
        {
            List<TestDoc> changeFeedReturnedDocs = new List<TestDoc>();
            ChangeFeedProcessor cfp = container.GetChangeFeedProcessorBuilder(
                "testCFP",
                (IReadOnlyCollection<TestDoc> changes, CancellationToken cancellationToken)
                =>
                {
                    changeFeedReturnedDocs.AddRange(changes);
                    return Task.CompletedTask;
                })
                //.WithInMemoryLeaseContainer()
                //.WithStartFromBeginning()
                .Build();

            await cfp.StartAsync();
            await Task.Delay(2000);
            await cfp.StopAsync();

            Assert.IsTrue(changeFeedReturnedDocs.Count >= 2);

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

        private static void ErrorHandler(DecryptionResult decryptionErrorDetails)
        {
            Assert.AreEqual(decryptionErrorDetails.Exception.Message, "Null DataEncryptionKey returned.");

            using (MemoryStream memoryStream = new MemoryStream(decryptionErrorDetails.EncryptedStream.ToArray()))
            {
                JObject itemJObj = TestCommon.FromStream<JObject>(memoryStream);
                JProperty encryptionPropertiesJProp = itemJObj.Property("_ei");
                Assert.IsNotNull(encryptionPropertiesJProp);
                Assert.AreEqual(itemJObj.Property("id").Value.ToString(), EncryptionTests.decryptionFailedDocId);
            }                
        }

        private static ItemRequestOptions GetItemRequestOptionsWithDecryptionResultHandler()
        {
            return new EncryptionItemRequestOptions
            {
                DecryptionResultHandler = EncryptionTests.ErrorHandler
            };
        }

        private static CosmosClient GetClient()
        {
            return TestCommon.CreateCosmosClient();
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
            Container container,
            TestDoc testDoc,
            string dekId,
            List<string> pathsToEncrypt,
            HttpStatusCode expectedStatusCode)
        {
            ItemResponse<TestDoc> upsertResponse = await container.UpsertItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                EncryptionTests.GetRequestOptions(dekId, pathsToEncrypt));
            Assert.AreEqual(expectedStatusCode, upsertResponse.StatusCode);
            Assert.AreEqual(testDoc, upsertResponse.Resource);
            return upsertResponse;
        }

        private static async Task<ItemResponse<TestDoc>> CreateItemAsync(
            Container container,
            string dekId,
            List<string> pathsToEncrypt,
            string partitionKey = null)
        {
            TestDoc testDoc = TestDoc.Create(partitionKey);
            ItemResponse<TestDoc> createResponse = await container.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                EncryptionTests.GetRequestOptions(dekId, pathsToEncrypt));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
            return createResponse;
        }

        private static async Task<ItemResponse<TestDoc>> ReplaceItemAsync(
            Container encryptedContainer,
            TestDoc testDoc,
            string dekId,
            List<string> pathsToEncrypt,
            string etag = null)
        {
            ItemResponse<TestDoc> replaceResponse = await encryptedContainer.ReplaceItemAsync(
                testDoc,
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                EncryptionTests.GetRequestOptions(dekId, pathsToEncrypt, etag));

            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            Assert.AreEqual(testDoc, replaceResponse.Resource);
            return replaceResponse;
        }

        private static async Task<ItemResponse<TestDoc>> DeleteItemAsync(
            Container encryptedContainer,
            TestDoc testDoc)
        {
            ItemResponse<TestDoc> deleteResponse = await encryptedContainer.DeleteItemAsync<TestDoc>(
                testDoc.Id,
                new PartitionKey(testDoc.PK));

            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
            Assert.IsNull(deleteResponse.Resource);
            return deleteResponse;
        }

        private static EncryptionItemRequestOptions GetRequestOptions(
            string dekId,
            List<string> pathsToEncrypt,
            string ifMatchEtag = null)
        {
            return new EncryptionItemRequestOptions
            {
                EncryptionOptions = EncryptionTests.GetEncryptionOptions(dekId, pathsToEncrypt),
                IfMatchEtag = ifMatchEtag
            };
        }

        private static TransactionalBatchItemRequestOptions GetBatchItemRequestOptions(
            string dekId,
            List<string> pathsToEncrypt,
            string ifMatchEtag = null)
        {
            return new EncryptionTransactionalBatchItemRequestOptions
            {
                EncryptionOptions = EncryptionTests.GetEncryptionOptions(dekId, pathsToEncrypt),
                IfMatchEtag = ifMatchEtag
            };
        }

        private static Encryption.EncryptionOptions GetEncryptionOptions(
            string dekId,
            List<string> pathsToEncrypt)
        {
            return new Encryption.EncryptionOptions()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = pathsToEncrypt
            };
        }

        private static async Task VerifyItemByReadStreamAsync(Container container, TestDoc testDoc, ItemRequestOptions requestOptions = null)
        {
            ResponseMessage readResponseMessage = await container.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK), requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, readResponseMessage.StatusCode);
            Assert.IsNotNull(readResponseMessage.Content);
            TestDoc readDoc = TestCommon.FromStream<TestDoc>(readResponseMessage.Content);
            Assert.AreEqual(testDoc, readDoc);
        }

        private static async Task VerifyItemByReadAsync(Container container, TestDoc testDoc, ItemRequestOptions requestOptions = null)
        {
            ItemResponse<TestDoc> readResponse = await container.ReadItemAsync<TestDoc>(testDoc.Id, new PartitionKey(testDoc.PK), requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
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
                return TestCommon.ToStream(this);
            }
        }

        private class TestKeyWrapProvider : EncryptionKeyWrapProvider
        {
            public int UnwrapCallCount { get; set; }
            public TimeSpan cacheTTL { get; set; }

            public TestKeyWrapProvider()
            {
                this.cacheTTL = TimeSpan.FromDays(1);
                this.UnwrapCallCount = 0;
            }

            public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(byte[] wrappedKey, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken)
            {
                this.UnwrapCallCount++;
                int moveBy = metadata.Value == EncryptionTests.metadata1.Value + EncryptionTests.metadataUpdateSuffix ? 1 : 2;
                return Task.FromResult(
                    new EncryptionKeyUnwrapResult(
                        wrappedKey.Select(b => (byte)(b - moveBy)).ToArray(), 
                        this.cacheTTL));
            }

            public override Task<EncryptionKeyWrapResult> WrapKeyAsync(byte[] key, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken)
            {
                EncryptionKeyWrapMetadata responseMetadata = new EncryptionKeyWrapMetadata(metadata.Value + EncryptionTests.metadataUpdateSuffix);
                int moveBy = metadata.Value == EncryptionTests.metadata1.Value ? 1 : 2;
                return Task.FromResult(new EncryptionKeyWrapResult(key.Select(b => (byte)(b + moveBy)).ToArray(), responseMetadata));
            }
        }

        // This class is same as CosmosEncryptor but copied so as to induce decryption failure easily for testing.
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
                    throw new InvalidOperationException($"Null {nameof(DataEncryptionKey)} returned.");
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

            protected override void Dispose(bool disposing)
            {
                throw new NotImplementedException();
            }
        }
    }
}
