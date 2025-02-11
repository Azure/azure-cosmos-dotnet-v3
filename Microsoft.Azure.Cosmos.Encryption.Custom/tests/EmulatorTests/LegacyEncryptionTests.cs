//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests.Utils;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using EncryptionKeyWrapMetadata = EncryptionKeyWrapMetadata;

#pragma warning disable CS0618 // Type or member is obsolete

    [TestClass]
    public class LegacyEncryptionTests
    {
        private static readonly EncryptionKeyWrapMetadata metadata1 = new("metadata1");
        private static readonly EncryptionKeyWrapMetadata metadata2 = new("metadata2");
        private const string metadataUpdateSuffix = "updated";
        private static readonly TimeSpan cacheTTL = TimeSpan.FromDays(1);
        private const string dekId = "mydek";
        private static CosmosClient client;
        private static Database database;
        private static DataEncryptionKeyProperties dekProperties;
        private static Container itemContainer;
        private static Container encryptionContainer;
        private static Container keyContainer;
        private static TestKeyWrapProvider testKeyWrapProvider;
        private static CosmosDataEncryptionKeyProvider dekProvider;
        private static TestEncryptor encryptor;

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            _ = context;
            testKeyWrapProvider = new TestKeyWrapProvider();
            dekProvider = new CosmosDataEncryptionKeyProvider(testKeyWrapProvider);
            encryptor = new TestEncryptor(dekProvider);

            client = TestCommon.CreateCosmosClient();
            database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());

            keyContainer = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            await dekProvider.InitializeAsync(database, keyContainer.Id);

            itemContainer = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/PK", 400);
            encryptionContainer = itemContainer.WithEncryptor(encryptor);
            dekProperties = await CreateDekAsync(dekProvider, dekId);
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            if (database != null)
            {
                using (await database.DeleteStreamAsync()) { }
            }

            client?.Dispose();
        }

        [TestMethod]
        public async Task EncryptionCreateDek()
        {
            string dekId = "anotherDek";
            DataEncryptionKeyProperties dekProperties = await CreateDekAsync(LegacyEncryptionTests.dekProvider, dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(metadata1.Value + metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            // Use different DEK provider to avoid (unintentional) cache impact
            CosmosDataEncryptionKeyProvider dekProvider = new(new TestKeyWrapProvider());
            await dekProvider.InitializeAsync(database, keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        [TestMethod]
        public async Task EncryptionCreateDekWithMdeAlgorithmFails()
        {
            string dekId = "mdeDek";
            try
            {
                await CreateDekAsync(dekProvider, dekId, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);
                Assert.Fail();
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("For use of 'MdeAeadAes256CbcHmac256Randomized' algorithm, Encryptor or CosmosDataEncryptionKeyProvider needs to be initialized with EncryptionKeyStoreProvider.", ex.Message);
            }
        }

        [TestMethod]
        public async Task EncryptionRewrapDek()
        {
            string dekId = "randomDek";
            DataEncryptionKeyProperties dekProperties = await CreateDekAsync(LegacyEncryptionTests.dekProvider, dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(metadata1.Value + metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            ItemResponse<DataEncryptionKeyProperties> dekResponse = await LegacyEncryptionTests.dekProvider.DataEncryptionKeyContainer.RewrapDataEncryptionKeyAsync(
                dekId,
                metadata2);

            Assert.AreEqual(HttpStatusCode.OK, dekResponse.StatusCode);
            dekProperties = VerifyDekResponse(
                dekResponse,
                dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(metadata2.Value + metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            // Use different DEK provider to avoid (unintentional) cache impact
            CosmosDataEncryptionKeyProvider dekProvider = new(new TestKeyWrapProvider());
            await dekProvider.InitializeAsync(database, keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        [TestMethod]
        public async Task EncryptionRewrapDekEtagMismatch()
        {
            string dekId = "dummyDek";
            EncryptionKeyWrapMetadata newMetadata = new("newMetadata");

            DataEncryptionKeyProperties dekProperties = await CreateDekAsync(LegacyEncryptionTests.dekProvider, dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(metadata1.Value + metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            // modify dekProperties directly, which would lead to etag change
            DataEncryptionKeyProperties updatedDekProperties = new(
                dekProperties.Id,
                dekProperties.EncryptionAlgorithm,
                dekProperties.WrappedDataEncryptionKey,
                dekProperties.EncryptionKeyWrapMetadata,
                DateTime.UtcNow);
            await keyContainer.ReplaceItemAsync(
                updatedDekProperties,
                dekProperties.Id,
                new PartitionKey(dekProperties.Id));

            // rewrap should succeed, despite difference in cached value
            ItemResponse<DataEncryptionKeyProperties> dekResponse = await LegacyEncryptionTests.dekProvider.DataEncryptionKeyContainer.RewrapDataEncryptionKeyAsync(
                dekId,
                newMetadata);

            Assert.AreEqual(HttpStatusCode.OK, dekResponse.StatusCode);
            dekProperties = VerifyDekResponse(
                dekResponse,
                dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(newMetadata.Value + metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            Assert.AreEqual(2, testKeyWrapProvider.WrapKeyCallsCount[newMetadata.Value]);

            // Use different DEK provider to avoid (unintentional) cache impact
            CosmosDataEncryptionKeyProvider dekProvider = new(new TestKeyWrapProvider());
            await dekProvider.InitializeAsync(database, keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        [TestMethod]
        public async Task EncryptionDekReadFeed()
        {
            Container newKeyContainer = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            try
            {
                CosmosDataEncryptionKeyProvider dekProvider = new(new TestKeyWrapProvider());
                await dekProvider.InitializeAsync(database, newKeyContainer.Id);

                string contosoV1 = "Contoso_v001";
                string contosoV2 = "Contoso_v002";
                string fabrikamV1 = "Fabrikam_v001";
                string fabrikamV2 = "Fabrikam_v002";

                await CreateDekAsync(dekProvider, contosoV1);
                await CreateDekAsync(dekProvider, contosoV2);
                await CreateDekAsync(dekProvider, fabrikamV1);
                await CreateDekAsync(dekProvider, fabrikamV2);

                // Test getting all keys
                await IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: false,
                    "SELECT * from c");

                // Test getting specific subset of keys
                await IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV2 },
                    isExpectedDeksCompleteSetForRequest: false,
                    isResultOrderExpected: true,
                    "SELECT TOP 1 * from c where c.id >= 'Contoso_v000' and c.id <= 'Contoso_v999' ORDER BY c.id DESC");

                // Ensure only required results are returned
                QueryDefinition queryDefinition = new QueryDefinition("SELECT * from c where c.id >= @startId and c.id <= @endId ORDER BY c.id ASC")
                    .WithParameter("@startId", "Contoso_v000")
                    .WithParameter("@endId", "Contoso_v999");

                await IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: true,
                    query: null,
                    queryDefinition: queryDefinition);

                // Test pagination
                await IterateDekFeedAsync(
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
        public async Task EncryptionCreateItemWithoutEncryptionOptions()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await encryptionContainer.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithNullEncryptionOptions()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await encryptionContainer.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                new EncryptionItemRequestOptions());
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithoutPartitionKey()
        {
            TestDoc testDoc = TestDoc.Create();
            try
            {
                await encryptionContainer.CreateItemAsync(
                    testDoc,
                    requestOptions: GetRequestOptions(dekId, TestDoc.PathsToEncrypt));
                Assert.Fail("CreateItem should've failed because PartitionKey was not provided.");
            }
            catch (NotSupportedException ex)
            {
                Assert.AreEqual("partitionKey cannot be null for operations using EncryptionContainer.", ex.Message);
            }
        }

        [TestMethod]
        public async Task EncryptionFailsWithUnknownDek()
        {
            string unknownDek = "unknownDek";

            try
            {
                await CreateItemAsync(encryptionContainer, unknownDek, TestDoc.PathsToEncrypt);
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual($"Failed to retrieve Data Encryption Key with id: '{unknownDek}'.", ex.Message);
                Assert.IsTrue(ex.InnerException is CosmosException);
            }
        }

        [TestMethod]
        public async Task EncryptionCreateItem()
        {
            TestDoc testDoc = await CreateItemAsync(encryptionContainer, dekId, TestDoc.PathsToEncrypt);

            await VerifyItemByReadAsync(encryptionContainer, testDoc);

            await VerifyItemByReadStreamAsync(encryptionContainer, testDoc);

            TestDoc expectedDoc = new(testDoc);

#if SDKPROJECTREF
            // FIXME Remove the above once the binary encoding issue is fixed.
            // Read feed (null query)
            await LegacyEncryptionTests.ValidateQueryResultsAsync(
                LegacyEncryptionTests.encryptionContainer,
                query: null,
                expectedDoc);
#endif

            await ValidateQueryResultsAsync(
                encryptionContainer,
                "SELECT * FROM c",
                expectedDoc);

            await ValidateQueryResultsAsync(
                encryptionContainer,
                string.Format(
                    "SELECT * FROM c where c.PK = '{0}' and c.id = '{1}' and c.NonSensitive = '{2}'",
                    expectedDoc.PK,
                    expectedDoc.Id,
                    expectedDoc.NonSensitive),
                expectedDoc);

            await ValidateQueryResultsAsync(
                encryptionContainer,
                string.Format("SELECT * FROM c where c.Sensitive = '{0}'", testDoc.Sensitive),
                expectedDoc: null);

            await ValidateQueryResultsAsync(
                encryptionContainer,
                queryDefinition: new QueryDefinition(
                    "select * from c where c.id = @theId and c.PK = @thePK")
                         .WithParameter("@theId", expectedDoc.Id)
                         .WithParameter("@thePK", expectedDoc.PK),
                expectedDoc: expectedDoc);

            expectedDoc.Sensitive = null;

            await ValidateQueryResultsAsync(
                encryptionContainer,
                "SELECT c.id, c.PK, c.Sensitive, c.NonSensitive FROM c",
                expectedDoc);

            await ValidateQueryResultsAsync(
                encryptionContainer,
                "SELECT c.id, c.PK, c.NonSensitive FROM c",
                expectedDoc);

            await ValidateSprocResultsAsync(
                encryptionContainer,
                expectedDoc);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException), "Decryptable content is not initialized.")]
        public void ValidateDecryptableContent()
        {
            TestDoc testDoc = TestDoc.Create();
            EncryptableItem<TestDoc> encryptableItem = new(testDoc);
            encryptableItem.DecryptableItem.GetItemAsync<TestDoc>();
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithLazyDecryption()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<EncryptableItem<TestDoc>> createResponse = await encryptionContainer.CreateItemAsync(
                new EncryptableItem<TestDoc>(testDoc),
                new PartitionKey(testDoc.PK),
                GetRequestOptions(dekId, TestDoc.PathsToEncrypt));

            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.IsNotNull(createResponse.Resource);

            await ValidateDecryptableItem(createResponse.Resource.DecryptableItem, testDoc);

            // stream
            TestDoc testDoc1 = TestDoc.Create();
            ItemResponse<EncryptableItemStream> createResponseStream = await encryptionContainer.CreateItemAsync(
                new EncryptableItemStream(TestCommon.ToStream(testDoc1)),
                new PartitionKey(testDoc1.PK),
                GetRequestOptions(dekId, TestDoc.PathsToEncrypt));

            Assert.AreEqual(HttpStatusCode.Created, createResponseStream.StatusCode);
            Assert.IsNotNull(createResponseStream.Resource);

            await ValidateDecryptableItem(createResponseStream.Resource.DecryptableItem, testDoc1);
        }

        [TestMethod]
        public async Task EncryptionChangeFeedDecryptionSuccessful()
        {
            string dek2 = "dek2ForChangeFeed";
            await CreateDekAsync(dekProvider, dek2);

            TestDoc testDoc1 = await CreateItemAsync(encryptionContainer, dekId, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await CreateItemAsync(encryptionContainer, dek2, TestDoc.PathsToEncrypt);

            // change feed iterator
            await ValidateChangeFeedIteratorResponse(encryptionContainer, testDoc1, testDoc2);

            // change feed processor
            await ValidateChangeFeedProcessorResponse(encryptionContainer, testDoc1, testDoc2);
        }

        [TestMethod]
        public async Task EncryptionHandleDecryptionFailure()
        {
            string dek2 = "failDek";
            await CreateDekAsync(dekProvider, dek2);

            TestDoc testDoc1 = await CreateItemAsync(encryptionContainer, dek2, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await CreateItemAsync(encryptionContainer, dekId, TestDoc.PathsToEncrypt);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";

            // success
            await ValidateQueryResultsMultipleDocumentsAsync(encryptionContainer, testDoc1, testDoc2, query);

            // induce failure for one document
            encryptor.FailDecryption = true;
            testDoc1.Sensitive = null;

            FeedIterator<DecryptableItem> queryResponseIterator = encryptionContainer.GetItemQueryIterator<DecryptableItem>(query);
            FeedResponse<DecryptableItem> readDocsLazily = await queryResponseIterator.ReadNextAsync();
            await ValidateLazyDecryptionResponse(readDocsLazily.GetEnumerator(), dek2);

            // validate changeFeed handling
            FeedIterator<DecryptableItem> changeIterator = encryptionContainer.GetChangeFeedIterator<DecryptableItem>(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.Incremental);

            while (changeIterator.HasMoreResults)
            {
                readDocsLazily = await changeIterator.ReadNextAsync();
                if (readDocsLazily.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }

                if (readDocsLazily.Resource != null)
                {
                    await ValidateLazyDecryptionResponse(readDocsLazily.GetEnumerator(), dek2);
                }
            }

            // validate changeFeedProcessor handling
            Container leaseContainer = await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id: "leasesContainer", partitionKeyPath: "/id"));

            List<DecryptableItem> changeFeedReturnedDocs = new();
            ChangeFeedProcessor cfp = encryptionContainer.GetChangeFeedProcessorBuilder(
                "testCFPFailure",
                (IReadOnlyCollection<DecryptableItem> changes, CancellationToken cancellationToken) =>
                {
                    changeFeedReturnedDocs.AddRange(changes);
                    return Task.CompletedTask;
                })
                .WithInstanceName("dummy")
                .WithLeaseContainer(leaseContainer)
                .WithStartTime(DateTime.UtcNow.AddMinutes(-5))
                .Build();

            await cfp.StartAsync();
            await Task.Delay(2000);
            await cfp.StopAsync();

            Assert.IsTrue(changeFeedReturnedDocs.Count >= 2);
            await ValidateLazyDecryptionResponse(changeFeedReturnedDocs.GetEnumerator(), dek2);

            encryptor.FailDecryption = false;
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleDocs()
        {
            TestDoc testDoc1 = await CreateItemAsync(encryptionContainer, dekId, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await CreateItemAsync(encryptionContainer, dekId, TestDoc.PathsToEncrypt);

            // test GetItemLinqQueryable
            await ValidateQueryResultsMultipleDocumentsAsync(encryptionContainer, testDoc1, testDoc2, null);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";
            await ValidateQueryResultsMultipleDocumentsAsync(encryptionContainer, testDoc1, testDoc2, query);

            // ORDER BY query
            query += " ORDER BY c._ts";

            await ValidateQueryResultsMultipleDocumentsAsync(encryptionContainer, testDoc1, testDoc2, query);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleEncryptedProperties()
        {
            List<string> pathsEncrypted = new() { "/Sensitive", "/NonSensitive" };
            TestDoc testDoc = await CreateItemAsync(
                encryptionContainer,
                dekId,
                pathsEncrypted);

            TestDoc expectedDoc = new(testDoc);

            await ValidateQueryResultsAsync(
                encryptionContainer,
                "SELECT * FROM c",
                expectedDoc,
                pathsEncrypted: pathsEncrypted);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryValueResponse()
        {
            await CreateItemAsync(encryptionContainer, dekId, TestDoc.PathsToEncrypt);
            string query = "SELECT VALUE COUNT(1) FROM c";

            await ValidateQueryResponseAsync(encryptionContainer, query);
            await ValidateQueryResponseWithLazyDecryptionAsync(encryptionContainer, query);
        }

        [TestMethod]
        public async Task EncryptionDecryptGroupByQueryResultTest()
        {
            string partitionKey = Guid.NewGuid().ToString();

            await CreateItemAsync(encryptionContainer, dekId, TestDoc.PathsToEncrypt, partitionKey);
            await CreateItemAsync(encryptionContainer, dekId, TestDoc.PathsToEncrypt, partitionKey);

            string query = $"SELECT COUNT(c.Id), c.PK " +
                           $"FROM c WHERE c.PK = '{partitionKey}' " +
                           $"GROUP BY c.PK ";

            await ValidateQueryResponseAsync(encryptionContainer, query);
        }

        [TestMethod]
        public async Task EncryptionStreamIteratorValidation()
        {
            await CreateItemAsync(encryptionContainer, dekId, TestDoc.PathsToEncrypt);
            await CreateItemAsync(encryptionContainer, dekId, TestDoc.PathsToEncrypt);

            // test GetItemLinqQueryable with ToEncryptionStreamIterator extension
            await ValidateQueryResponseAsync(encryptionContainer);
        }

        [TestMethod]
        public async Task EncryptionRudItem()
        {
            TestDoc testDoc = await UpsertItemAsync(
                encryptionContainer,
                TestDoc.Create(),
                dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.Created);

            await VerifyItemByReadAsync(encryptionContainer, testDoc);

            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            ItemResponse<TestDoc> upsertResponse = await UpsertItemAsync(
                encryptionContainer,
                testDoc,
                dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.OK);
            TestDoc updatedDoc = upsertResponse.Resource;

            await VerifyItemByReadAsync(encryptionContainer, updatedDoc);

            updatedDoc.NonSensitive = Guid.NewGuid().ToString();
            updatedDoc.Sensitive = Guid.NewGuid().ToString();

            TestDoc replacedDoc = await ReplaceItemAsync(
                encryptionContainer,
                updatedDoc,
                dekId,
                TestDoc.PathsToEncrypt,
                upsertResponse.ETag);

            await VerifyItemByReadAsync(encryptionContainer, replacedDoc);

            await DeleteItemAsync(encryptionContainer, replacedDoc);
        }

        [TestMethod]
        public async Task EncryptionRudItemLazyDecryption()
        {
            TestDoc testDoc = TestDoc.Create();
            // Upsert (item doesn't exist)
            ItemResponse<EncryptableItem<TestDoc>> upsertResponse = await encryptionContainer.UpsertItemAsync(
                new EncryptableItem<TestDoc>(testDoc),
                new PartitionKey(testDoc.PK),
                GetRequestOptions(dekId, TestDoc.PathsToEncrypt));

            Assert.AreEqual(HttpStatusCode.Created, upsertResponse.StatusCode);
            Assert.IsNotNull(upsertResponse.Resource);

            await ValidateDecryptableItem(upsertResponse.Resource.DecryptableItem, testDoc);
            await VerifyItemByReadAsync(encryptionContainer, testDoc);

            // Upsert with stream (item exists)
            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            ItemResponse<EncryptableItemStream> upsertResponseStream = await encryptionContainer.UpsertItemAsync(
                new EncryptableItemStream(TestCommon.ToStream(testDoc)),
                new PartitionKey(testDoc.PK),
                GetRequestOptions(dekId, TestDoc.PathsToEncrypt));

            Assert.AreEqual(HttpStatusCode.OK, upsertResponseStream.StatusCode);
            Assert.IsNotNull(upsertResponseStream.Resource);

            await ValidateDecryptableItem(upsertResponseStream.Resource.DecryptableItem, testDoc);
            await VerifyItemByReadAsync(encryptionContainer, testDoc);

            // replace
            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            ItemResponse<EncryptableItemStream> replaceResponseStream = await encryptionContainer.ReplaceItemAsync(
                new EncryptableItemStream(TestCommon.ToStream(testDoc)),
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                GetRequestOptions(dekId, TestDoc.PathsToEncrypt, upsertResponseStream.ETag));

            Assert.AreEqual(HttpStatusCode.OK, replaceResponseStream.StatusCode);
            Assert.IsNotNull(replaceResponseStream.Resource);

            await ValidateDecryptableItem(replaceResponseStream.Resource.DecryptableItem, testDoc);
            await VerifyItemByReadAsync(encryptionContainer, testDoc);

            await DeleteItemAsync(encryptionContainer, testDoc);
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuthRestricted()
        {
            TestDoc testDoc = await CreateItemAsync(encryptionContainer, dekId, TestDoc.PathsToEncrypt);

            User restrictedUser = database.GetUser(Guid.NewGuid().ToString());
            await database.CreateUserAsync(restrictedUser.Id);

            PermissionProperties restrictedUserPermission = await restrictedUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, itemContainer));

            CosmosDataEncryptionKeyProvider dekProvider = new(new TestKeyWrapProvider());
            TestEncryptor encryptor = new(dekProvider);

            CosmosClient clientForRestrictedUser = TestCommon.CreateCosmosClient(
                restrictedUserPermission.Token);

            Database databaseForRestrictedUser = clientForRestrictedUser.GetDatabase(database.Id);
            Container containerForRestrictedUser = databaseForRestrictedUser.GetContainer(itemContainer.Id);
            Container encryptionContainerForRestrictedUser = containerForRestrictedUser.WithEncryptor(encryptor);

            await PerformForbiddenOperationAsync(() =>
                dekProvider.InitializeAsync(databaseForRestrictedUser, keyContainer.Id), "CosmosDekProvider.InitializeAsync");

            await PerformOperationOnUninitializedDekProviderAsync(() =>
                dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId), "DEK.ReadAsync");

            try
            {
                await encryptionContainerForRestrictedUser.ReadItemAsync<TestDoc>(testDoc.Id, new PartitionKey(testDoc.PK));
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual(ex.Message, "The CosmosDataEncryptionKeyProvider was not initialized.");
            }

            try
            {
                await encryptionContainerForRestrictedUser.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK));
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual(ex.Message, "The CosmosDataEncryptionKeyProvider was not initialized.");
            }
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuthAllowed()
        {
            User keyManagerUser = database.GetUser(Guid.NewGuid().ToString());
            await database.CreateUserAsync(keyManagerUser.Id);

            PermissionProperties keyManagerUserPermission = await keyManagerUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, keyContainer));

            CosmosDataEncryptionKeyProvider dekProvider = new(new TestKeyWrapProvider());
            TestEncryptor encryptor = new(dekProvider);
            CosmosClient clientForKeyManagerUser = TestCommon.CreateCosmosClient(keyManagerUserPermission.Token);

            Database databaseForKeyManagerUser = clientForKeyManagerUser.GetDatabase(database.Id);

            await dekProvider.InitializeAsync(databaseForKeyManagerUser, keyContainer.Id);

            DataEncryptionKeyProperties readDekProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readDekProperties);
        }

        [TestMethod]
        public async Task EncryptionRestrictedProperties()
        {
            try
            {
                await CreateItemAsync(encryptionContainer, dekId, new List<string>() { "/id" });
                Assert.Fail("Expected item creation with id specified to be encrypted to fail.");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("PathsToEncrypt includes a invalid path: '/id'.", ex.Message);
            }


            try
            {
                await CreateItemAsync(encryptionContainer, dekId, new List<string>() { "/PK" });
                Assert.Fail("Expected item creation with PK specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }
        }

        [TestMethod]
        public async Task EncryptionBulkCrud()
        {
            TestDoc docToReplace = await CreateItemAsync(encryptionContainer, dekId, TestDoc.PathsToEncrypt);
            docToReplace.NonSensitive = Guid.NewGuid().ToString();
            docToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToUpsert = await CreateItemAsync(encryptionContainer, dekId, TestDoc.PathsToEncrypt);
            docToUpsert.NonSensitive = Guid.NewGuid().ToString();
            docToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await CreateItemAsync(encryptionContainer, dekId, TestDoc.PathsToEncrypt);

            CosmosClient clientWithBulk = TestCommon.CreateCosmosClient(builder => builder
                .WithBulkExecution(true)
                .Build());

            Database databaseWithBulk = clientWithBulk.GetDatabase(database.Id);
            Container containerWithBulk = databaseWithBulk.GetContainer(itemContainer.Id);
            Container encryptionContainerWithBulk = containerWithBulk.WithEncryptor(encryptor);

            List<Task> tasks = new()
            {
                CreateItemAsync(encryptionContainerWithBulk, dekId, TestDoc.PathsToEncrypt),
                UpsertItemAsync(encryptionContainerWithBulk, TestDoc.Create(), dekId, TestDoc.PathsToEncrypt, HttpStatusCode.Created),
                ReplaceItemAsync(encryptionContainerWithBulk, docToReplace, dekId, TestDoc.PathsToEncrypt),
                UpsertItemAsync(encryptionContainerWithBulk, docToUpsert, dekId, TestDoc.PathsToEncrypt, HttpStatusCode.OK),
                DeleteItemAsync(encryptionContainerWithBulk, docToDelete)
            };

            await Task.WhenAll(tasks);
        }

        [TestMethod]
        public async Task EncryptionTransactionalBatchCrud()
        {
            string partitionKey = "thePK";
            string dek1 = dekId;
            string dek2 = "dek2Forbatch";
            await CreateDekAsync(dekProvider, dek2);

            TestDoc doc1ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc2ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc3ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc4ToCreate = TestDoc.Create(partitionKey);

            ItemResponse<TestDoc> doc1ToReplaceCreateResponse = await CreateItemAsync(encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            TestDoc doc1ToReplace = doc1ToReplaceCreateResponse.Resource;
            doc1ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc1ToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc2ToReplace = await CreateItemAsync(encryptionContainer, dek2, TestDoc.PathsToEncrypt, partitionKey);
            doc2ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc2ToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc1ToUpsert = await CreateItemAsync(encryptionContainer, dek2, TestDoc.PathsToEncrypt, partitionKey);
            doc1ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc1ToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc2ToUpsert = await CreateItemAsync(encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            doc2ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc2ToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await CreateItemAsync(encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);

            TransactionalBatchResponse batchResponse = await encryptionContainer.CreateTransactionalBatch(new PartitionKey(partitionKey))
                .CreateItem(doc1ToCreate, GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt))
                .CreateItemStream(doc2ToCreate.ToStream(), GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
                .ReplaceItem(doc1ToReplace.Id, doc1ToReplace, GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt, doc1ToReplaceCreateResponse.ETag))
                .CreateItem(doc3ToCreate)
                .CreateItem(doc4ToCreate, GetBatchItemRequestOptions(dek1, new List<string>())) // empty PathsToEncrypt list
                .ReplaceItemStream(doc2ToReplace.Id, doc2ToReplace.ToStream(), GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
                .UpsertItem(doc1ToUpsert, GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt))
                .DeleteItem(docToDelete.Id)
                .UpsertItemStream(doc2ToUpsert.ToStream(), GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);

            TransactionalBatchOperationResult<TestDoc> doc1 = batchResponse.GetOperationResultAtIndex<TestDoc>(0);
            Assert.AreEqual(doc1ToCreate, doc1.Resource);

            TransactionalBatchOperationResult<TestDoc> doc2 = batchResponse.GetOperationResultAtIndex<TestDoc>(1);
            Assert.AreEqual(doc2ToCreate, doc2.Resource);

            TransactionalBatchOperationResult<TestDoc> doc3 = batchResponse.GetOperationResultAtIndex<TestDoc>(2);
            Assert.AreEqual(doc1ToReplace, doc3.Resource);

            TransactionalBatchOperationResult<TestDoc> doc4 = batchResponse.GetOperationResultAtIndex<TestDoc>(3);
            Assert.AreEqual(doc3ToCreate, doc4.Resource);

            TransactionalBatchOperationResult<TestDoc> doc5 = batchResponse.GetOperationResultAtIndex<TestDoc>(4);
            Assert.AreEqual(doc4ToCreate, doc5.Resource);

            TransactionalBatchOperationResult<TestDoc> doc6 = batchResponse.GetOperationResultAtIndex<TestDoc>(5);
            Assert.AreEqual(doc2ToReplace, doc6.Resource);

            TransactionalBatchOperationResult<TestDoc> doc7 = batchResponse.GetOperationResultAtIndex<TestDoc>(6);
            Assert.AreEqual(doc1ToUpsert, doc7.Resource);

            TransactionalBatchOperationResult<TestDoc> doc8 = batchResponse.GetOperationResultAtIndex<TestDoc>(8);
            Assert.AreEqual(doc2ToUpsert, doc8.Resource);

            await VerifyItemByReadAsync(encryptionContainer, doc1ToCreate);
            await VerifyItemByReadAsync(encryptionContainer, doc2ToCreate, dekId: dek2);
            await VerifyItemByReadAsync(encryptionContainer, doc3ToCreate, isDocDecrypted: false);
            await VerifyItemByReadAsync(encryptionContainer, doc4ToCreate, isDocDecrypted: false);
            await VerifyItemByReadAsync(encryptionContainer, doc1ToReplace, dekId: dek2);
            await VerifyItemByReadAsync(encryptionContainer, doc2ToReplace, dekId: dek2);
            await VerifyItemByReadAsync(encryptionContainer, doc1ToUpsert);
            await VerifyItemByReadAsync(encryptionContainer, doc2ToUpsert, dekId: dek2);

            ResponseMessage readResponseMessage = await encryptionContainer.ReadItemStreamAsync(docToDelete.Id, new PartitionKey(docToDelete.PK));
            Assert.AreEqual(HttpStatusCode.NotFound, readResponseMessage.StatusCode);

            // Validate that the documents are encrypted as expected by trying to retrieve through regular (non-encryption) container
            doc1ToCreate.Sensitive = null;
            await VerifyItemByReadAsync(itemContainer, doc1ToCreate);

            doc2ToCreate.Sensitive = null;
            await VerifyItemByReadAsync(itemContainer, doc2ToCreate);

            // doc3ToCreate, doc4ToCreate wasn't encrypted
            await VerifyItemByReadAsync(itemContainer, doc3ToCreate);
            await VerifyItemByReadAsync(itemContainer, doc4ToCreate);

            doc1ToReplace.Sensitive = null;
            await VerifyItemByReadAsync(itemContainer, doc1ToReplace);

            doc2ToReplace.Sensitive = null;
            await VerifyItemByReadAsync(itemContainer, doc2ToReplace);

            doc1ToUpsert.Sensitive = null;
            await VerifyItemByReadAsync(itemContainer, doc1ToUpsert);

            doc2ToUpsert.Sensitive = null;
            await VerifyItemByReadAsync(itemContainer, doc2ToUpsert);
        }

        [TestMethod]
        public async Task EncryptionTransactionalBatchWithCustomSerializer()
        {
            CustomSerializer customSerializer = new();
            CosmosClient clientWithCustomSerializer = TestCommon.CreateCosmosClient(builder => builder
                .WithCustomSerializer(customSerializer)
                .Build());

            Database databaseWithCustomSerializer = clientWithCustomSerializer.GetDatabase(database.Id);
            Container containerWithCustomSerializer = databaseWithCustomSerializer.GetContainer(itemContainer.Id);
            Container encryptionContainerWithCustomSerializer = containerWithCustomSerializer.WithEncryptor(encryptor);

            string partitionKey = "thePK";
            string dek1 = dekId;

            TestDoc doc1ToCreate = TestDoc.Create(partitionKey);

            ItemResponse<TestDoc> doc1ToReplaceCreateResponse = await CreateItemAsync(encryptionContainerWithCustomSerializer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            TestDoc doc1ToReplace = doc1ToReplaceCreateResponse.Resource;
            doc1ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc1ToReplace.Sensitive = Guid.NewGuid().ToString();

            TransactionalBatchResponse batchResponse = await encryptionContainerWithCustomSerializer.CreateTransactionalBatch(new PartitionKey(partitionKey))
                .CreateItem(doc1ToCreate, GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt))
                .ReplaceItem(doc1ToReplace.Id, doc1ToReplace, GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt, doc1ToReplaceCreateResponse.ETag))
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);
            // FromStream is called as part of CreateItem request
            Assert.AreEqual(1, customSerializer.FromStreamCalled);

            TransactionalBatchOperationResult<TestDoc> doc1 = batchResponse.GetOperationResultAtIndex<TestDoc>(0);
            Assert.AreEqual(doc1ToCreate, doc1.Resource);
            Assert.AreEqual(2, customSerializer.FromStreamCalled);

            TransactionalBatchOperationResult<TestDoc> doc2 = batchResponse.GetOperationResultAtIndex<TestDoc>(1);
            Assert.AreEqual(doc1ToReplace, doc2.Resource);
            Assert.AreEqual(3, customSerializer.FromStreamCalled);

            await VerifyItemByReadAsync(encryptionContainerWithCustomSerializer, doc1ToCreate);
            await VerifyItemByReadAsync(encryptionContainerWithCustomSerializer, doc1ToReplace);

            // Validate that the documents are encrypted as expected by trying to retrieve through regular (non-encryption) container
            doc1ToCreate.Sensitive = null;
            await VerifyItemByReadAsync(itemContainer, doc1ToCreate);

            doc1ToReplace.Sensitive = null;
            await VerifyItemByReadAsync(itemContainer, doc1ToReplace);
        }

        [TestMethod]
        public async Task VerifyDekOperationWithSystemTextSerializer()
        {
            System.Text.Json.JsonSerializerOptions jsonSerializerOptions = new()
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer = new(jsonSerializerOptions);

            CosmosClient clientWithCosmosSystemTextJsonSerializer = TestCommon.CreateCosmosClient(builder => builder
                .WithCustomSerializer(cosmosSystemTextJsonSerializer)
                .Build());

            // get database and container
            Database databaseWithCosmosSystemTextJsonSerializer = clientWithCosmosSystemTextJsonSerializer.GetDatabase(database.Id);
            Container containerWithCosmosSystemTextJsonSerializer = databaseWithCosmosSystemTextJsonSerializer.GetContainer(itemContainer.Id);

            // create the Dek container
            Container dekContainerWithCosmosSystemTextJsonSerializer = await databaseWithCosmosSystemTextJsonSerializer.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);

            CosmosDataEncryptionKeyProvider dekProviderWithCosmosSystemTextJsonSerializer = new(new TestKeyWrapProvider());
            await dekProviderWithCosmosSystemTextJsonSerializer.InitializeAsync(databaseWithCosmosSystemTextJsonSerializer, dekContainerWithCosmosSystemTextJsonSerializer.Id);

            TestEncryptor encryptorWithCosmosSystemTextJsonSerializer = new(dekProviderWithCosmosSystemTextJsonSerializer);

            // enable encryption on container
            Container encryptionContainerWithCosmosSystemTextJsonSerializer = containerWithCosmosSystemTextJsonSerializer.WithEncryptor(encryptorWithCosmosSystemTextJsonSerializer);

            string dekId = "dekWithSystemTextJson";
            DataEncryptionKeyProperties dekProperties = await CreateDekAsync(dekProviderWithCosmosSystemTextJsonSerializer, dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(metadata1.Value + metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            // Use different DEK provider to avoid (unintentional) cache impact
            CosmosDataEncryptionKeyProvider dekProvider = new(new TestKeyWrapProvider());
            await dekProvider.InitializeAsync(databaseWithCosmosSystemTextJsonSerializer, dekContainerWithCosmosSystemTextJsonSerializer.Id);
            DataEncryptionKeyProperties readProperties = await dekProviderWithCosmosSystemTextJsonSerializer.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);

            // rewrap
            ItemResponse<DataEncryptionKeyProperties> dekResponse = await dekProviderWithCosmosSystemTextJsonSerializer.DataEncryptionKeyContainer.RewrapDataEncryptionKeyAsync(
               dekId,
               metadata2);

            Assert.AreEqual(HttpStatusCode.OK, dekResponse.StatusCode);
            dekProperties = VerifyDekResponse(
                dekResponse,
                dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(metadata2.Value + metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            readProperties = await dekProviderWithCosmosSystemTextJsonSerializer.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);

            TestDocSystemText testDocSystemText = new()
            {
                Id = Guid.NewGuid().ToString(),
                ActivityId = Guid.NewGuid().ToString(),
                PartitionKey = "myPartitionKey",
                Status = "Active"
            };

            // Create items that use System.Text.Json serialization attributes
            ItemResponse<TestDocSystemText> createTestDoc = await encryptionContainerWithCosmosSystemTextJsonSerializer.CreateItemAsync(
                testDocSystemText,
                new PartitionKey(testDocSystemText.PartitionKey),
                GetRequestOptions(dekId, new List<string>() { "/status" }));

            Assert.AreEqual(HttpStatusCode.Created, createTestDoc.StatusCode);

            string contosoV1 = "Contoso_v001";
            string contosoV2 = "Contoso_v002";
            string fabrikamV1 = "Fabrikam_v001";
            string fabrikamV2 = "Fabrikam_v002";

            await CreateDekAsync(dekProviderWithCosmosSystemTextJsonSerializer, contosoV1);
            await CreateDekAsync(dekProviderWithCosmosSystemTextJsonSerializer, contosoV2);
            await CreateDekAsync(dekProviderWithCosmosSystemTextJsonSerializer, fabrikamV1);
            await CreateDekAsync(dekProviderWithCosmosSystemTextJsonSerializer, fabrikamV2);

            // Test getting all keys
            await IterateDekFeedAsync(
                dekProviderWithCosmosSystemTextJsonSerializer,
                new List<string> { dekId, contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                isExpectedDeksCompleteSetForRequest: true,
                isResultOrderExpected: false,
                "SELECT * from c");

            // Test getting specific subset of keys
            await IterateDekFeedAsync(
                dekProviderWithCosmosSystemTextJsonSerializer,
                new List<string> { contosoV2 },
                isExpectedDeksCompleteSetForRequest: false,
                isResultOrderExpected: true,
                "SELECT TOP 1 * from c where c.id >= 'Contoso_v000' and c.id <= 'Contoso_v999' ORDER BY c.id DESC");

            // Ensure only required results are returned
            await IterateDekFeedAsync(
                dekProviderWithCosmosSystemTextJsonSerializer,
                new List<string> { contosoV1, contosoV2 },
                isExpectedDeksCompleteSetForRequest: true,
                isResultOrderExpected: true,
                "SELECT * from c where c.id >= 'Contoso_v000' and c.id <= 'Contoso_v999' ORDER BY c.id ASC");

            // Test pagination
            await IterateDekFeedAsync(
                dekProviderWithCosmosSystemTextJsonSerializer,
                new List<string> { dekId, contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                isExpectedDeksCompleteSetForRequest: true,
                isResultOrderExpected: false,
                "SELECT * from c",
                itemCountInPage: 3);


            //clean up
            FeedIterator<TestDocSystemText> iterator = containerWithCosmosSystemTextJsonSerializer.GetItemQueryIterator<TestDocSystemText>();

            while (iterator.HasMoreResults)
            {
                FeedResponse<TestDocSystemText> feedResponse = await iterator.ReadNextAsync();
                foreach (TestDocSystemText testDoc in feedResponse)
                {
                    if (testDoc.Id == null)
                    {
                        continue;
                    }
                    await containerWithCosmosSystemTextJsonSerializer.DeleteItemAsync<TestDocSystemText>(testDoc.Id, new PartitionKey(testDoc.PartitionKey));
                }
            }
        }

        [TestMethod]
        public async Task EncryptionTransactionalBatchConflictResponse()
        {
            string partitionKey = "thePK";
            string dek1 = dekId;

            ItemResponse<TestDoc> doc1CreatedResponse = await CreateItemAsync(encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            TestDoc doc1ToCreateAgain = doc1CreatedResponse.Resource;
            doc1ToCreateAgain.NonSensitive = Guid.NewGuid().ToString();
            doc1ToCreateAgain.Sensitive = Guid.NewGuid().ToString();

            TransactionalBatchResponse batchResponse = await encryptionContainer.CreateTransactionalBatch(new PartitionKey(partitionKey))
                .CreateItem(doc1ToCreateAgain, GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt))
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.Conflict, batchResponse.StatusCode);
            Assert.AreEqual(1, batchResponse.Count);
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
            QueryDefinition queryDefinition = null,
            List<string> pathsEncrypted = null)
        {
            QueryRequestOptions requestOptions = expectedDoc != null
                ? new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey(expectedDoc.PK),
                }
                : null;

            FeedIterator<TestDoc> queryResponseIterator;
            FeedIterator<DecryptableItem> queryResponseIteratorForLazyDecryption;
            if (query != null)
            {
                queryResponseIterator = container.GetItemQueryIterator<TestDoc>(query, requestOptions: requestOptions);
                queryResponseIteratorForLazyDecryption = container.GetItemQueryIterator<DecryptableItem>(query, requestOptions: requestOptions);
            }
            else
            {
                queryResponseIterator = container.GetItemQueryIterator<TestDoc>(queryDefinition, requestOptions: requestOptions);
                queryResponseIteratorForLazyDecryption = container.GetItemQueryIterator<DecryptableItem>(queryDefinition, requestOptions: requestOptions);
            }

            FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();
            Assert.AreEqual(null, readDocs.ContinuationToken);

            FeedResponse<DecryptableItem> readDocsLazily = await queryResponseIteratorForLazyDecryption.ReadNextAsync();
            Assert.AreEqual(null, readDocsLazily.ContinuationToken);

            if (expectedDoc != null)
            {
                Assert.AreEqual(1, readDocs.Count);
                TestDoc readDoc = readDocs.Single();
                Assert.AreEqual(expectedDoc, readDoc);

                Assert.AreEqual(1, readDocsLazily.Count);
                await ValidateDecryptableItem(readDocsLazily.First(), expectedDoc, pathsEncrypted: pathsEncrypted);
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
            string query)
        {
            FeedIterator<TestDoc> queryResponseIterator;
            FeedIterator<DecryptableItem> queryResponseIteratorForLazyDecryption;

            if (query == null)
            {
                IOrderedQueryable<TestDoc> linqQueryable = container.GetItemLinqQueryable<TestDoc>();
                queryResponseIterator = container.ToEncryptionFeedIterator(linqQueryable);

                IOrderedQueryable<DecryptableItem> linqQueryableDecryptableItem = container.GetItemLinqQueryable<DecryptableItem>();
                queryResponseIteratorForLazyDecryption = container.ToEncryptionFeedIterator(linqQueryableDecryptableItem);
            }
            else
            {
                queryResponseIterator = container.GetItemQueryIterator<TestDoc>(query);
                queryResponseIteratorForLazyDecryption = container.GetItemQueryIterator<DecryptableItem>(query);
            }

            FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();
            Assert.AreEqual(null, readDocs.ContinuationToken);

            FeedResponse<DecryptableItem> readDocsLazily = await queryResponseIteratorForLazyDecryption.ReadNextAsync();
            Assert.AreEqual(null, readDocsLazily.ContinuationToken);

            if (query == null)
            {
                Assert.IsTrue(readDocs.Count >= 2);
                Assert.IsTrue(readDocsLazily.Count >= 2);
            }
            else
            {
                Assert.AreEqual(2, readDocs.Count);
                Assert.AreEqual(2, readDocsLazily.Count);
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

        private static async Task ValidateQueryResponseAsync(Container container,
            string query = null)
        {
            FeedIterator feedIterator;
            if (query == null)
            {
                IOrderedQueryable<TestDoc> linqQueryable = container.GetItemLinqQueryable<TestDoc>();
                feedIterator = container.ToEncryptionStreamIterator(linqQueryable);
            }
            else
            {
                feedIterator = container.GetItemQueryStreamIterator(query);
            }

            while (feedIterator.HasMoreResults)
            {
                ResponseMessage response = await feedIterator.ReadNextAsync();
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.IsNull(response.ErrorMessage);
            }
        }

        private static async Task ValidateQueryResponseWithLazyDecryptionAsync(Container container,
            string query = null)
        {
            FeedIterator<DecryptableItem> queryResponseIteratorForLazyDecryption = container.GetItemQueryIterator<DecryptableItem>(query);
            FeedResponse<DecryptableItem> readDocsLazily = await queryResponseIteratorForLazyDecryption.ReadNextAsync();
            Assert.AreEqual(null, readDocsLazily.ContinuationToken);
            Assert.AreEqual(1, readDocsLazily.Count);
            (dynamic readDoc, DecryptionContext decryptionContext) = await readDocsLazily.First().GetItemAsync<dynamic>();
            Assert.IsTrue((long)readDoc >= 1);
            Assert.IsNull(decryptionContext);
        }

        private static async Task ValidateChangeFeedIteratorResponse(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2)
        {
            FeedIterator<TestDoc> changeIterator = container.GetChangeFeedIterator<TestDoc>(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.Incremental);

            List<TestDoc> changeFeedReturnedDocs = new();
            while (changeIterator.HasMoreResults)
            {
                FeedResponse<TestDoc> testDocs = await changeIterator.ReadNextAsync();
                if (testDocs.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }

                for (int index = 0; index < testDocs.Count; index++)
                {
                    if (testDocs.Resource.ElementAt(index).Id.Equals(testDoc1.Id) || testDocs.Resource.ElementAt(index).Id.Equals(testDoc2.Id))
                    {
                        changeFeedReturnedDocs.Add(testDocs.Resource.ElementAt(index));
                    }
                }
            }

            Assert.AreEqual(changeFeedReturnedDocs.Count, 2);
            Assert.AreEqual(testDoc1, changeFeedReturnedDocs[^2]);
            Assert.AreEqual(testDoc2, changeFeedReturnedDocs[^1]);
        }

        private static async Task ValidateChangeFeedProcessorResponse(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2)
        {
            Container leaseContainer = await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id: "leases", partitionKeyPath: "/id"));

            List<TestDoc> changeFeedReturnedDocs = new();
            ChangeFeedProcessor cfp = container.GetChangeFeedProcessorBuilder(
                "testCFP",
                (IReadOnlyCollection<TestDoc> changes, CancellationToken cancellationToken)
                =>
                {
                    changeFeedReturnedDocs.AddRange(changes);
                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithLeaseContainer(leaseContainer)
                .WithStartTime(DateTime.UtcNow.AddMinutes(-5))
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

        private static async Task ValidateLazyDecryptionResponse(
            IEnumerator<DecryptableItem> readDocsLazily,
            string failureDek)
        {
            int decryptedDoc = 0;
            int failedDoc = 0;

            while (readDocsLazily.MoveNext())
            {
                try
                {
                    (_, _) = await readDocsLazily.Current.GetItemAsync<dynamic>();
                    decryptedDoc++;
                }
                catch (EncryptionException encryptionException)
                {
                    failedDoc++;
                    ValidateEncryptionException(encryptionException, failureDek);
                }
            }

            Assert.IsTrue(decryptedDoc >= 1);
            Assert.AreEqual(1, failedDoc);
        }

        private static void ValidateEncryptionException(
            EncryptionException encryptionException,
            string failureDek)
        {
            Assert.AreEqual(failureDek, encryptionException.DataEncryptionKeyId);
            Assert.IsNotNull(encryptionException.EncryptedContent);
            Assert.IsNotNull(encryptionException.InnerException);
            Assert.IsTrue(encryptionException.InnerException is InvalidOperationException);
            Assert.AreEqual(encryptionException.InnerException.Message, "Null DataEncryptionKey returned.");
        }

        private static async Task IterateDekFeedAsync(
            CosmosDataEncryptionKeyProvider dekProvider,
            List<string> expectedDekIds,
            bool isExpectedDeksCompleteSetForRequest,
            bool isResultOrderExpected,
            string query,
            int? itemCountInPage = null,
            QueryDefinition queryDefinition = null)
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

            FeedIterator<DataEncryptionKeyProperties> dekIterator = queryDefinition != null
                ? dekProvider.DataEncryptionKeyContainer.GetDataEncryptionKeyQueryIterator<DataEncryptionKeyProperties>(
                    queryDefinition,
                    requestOptions: requestOptions)
                : dekProvider.DataEncryptionKeyContainer.GetDataEncryptionKeyQueryIterator<DataEncryptionKeyProperties>(
                    query,
                    requestOptions: requestOptions);

            Assert.IsTrue(dekIterator.HasMoreResults);

            List<string> readDekIds = new();
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
                GetRequestOptions(dekId, pathsToEncrypt));
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
                GetRequestOptions(dekId, pathsToEncrypt));
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
                GetRequestOptions(dekId, pathsToEncrypt, etag));

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
                EncryptionOptions = GetEncryptionOptions(dekId, pathsToEncrypt),
                IfMatchEtag = ifMatchEtag
            };
        }

        private static EncryptionTransactionalBatchItemRequestOptions GetBatchItemRequestOptions(
            string dekId,
            List<string> pathsToEncrypt,
            string ifMatchEtag = null)
        {
            return new EncryptionTransactionalBatchItemRequestOptions
            {
                EncryptionOptions = GetEncryptionOptions(dekId, pathsToEncrypt),
                IfMatchEtag = ifMatchEtag
            };
        }

        private static EncryptionOptions GetEncryptionOptions(
            string dekId,
            List<string> pathsToEncrypt)
        {
            return new EncryptionOptions()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = pathsToEncrypt
            };
        }

        private static async Task ValidateDecryptableItem(
            DecryptableItem decryptableItem,
            TestDoc testDoc,
            string dekId = null,
            List<string> pathsEncrypted = null,
            bool isDocDecrypted = true)
        {
            (TestDoc readDoc, DecryptionContext decryptionContext) = await decryptableItem.GetItemAsync<TestDoc>();
            Assert.AreEqual(testDoc, readDoc);
            if (isDocDecrypted && testDoc.Sensitive != null)
            {
                ValidateDecryptionContext(decryptionContext, dekId, pathsEncrypted);
            }
            else
            {
                Assert.IsNull(decryptionContext);
            }
        }

        private static void ValidateDecryptionContext(
            DecryptionContext decryptionContext,
            string dekId = null,
            List<string> pathsEncrypted = null)
        {
            Assert.IsNotNull(decryptionContext.DecryptionInfoList);
            Assert.AreEqual(1, decryptionContext.DecryptionInfoList.Count);
            DecryptionInfo decryptionInfo = decryptionContext.DecryptionInfoList[0];
            Assert.AreEqual(dekId ?? LegacyEncryptionTests.dekId, decryptionInfo.DataEncryptionKeyId);

            pathsEncrypted ??= TestDoc.PathsToEncrypt;

            Assert.AreEqual(pathsEncrypted.Count, decryptionInfo.PathsDecrypted.Count);
            Assert.IsFalse(pathsEncrypted.Exists(path => !decryptionInfo.PathsDecrypted.Contains(path)));
        }

        private static async Task VerifyItemByReadStreamAsync(Container container, TestDoc testDoc, ItemRequestOptions requestOptions = null)
        {
            ResponseMessage readResponseMessage = await container.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK), requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, readResponseMessage.StatusCode);
            Assert.IsNotNull(readResponseMessage.Content);
            TestDoc readDoc = TestCommon.FromStream<TestDoc>(readResponseMessage.Content);
            Assert.AreEqual(testDoc, readDoc);
        }

        private static async Task VerifyItemByReadAsync(Container container, TestDoc testDoc, ItemRequestOptions requestOptions = null, string dekId = null, bool isDocDecrypted = true)
        {
            ItemResponse<TestDoc> readResponse = await container.ReadItemAsync<TestDoc>(testDoc.Id, new PartitionKey(testDoc.PK), requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.AreEqual(testDoc, readResponse.Resource);

            // ignore for reads via regular container..
            if (container == encryptionContainer)
            {
                ItemResponse<DecryptableItem> readResponseDecryptableItem = await container.ReadItemAsync<DecryptableItem>(testDoc.Id, new PartitionKey(testDoc.PK), requestOptions);
                Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
                await ValidateDecryptableItem(readResponseDecryptableItem.Resource, testDoc, dekId, isDocDecrypted: isDocDecrypted);
            }
        }

        private static async Task<DataEncryptionKeyProperties> CreateDekAsync(CosmosDataEncryptionKeyProvider dekProvider, string dekId, string algorithm = null)
        {
            ItemResponse<DataEncryptionKeyProperties> dekResponse = await dekProvider.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                dekId,
                algorithm ?? CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                metadata1);

            Assert.AreEqual(HttpStatusCode.Created, dekResponse.StatusCode);

            return VerifyDekResponse(dekResponse,
                dekId);
        }

        private static DataEncryptionKeyProperties VerifyDekResponse(
            ItemResponse<DataEncryptionKeyProperties> dekResponse,
            string dekId)
        {
            Assert.IsTrue(dekResponse.RequestCharge > 0);
            Assert.IsNotNull(dekResponse.ETag);

            DataEncryptionKeyProperties dekProperties = dekResponse.Resource;
            Assert.IsNotNull(dekProperties);
            Assert.AreEqual(dekResponse.ETag, dekProperties.ETag);
            Assert.AreEqual(dekId, dekProperties.Id);
            Assert.IsNotNull(dekProperties.SelfLink);
            Assert.IsNotNull(dekProperties.CreatedTime);
            Assert.IsNotNull(dekProperties.LastModified);

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

        public class TestDocSystemText
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string Id { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("PK")]
            public string PartitionKey { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("activityId")]
            public string ActivityId { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("status")]
            public string Status { get; set; }
        }

        private class TestKeyWrapProvider : EncryptionKeyWrapProvider
        {
            public Dictionary<string, int> WrapKeyCallsCount { get; private set; }

            public TestKeyWrapProvider()
            {
                this.WrapKeyCallsCount = new Dictionary<string, int>();
            }

            public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(byte[] wrappedKey, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken)
            {
                int moveBy = metadata.Value == metadata1.Value + metadataUpdateSuffix ? 1 : 2;
                return Task.FromResult(new EncryptionKeyUnwrapResult(wrappedKey.Select(b => (byte)(b - moveBy)).ToArray(), cacheTTL));
            }

            public override Task<EncryptionKeyWrapResult> WrapKeyAsync(byte[] key, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken)
            {
                if (!this.WrapKeyCallsCount.ContainsKey(metadata.Value))
                {
                    this.WrapKeyCallsCount[metadata.Value] = 1;
                }
                else
                {
                    this.WrapKeyCallsCount[metadata.Value]++;
                }

                EncryptionKeyWrapMetadata responseMetadata = new(metadata.Value + metadataUpdateSuffix);
                int moveBy = metadata.Value == metadata1.Value ? 1 : 2;
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

                DataEncryptionKey dek = await this.GetEncryptionKeyAsync(
                    dataEncryptionKeyId,
                    encryptionAlgorithm,
                    cancellationToken) ?? throw new InvalidOperationException($"Null {nameof(DataEncryptionKey)} returned from {nameof(this.DataEncryptionKeyProvider.FetchDataEncryptionKeyWithoutRawKeyAsync)}.");
                return dek.DecryptData(cipherText);
            }

            public override async Task<byte[]> EncryptAsync(
                byte[] plainText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                DataEncryptionKey dek = await this.GetEncryptionKeyAsync(
                    dataEncryptionKeyId,
                    encryptionAlgorithm,
                    cancellationToken);

                return dek.EncryptData(plainText);
            }

            public override async Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                return await this.DataEncryptionKeyProvider.FetchDataEncryptionKeyWithoutRawKeyAsync(
                    dataEncryptionKeyId,
                    encryptionAlgorithm,
                    cancellationToken);
            }
        }

        internal class CustomSerializer : CosmosSerializer
        {
            private readonly JsonSerializer serializer = new();
            public int FromStreamCalled = 0;

            public override T FromStream<T>(Stream stream)
            {
                this.FromStreamCalled++;
                using (StreamReader sr = new(stream))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    JsonSerializer serializer = new();
                    return this.serializer.Deserialize<T>(reader);
                }
            }

            public override Stream ToStream<T>(T input)
            {
                MemoryStream streamPayload = new();
                using (StreamWriter streamWriter = new(streamPayload, encoding: Encoding.UTF8, bufferSize: 1024, leaveOpen: true))
                {
                    using JsonTextWriter writer = new (streamWriter);
                    writer.Formatting = Formatting.None;
                    this.serializer.Serialize(writer, input);
                    writer.Flush();
                    streamWriter.Flush();
                }

                streamPayload.Position = 0;
                return streamPayload;
            }
        }

        internal class CosmosSystemTextJsonSerializer : CosmosSerializer
        {
            private readonly JsonObjectSerializer systemTextJsonSerializer;

            public CosmosSystemTextJsonSerializer(System.Text.Json.JsonSerializerOptions jsonSerializerOptions)
            {
                this.systemTextJsonSerializer = new JsonObjectSerializer(jsonSerializerOptions);
            }

            public override T FromStream<T>(Stream stream)
            {
                using (stream)
                {
                    if (stream.CanSeek
                           && stream.Length == 0)
                    {
                        return default;
                    }

                    if (typeof(Stream).IsAssignableFrom(typeof(T)))
                    {
                        return (T)(object)stream;
                    }

                    return (T)this.systemTextJsonSerializer.Deserialize(stream, typeof(T), default);
                }
            }

            public override Stream ToStream<T>(T input)
            {
                MemoryStream streamPayload = new();
                this.systemTextJsonSerializer.Serialize(streamPayload, input, typeof(T), default);
                streamPayload.Position = 0;
                return streamPayload;
            }
        }
    }

#pragma warning restore CS0618 // Type or member is obsolete
}
