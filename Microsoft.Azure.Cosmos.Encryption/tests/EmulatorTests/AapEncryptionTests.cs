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
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using global::Azure.Identity;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Data.AlwaysProtected.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class AapEncryptionTests
    {
        private static Uri masterKeyUri1 = new Uri("https://demo.keyvault.net/keys/samplekey1/03ded886623sss09bzc60351e536a111");
        private static Uri masterKeyUri2 = new Uri("https://demo.keyvault.net/keys/samplekey2/47d306aeaaeyyyaabs9467235460dc22");
        private static Uri masterKeyUri3 = new Uri("https://demo.keyvault.net/keys/samplekey3/525eff9690fxxxf9by96c30d4833c113");
        private static EncryptionKeyWrapMetadata metadata1;
        private static EncryptionKeyWrapMetadata metadata2;
        private static EncryptionKeyWrapMetadata metadata3;

        private const string dekId = "mydek";
        private static CosmosClient client;
        private static Database database;
        private static DataEncryptionKeyProperties dekProperties;
        private static Container itemContainer;
        private static Container encryptionContainer;
        private static Container keyContainer;
        
        private static AapCosmosEncryptor encryptor;
        private static AapTestEncryptor testEncryptor;
        private static string decryptionFailedDocId;

        private static CosmosDataEncryptionKeyProvider dekProvider;

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            metadata1 = new EncryptionKeyWrapMetadata(value:masterKeyUri1.ToString(), name: "sample", path:masterKeyUri1.ToString());
            metadata2 = new EncryptionKeyWrapMetadata(value:masterKeyUri2.ToString(), name: "sample1", path:masterKeyUri2.ToString());
            metadata3 = new EncryptionKeyWrapMetadata(value: masterKeyUri3.ToString(), name: "sample2", path: masterKeyUri3.ToString());

            AapEncryptionTests.dekProvider = new CosmosDataEncryptionKeyProvider(new TestAapEncryptionKeyStoreProvider());
            AapEncryptionTests.encryptor = new AapCosmosEncryptor(new TestAapEncryptionKeyStoreProvider());
            AapEncryptionTests.client = TestCommon.CreateCosmosClient();
            AapEncryptionTests.database = await AapEncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());

            AapEncryptionTests.keyContainer = await AapEncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            await AapEncryptionTests.dekProvider.InitializeAsync(AapEncryptionTests.database, AapEncryptionTests.keyContainer.Id);

            AapEncryptionTests.itemContainer = await AapEncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/PK", 400);
            await encryptor.InitializeAsync(AapEncryptionTests.database, AapEncryptionTests.keyContainer.Id);
            AapEncryptionTests.encryptionContainer = AapEncryptionTests.itemContainer.WithAapEncryptor(encryptor);
            AapEncryptionTests.dekProperties = await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, AapEncryptionTests.dekId);
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            if (AapEncryptionTests.database != null)
            {
                using (await AapEncryptionTests.database.DeleteStreamAsync()) { }
            }

            if (AapEncryptionTests.client != null)
            {
                AapEncryptionTests.client.Dispose();
            }
        }


        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleDocs()
        {

            TestDoc testDoc1 = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            // test GetItemLinqQueryable
            await AapEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(AapEncryptionTests.encryptionContainer, testDoc1, testDoc2, null);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";
            await AapEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(AapEncryptionTests.encryptionContainer, testDoc1, testDoc2, query);

            // ORDER BY query
            query = query + " ORDER BY c._ts";
            await AapEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(AapEncryptionTests.encryptionContainer, testDoc1, testDoc2, query);
        }

        [TestMethod]
        public async Task EncryptionCreateDek()
        {
            string dekId = "anotherDek";
            DataEncryptionKeyProperties dekProperties = await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dekId);
            Assert.AreEqual(
                AapEncryptionTests.metadata1,
                dekProperties.EncryptionKeyWrapMetadata);
            
            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestAapEncryptionKeyStoreProvider());
            await dekProvider.InitializeAsync(AapEncryptionTests.database, AapEncryptionTests.keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }  


        [TestMethod]
        public async Task EncryptionRewrapDek()
        {
            string dekId = "randomDek";
            DataEncryptionKeyProperties dekProperties = await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dekId);
            Assert.AreEqual(
                AapEncryptionTests.metadata1,
                dekProperties.EncryptionKeyWrapMetadata);

            ItemResponse<DataEncryptionKeyProperties> dekResponse = await AapEncryptionTests.dekProvider.DataEncryptionKeyContainer.RewrapDataEncryptionKeyAsync(
                dekId,
                AapEncryptionTests.metadata2);

            Assert.AreEqual(HttpStatusCode.OK, dekResponse.StatusCode);
            dekProperties = AapEncryptionTests.VerifyDekResponse(
                dekResponse,
                dekId);
            Assert.AreEqual(
                AapEncryptionTests.metadata2,
                dekProperties.EncryptionKeyWrapMetadata);
            
            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestAapEncryptionKeyStoreProvider());
            await dekProvider.InitializeAsync(AapEncryptionTests.database, AapEncryptionTests.keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        [TestMethod]
        public async Task EncryptionRewrapDekEtagMismatch()
        {
            string dekId = "dummyDek";
            //EncryptionKeyWrapMetadata newMetadata = new EncryptionKeyWrapMetadata("newMetadata");

            DataEncryptionKeyProperties dekProperties = await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dekId);
            Assert.AreEqual(
                AapEncryptionTests.metadata1,
                dekProperties.EncryptionKeyWrapMetadata);

            // modify dekProperties directly, which would lead to etag change
            DataEncryptionKeyProperties updatedDekProperties = new DataEncryptionKeyProperties(
                dekProperties.Id,
                dekProperties.EncryptionAlgorithm,
                dekProperties.WrappedDataEncryptionKey,
                dekProperties.EncryptionKeyWrapMetadata,
                DateTime.UtcNow);
            await AapEncryptionTests.keyContainer.ReplaceItemAsync<DataEncryptionKeyProperties>(
                updatedDekProperties,
                dekProperties.Id,
                new PartitionKey(dekProperties.Id));

            // rewrap should succeed, despite difference in cached value
            ItemResponse<DataEncryptionKeyProperties> dekResponse = await AapEncryptionTests.dekProvider.DataEncryptionKeyContainer.RewrapDataEncryptionKeyAsync(
                dekId,
                AapEncryptionTests.metadata3);

            Assert.AreEqual(HttpStatusCode.OK, dekResponse.StatusCode);
            dekProperties = AapEncryptionTests.VerifyDekResponse(
                dekResponse,
                dekId);
            Assert.AreEqual(
                AapEncryptionTests.metadata3,
                dekProperties.EncryptionKeyWrapMetadata);
            
            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestAapEncryptionKeyStoreProvider());
            await dekProvider.InitializeAsync(AapEncryptionTests.database, AapEncryptionTests.keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        [TestMethod]
        public async Task EncryptionDekReadFeed()
        {
            Container newKeyContainer = await AapEncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            try
            {                
                CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestAapEncryptionKeyStoreProvider());
                await dekProvider.InitializeAsync(AapEncryptionTests.database, newKeyContainer.Id);

                string contosoV1 = "Contoso_v001";
                string contosoV2 = "Contoso_v002";
                string fabrikamV1 = "Fabrikam_v001";
                string fabrikamV2 = "Fabrikam_v002";

                await AapEncryptionTests.CreateDekAsync(dekProvider, contosoV1);
                await AapEncryptionTests.CreateDekAsync(dekProvider, contosoV2);
                await AapEncryptionTests.CreateDekAsync(dekProvider, fabrikamV1);
                await AapEncryptionTests.CreateDekAsync(dekProvider, fabrikamV2);

                // Test getting all keys
                await AapEncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: false,
                    "SELECT * from c");

                // Test getting specific subset of keys
                await AapEncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV2 },
                    isExpectedDeksCompleteSetForRequest: false,
                    isResultOrderExpected: true,
                    "SELECT TOP 1 * from c where c.id >= 'Contoso_v000' and c.id <= 'Contoso_v999' ORDER BY c.id DESC");

                // Ensure only required results are returned
                await AapEncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: true,
                    "SELECT * from c where c.id >= 'Contoso_v000' and c.id <= 'Contoso_v999' ORDER BY c.id ASC");

                // Test pagination
                await AapEncryptionTests.IterateDekFeedAsync(
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
            ItemResponse<TestDoc> createResponse = await AapEncryptionTests.encryptionContainer.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithNullEncryptionOptions()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await AapEncryptionTests.encryptionContainer.CreateItemAsync(
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
                await AapEncryptionTests.encryptionContainer.CreateItemAsync(
                    testDoc,
                    requestOptions: AapEncryptionTests.GetRequestOptions(AapEncryptionTests.dekId, TestDoc.PathsToEncrypt));
                Assert.Fail("CreateItem should've failed because PartitionKey was not provided.");
            }
            catch (NotSupportedException ex)
            {
                Assert.AreEqual("partitionKey cannot be null for operations using AapContainer.", ex.Message);
            }
        }

        [TestMethod]
        public async Task EncryptionFailsWithUnknownDek()
        {
            string unknownDek = "unknownDek";

            try
            {
                await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, unknownDek, TestDoc.PathsToEncrypt);
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
            TestDoc testDoc = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, testDoc);

            await AapEncryptionTests.VerifyItemByReadStreamAsync(AapEncryptionTests.encryptionContainer, testDoc);

            TestDoc expectedDoc = new TestDoc(testDoc);

            // Read feed (null query)
            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                query: null,
                expectedDoc);

            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                "SELECT * FROM c",
                expectedDoc);

            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                string.Format(
                    "SELECT * FROM c where c.PK = '{0}' and c.id = '{1}' and c.NonSensitive = '{2}'",
                    expectedDoc.PK,
                    expectedDoc.Id,
                    expectedDoc.NonSensitive),
                expectedDoc);

            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                string.Format("SELECT * FROM c where c.Sensitive = '{0}'", testDoc.Sensitive),
                expectedDoc: null);

            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                queryDefinition: new QueryDefinition(
                    "select * from c where c.id = @theId and c.PK = @thePK")
                         .WithParameter("@theId", expectedDoc.Id)
                         .WithParameter("@thePK", expectedDoc.PK),
                expectedDoc: expectedDoc);

            expectedDoc.Sensitive = null;
            
            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                "SELECT c.id, c.PK, c.Sensitive, c.NonSensitive FROM c",
                expectedDoc);

            expectedDoc.Sensitive = null;

            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                "SELECT c.id, c.PK, c.NonSensitive FROM c",
                expectedDoc);

            expectedDoc.Sensitive = null;

            await AapEncryptionTests.ValidateSprocResultsAsync(
                AapEncryptionTests.encryptionContainer,
                expectedDoc);
        }

        [TestMethod]
        public async Task EncryptionChangeFeedDecryptionSuccessful()
        {
            string dek2 = "dek2ForChangeFeed";
            await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dek2);

            TestDoc testDoc1 = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt);
            
            // change feed iterator
            await this.ValidateChangeFeedIteratorResponse(AapEncryptionTests.encryptionContainer, testDoc1, testDoc2);

            // change feed processor
            // await this.ValidateChangeFeedProcessorResponse(EncryptionTests.encryptionContainer, testDoc1, testDoc2);
        }

        [TestMethod]
        public async Task EncryptionHandleDecryptionFailure()
        {
            string dek2 = "failDek";
            await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dek2);
            AapEncryptionTests.testEncryptor = new AapTestEncryptor(AapEncryptionTests.dekProvider);
            Container encryptionContainerTestEncryptor;
            encryptionContainerTestEncryptor = AapEncryptionTests.itemContainer.WithAapEncryptor(testEncryptor);
            TestDoc testDoc1 = await AapEncryptionTests.CreateItemAsync(encryptionContainerTestEncryptor, dek2, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await AapEncryptionTests.CreateItemAsync(encryptionContainerTestEncryptor, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";

            // success
            await AapEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(encryptionContainerTestEncryptor, testDoc1, testDoc2, query);

            // induce failure        
            
            AapEncryptionTests.testEncryptor.FailDecryption = true;

            AapEncryptionTests.decryptionFailedDocId = testDoc1.Id;
            testDoc1.Sensitive = null;

            await AapEncryptionTests.VerifyItemByReadAsync(
                encryptionContainerTestEncryptor,
                testDoc1,
                AapEncryptionTests.GetItemRequestOptionsWithDecryptionResultHandler());

            await AapEncryptionTests.VerifyItemByReadStreamAsync(
                encryptionContainerTestEncryptor,
                testDoc1,
                AapEncryptionTests.GetItemRequestOptionsWithDecryptionResultHandler());

            EncryptionQueryRequestOptions queryRequestOptions = new EncryptionQueryRequestOptions
            {
                DecryptionResultHandler = AapEncryptionTests.ErrorHandler
            };

            await AapEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(
                encryptionContainerTestEncryptor,
                testDoc1,
                testDoc2,
                query,
                queryRequestOptions);

            // GetItemLinqQueryable
            await AapEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(
                encryptionContainerTestEncryptor, 
                testDoc1, 
                testDoc2, 
                query: null,
                queryRequestOptions);

            await this.ValidateChangeFeedIteratorResponse(
                encryptionContainerTestEncryptor,
                testDoc1,
                testDoc2,
                AapEncryptionTests.ErrorHandler);

            // await this.ValidateChangeFeedProcessorResponse(EncryptionTests.itemContainerCore, testDoc1, testDoc2, false);
            AapEncryptionTests.testEncryptor.FailDecryption = false;
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleEncryptedProperties()
        {
            TestDoc testDoc = await AapEncryptionTests.CreateItemAsync(
                AapEncryptionTests.encryptionContainer,
                AapEncryptionTests.dekId,
                new List<string>() { "/Sensitive", "/NonSensitive" });

            TestDoc expectedDoc = new TestDoc(testDoc);

            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                "SELECT * FROM c",
                expectedDoc);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryValueResponse()
        {
            await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            string query = "SELECT VALUE COUNT(1) FROM c";

            await AapEncryptionTests.ValidateQueryResponseAsync(AapEncryptionTests.encryptionContainer, query);
        }

        [TestMethod]
        public async Task EncryptionDecryptGroupByQueryResultTest()
        {
            string partitionKey = Guid.NewGuid().ToString();

            await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt, partitionKey);
            await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt, partitionKey);

            string query = $"SELECT COUNT(c.Id), c.PK " +
                           $"FROM c WHERE c.PK = '{partitionKey}' " +
                           $"GROUP BY c.PK ";

            await AapEncryptionTests.ValidateQueryResponseAsync(AapEncryptionTests.encryptionContainer, query);
        }

        [TestMethod]
        public async Task EncryptionStreamIteratorValidation()
        {
            await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            await AapEncryptionTests.ValidateQueryResponseAsync(AapEncryptionTests.encryptionContainer);
        }

        [TestMethod]
        public async Task EncryptionRudItem()
        {
            TestDoc testDoc = await AapEncryptionTests.UpsertItemAsync(
                AapEncryptionTests.encryptionContainer,
                TestDoc.Create(),
                AapEncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.Created);

            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, testDoc);

            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            ItemResponse<TestDoc> upsertResponse = await AapEncryptionTests.UpsertItemAsync(
                AapEncryptionTests.encryptionContainer,
                testDoc,
                AapEncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.OK);
            TestDoc updatedDoc = upsertResponse.Resource;

            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, updatedDoc);

            updatedDoc.NonSensitive = Guid.NewGuid().ToString();
            updatedDoc.Sensitive = Guid.NewGuid().ToString();

            TestDoc replacedDoc = await AapEncryptionTests.ReplaceItemAsync(
                AapEncryptionTests.encryptionContainer,
                updatedDoc,
                AapEncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                upsertResponse.ETag);

            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, replacedDoc);

            await AapEncryptionTests.DeleteItemAsync(AapEncryptionTests.encryptionContainer, replacedDoc);
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuthRestricted()
        {
            TestDoc testDoc = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            User restrictedUser = AapEncryptionTests.database.GetUser(Guid.NewGuid().ToString());
            await AapEncryptionTests.database.CreateUserAsync(restrictedUser.Id);

            PermissionProperties restrictedUserPermission = await restrictedUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, AapEncryptionTests.itemContainer));
            
            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestAapEncryptionKeyStoreProvider());
            AapCosmosEncryptor encryptor = new AapCosmosEncryptor(dekProvider);

            CosmosClient clientForRestrictedUser = TestCommon.CreateCosmosClient(
                restrictedUserPermission.Token);

            int failureCount = 0;
            Database databaseForRestrictedUser = clientForRestrictedUser.GetDatabase(AapEncryptionTests.database.Id);
            Container containerForRestrictedUser = databaseForRestrictedUser.GetContainer(AapEncryptionTests.itemContainer.Id);
            Action<DecryptionResult> errorHandler = (decryptionErrorDetails) =>
            {
                Assert.AreEqual(decryptionErrorDetails.Exception.Message, $"The CosmosDataEncryptionKeyProvider was not initialized.");
                failureCount++;
            };


            Container encryptionContainerForRestrictedUser = containerForRestrictedUser.WithAapEncryptor(encryptor);

            await AapEncryptionTests.PerformForbiddenOperationAsync(() =>
              dekProvider.InitializeAsync(databaseForRestrictedUser, AapEncryptionTests.keyContainer.Id), "CosmosDekProvider.InitializeAsync");

            await AapEncryptionTests.PerformOperationOnUninitializedDekProviderAsync(() =>
                dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(AapEncryptionTests.dekId), "DEK.ReadAsync");

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
            User keyManagerUser = AapEncryptionTests.database.GetUser(Guid.NewGuid().ToString());
            await AapEncryptionTests.database.CreateUserAsync(keyManagerUser.Id);

            PermissionProperties keyManagerUserPermission = await keyManagerUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, AapEncryptionTests.keyContainer));

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestAapEncryptionKeyStoreProvider());

            AapCosmosEncryptor encryptor = new AapCosmosEncryptor(dekProvider);

            CosmosClient clientForKeyManagerUser = TestCommon.CreateCosmosClient(keyManagerUserPermission.Token);

            Database databaseForKeyManagerUser = clientForKeyManagerUser.GetDatabase(AapEncryptionTests.database.Id);

            await dekProvider.InitializeAsync(databaseForKeyManagerUser, AapEncryptionTests.keyContainer.Id);

            DataEncryptionKeyProperties readDekProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(AapEncryptionTests.dekId);
            Assert.AreEqual(AapEncryptionTests.dekProperties, readDekProperties);
        }

        [TestMethod]
        public async Task EncryptionRestrictedProperties()
        {
            TestDoc testDoc = TestDoc.Create();

            try
            {
                await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, new List<string>() { "/id" });
                Assert.Fail("Expected item creation with id specified to be encrypted to fail.");
            }
            catch (Exception ex)
            {
                _ = ex;                  
            }

            try
            {
                await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, new List<string>() { "/PK" });
                Assert.Fail("Expected item creation with PK specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }
        }

        [TestMethod]
        public async Task EncryptionBulkCrud()
        {
            TestDoc docToReplace = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            docToReplace.NonSensitive = Guid.NewGuid().ToString();
            docToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToUpsert = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            docToUpsert.NonSensitive = Guid.NewGuid().ToString();
            docToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            CosmosClient clientWithBulk = TestCommon.CreateCosmosClient(builder => builder
                .WithBulkExecution(true)
                .Build());

            Database databaseWithBulk = clientWithBulk.GetDatabase(AapEncryptionTests.database.Id);
            Container containerWithBulk = databaseWithBulk.GetContainer(AapEncryptionTests.itemContainer.Id);
            Container encryptionContainerWithBulk = containerWithBulk.WithAapEncryptor(AapEncryptionTests.encryptor);

            List<Task> tasks = new List<Task>()
            {
                AapEncryptionTests.CreateItemAsync(encryptionContainerWithBulk, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt),
                AapEncryptionTests.UpsertItemAsync(encryptionContainerWithBulk, TestDoc.Create(), AapEncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.Created),
                AapEncryptionTests.ReplaceItemAsync(encryptionContainerWithBulk, docToReplace, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt),
                AapEncryptionTests.UpsertItemAsync(encryptionContainerWithBulk, docToUpsert, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.OK),
                AapEncryptionTests.DeleteItemAsync(encryptionContainerWithBulk, docToDelete)
            };

            await Task.WhenAll(tasks);
        }

        [TestMethod]
        public async Task EncryptionTransactionBatchCrud()
        {
            string partitionKey = "thePK";
            string dek1 = AapEncryptionTests.dekId;
            string dek2 = "dek2Forbatch";
            await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dek2);

            TestDoc doc1ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc2ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc3ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc4ToCreate = TestDoc.Create(partitionKey);

            ItemResponse<TestDoc> doc1ToReplaceCreateResponse = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            TestDoc doc1ToReplace = doc1ToReplaceCreateResponse.Resource;
            doc1ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc1ToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc2ToReplace = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt, partitionKey);
            doc2ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc2ToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc1ToUpsert = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt, partitionKey);
            doc1ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc1ToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc2ToUpsert = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            doc2ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc2ToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);

            TransactionalBatchResponse batchResponse = await AapEncryptionTests.encryptionContainer.CreateTransactionalBatch(new Cosmos.PartitionKey(partitionKey))
                .CreateItem(doc1ToCreate, AapEncryptionTests.GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt))
                .CreateItemStream(doc2ToCreate.ToStream(), AapEncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
                .ReplaceItem(doc1ToReplace.Id, doc1ToReplace, AapEncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt, doc1ToReplaceCreateResponse.ETag))
                .CreateItem(doc3ToCreate)
                .CreateItem(doc4ToCreate, AapEncryptionTests.GetBatchItemRequestOptions(dek1, new List<string>())) // empty PathsToEncrypt list
                .ReplaceItemStream(doc2ToReplace.Id, doc2ToReplace.ToStream(), AapEncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
                .UpsertItem(doc1ToUpsert, AapEncryptionTests.GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt))
                .DeleteItem(docToDelete.Id)
                .UpsertItemStream(doc2ToUpsert.ToStream(), AapEncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
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


            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc1ToCreate);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc2ToCreate);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc3ToCreate);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc4ToCreate);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc1ToReplace);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc2ToReplace);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc1ToUpsert);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc2ToUpsert);

            ResponseMessage readResponseMessage = await AapEncryptionTests.encryptionContainer.ReadItemStreamAsync(docToDelete.Id, new PartitionKey(docToDelete.PK));
            Assert.AreEqual(HttpStatusCode.NotFound, readResponseMessage.StatusCode);

            // Validate that the documents are encrypted as expected by trying to retrieve through regular (non-encryption) container
            doc1ToCreate.Sensitive = null;
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc1ToCreate);

            doc2ToCreate.Sensitive = null;
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc2ToCreate);

            // doc3ToCreate, doc4ToCreate wasn't encrypted
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc3ToCreate);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc4ToCreate);

            doc1ToReplace.Sensitive = null;
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc1ToReplace);

            doc2ToReplace.Sensitive = null;
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc2ToReplace);

            doc1ToUpsert.Sensitive = null;
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc1ToUpsert);

            doc2ToUpsert.Sensitive = null;
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc2ToUpsert);
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

            if (expectedDoc.Sensitive == null)
            {
                expectedDoc.Sensitive = sprocResponse.Resource.Sensitive;                
            }

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

                if (expectedDoc.Sensitive == null)
                {
                    expectedDoc.Sensitive = readDoc.Sensitive;                    
                }

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
                    if (testDoc1.Sensitive == null)
                    {
                        testDoc1.Sensitive = readDocs.ElementAt(index).Sensitive;                        
                    }

                    Assert.AreEqual(readDocs.ElementAt(index), testDoc1);
                }
                else if (readDocs.ElementAt(index).Id.Equals(testDoc2.Id))
                {
                    if (testDoc2.Sensitive == null)
                    {
                        testDoc2.Sensitive = readDocs.ElementAt(index).Sensitive;
                        
                    }
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

            if (testDoc1.Sensitive == null)
            {
                testDoc1.Sensitive = changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 2].Sensitive;
                
            }

            Assert.AreEqual(testDoc1, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 2]);

            if (testDoc2.Sensitive == null)
            {
                testDoc2.Sensitive = changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 1].Sensitive;
                
            }
            Assert.AreEqual(testDoc2, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 1]);
        }       

        private static void ErrorHandler(DecryptionResult decryptionErrorDetails)
        {
            Assert.AreEqual(decryptionErrorDetails.Exception.Message, "Null DataEncryptionKey returned.");

            using (MemoryStream memoryStream = new MemoryStream(decryptionErrorDetails.EncryptedStream.ToArray()))
            {
                JObject itemJObj = TestCommon.FromStream<JObject>(memoryStream);
                JProperty encryptionPropertiesJProp = itemJObj.Property("_ei");
                Assert.IsNotNull(encryptionPropertiesJProp);
                Assert.AreEqual(itemJObj.Property("id").Value.ToString(), AapEncryptionTests.decryptionFailedDocId);
            }                
        }

        private static ItemRequestOptions GetItemRequestOptionsWithDecryptionResultHandler()
        {
            return new EncryptionItemRequestOptions
            {
                DecryptionResultHandler = AapEncryptionTests.ErrorHandler
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
                AapEncryptionTests.GetRequestOptions(dekId, pathsToEncrypt));
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
                AapEncryptionTests.GetRequestOptions(dekId, pathsToEncrypt));
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
                AapEncryptionTests.GetRequestOptions(dekId, pathsToEncrypt, etag));

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
                EncryptionOptions = AapEncryptionTests.GetEncryptionOptions(dekId, pathsToEncrypt),
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
                EncryptionOptions = AapEncryptionTests.GetEncryptionOptions(dekId, pathsToEncrypt),
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
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = pathsToEncrypt
            };
        }

        private static async Task VerifyItemByReadStreamAsync(Container container, TestDoc testDoc, ItemRequestOptions requestOptions = null)
        {
            ResponseMessage readResponseMessage = await container.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK), requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, readResponseMessage.StatusCode);
            Assert.IsNotNull(readResponseMessage.Content);
            TestDoc readDoc = TestCommon.FromStream<TestDoc>(readResponseMessage.Content);
            if (testDoc.Sensitive == null)
            {
                testDoc.Sensitive = readDoc.Sensitive;
            }

            Assert.AreEqual(testDoc, readDoc);      
            
        }

        private static async Task VerifyItemByReadAsync(Container container, TestDoc testDoc, ItemRequestOptions requestOptions = null)
        {
            ItemResponse<TestDoc> readResponse = await container.ReadItemAsync<TestDoc>(testDoc.Id, new PartitionKey(testDoc.PK), requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            if (testDoc.Sensitive == null && readResponse.Resource != null && readResponse.Resource.Sensitive != null)
            {
                testDoc.Sensitive = readResponse.Resource.Sensitive;                
            }

            Assert.AreEqual(testDoc, readResponse.Resource);
        }

        private static async Task<DataEncryptionKeyProperties> CreateDekAsync(CosmosDataEncryptionKeyProvider dekProvider, string dekId)
        {
            ItemResponse<DataEncryptionKeyProperties> dekResponse = await dekProvider.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                dekId,
                CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized,
                AapEncryptionTests.metadata1);

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

        private static X509Certificate2 GetCertificate(string clientCertThumbprint)
        {
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindByThumbprint, clientCertThumbprint, false);
            store.Close();

            if (certs.Count == 0)
            {
                throw new ArgumentException("Certificate with thumbprint not found in LocalMachine certificate store");
            }

            return certs[0];
        }

        private static TokenCredential GetTokenCredential(string tenantId, string clientId, string clientCertThumbprint)
        {
            ClientCertificateCredential clientCertificateCredential;
            clientCertificateCredential = new ClientCertificateCredential(tenantId, clientId, GetCertificate(clientCertThumbprint));
            return clientCertificateCredential;
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

        private class TestAapEncryptionKeyStoreProvider : EncryptionKeyStoreProvider
        {
            Dictionary<string, int> keyinfo = new Dictionary<string, int>
                {
                {masterKeyUri1.ToString(),1},
                {masterKeyUri2.ToString(),2},
                {masterKeyUri3.ToString(),3},
                };

            public Dictionary<string, int> WrapKeyCallsCount { get; set; }                      

            public TestAapEncryptionKeyStoreProvider()
            {
                this.WrapKeyCallsCount = new Dictionary<string, int>();                
            }

            public override string ProviderName => "TESTKEYSTORE_VAULT";

            public override byte[] DecryptEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
            {
                this.keyinfo.TryGetValue(masterKeyPath, out int moveBy);
                byte[] plainkey = encryptedColumnEncryptionKey.Select(b => (byte)(b - moveBy)).ToArray();
                return plainkey;
            }

            public override byte[] EncryptEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
            {
                this.keyinfo.TryGetValue(masterKeyPath, out int moveBy);
                byte[] encryptedkey =  columnEncryptionKey.Select(b => (byte)(b + moveBy)).ToArray();
                return encryptedkey;
            }

            public override byte[] SignMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations)
            {
                return null;
            }

            public override bool VerifyMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
            {
                return true;
            }
        }

        // This class is same as CosmosEncryptor but copied so as to induce decryption failure easily for testing.
        private class AapTestEncryptor : Encryptor
        {
            public DataEncryptionKeyProvider DataEncryptionKeyProvider { get; }
            public bool FailDecryption { get; set; }

            public AapTestEncryptor(DataEncryptionKeyProvider dataEncryptionKeyProvider)
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
        }        
    }
}
