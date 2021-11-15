//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption;
    using Microsoft.Data.Encryption.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using EncryptionKeyWrapMetadata = Cosmos.EncryptionKeyWrapMetadata;

    [TestClass]
    public class MdeEncryptionTests
    {
        private static EncryptionKeyWrapMetadata metadata1;
        private static EncryptionKeyWrapMetadata metadata2;

        private static CosmosClient client;
        private static CosmosClient encryptionCosmosClient;
        private static Database database;
        private static Container encryptionContainer;
        private static Container encryptionContainerForChangeFeed;
        private static TestEncryptionKeyStoreProvider testEncryptionKeyStoreProvider;

        [ClassInitialize]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The ClassInitialize method takes a single parameter of type TestContext.")]
        public static async Task ClassInitialize(TestContext context)
        {
            MdeEncryptionTests.client = TestCommon.CreateCosmosClient();
            testEncryptionKeyStoreProvider = new TestEncryptionKeyStoreProvider
            {
                DataEncryptionKeyCacheTimeToLive = null
            };

            metadata1 = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, "key1", "tempmetadata1");
            metadata2 = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, "key2", "tempmetadata2");

            MdeEncryptionTests.encryptionCosmosClient = MdeEncryptionTests.client.WithEncryption(testEncryptionKeyStoreProvider);
            MdeEncryptionTests.database = await MdeEncryptionTests.encryptionCosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());

            await MdeEncryptionTests.CreateClientEncryptionKeyAsync(
               "key1",
               metadata1);

            await MdeEncryptionTests.CreateClientEncryptionKeyAsync(
                "key2",
                metadata2);


            EncryptionKeyWrapMetadata revokedKekmetadata = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, "revokedKek", "revokedKek-metadata");
            await database.CreateClientEncryptionKeyAsync(
                "keywithRevokedKek",
                DataEncryptionKeyAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256,
                revokedKekmetadata);

            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>()
            {
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_StringFormat",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_ArrayFormat",
                    ClientEncryptionKeyId = "keywithRevokedKek",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_NestedObjectFormatL1",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_IntArray",
                    ClientEncryptionKeyId = "key2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_IntFormat",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_DecimalFormat",
                    ClientEncryptionKeyId = "key2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_DateFormat",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_BoolFormat",
                    ClientEncryptionKeyId = "key2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_FloatFormat",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
                
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_ArrayMultiTypes",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
               
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_IntMultiDimArray",
                    ClientEncryptionKeyId = "key2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_ObjectArrayType",
                    ClientEncryptionKeyId = "key2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
            };


            ClientEncryptionPolicy clientEncryptionPolicy = new ClientEncryptionPolicy(paths);
           

            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK") { ClientEncryptionPolicy = clientEncryptionPolicy };
            ContainerProperties containerPropertiesForChangeFeed = new ContainerProperties(Guid.NewGuid().ToString(), "/PK") { ClientEncryptionPolicy = clientEncryptionPolicy };

            encryptionContainer = await database.CreateContainerAsync(containerProperties, 400);
            await encryptionContainer.InitializeEncryptionAsync();

            encryptionContainerForChangeFeed = await database.CreateContainerAsync(containerPropertiesForChangeFeed, 400);
            await encryptionContainerForChangeFeed.InitializeEncryptionAsync();
        }

        private static async Task<ClientEncryptionKeyResponse> CreateClientEncryptionKeyAsync(string cekId, Cosmos.EncryptionKeyWrapMetadata encryptionKeyWrapMetadata)
        {
            ClientEncryptionKeyResponse clientEncrytionKeyResponse = await database.CreateClientEncryptionKeyAsync(
                   cekId,
                   DataEncryptionKeyAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256,
                   encryptionKeyWrapMetadata);

            Assert.AreEqual(HttpStatusCode.Created, clientEncrytionKeyResponse.StatusCode);
            Assert.IsTrue(clientEncrytionKeyResponse.RequestCharge > 0);
            Assert.IsNotNull(clientEncrytionKeyResponse.ETag);
            return clientEncrytionKeyResponse;
        }

        private static async Task<ClientEncryptionKeyResponse> RewarpClientEncryptionKeyAsync(string cekId, Cosmos.EncryptionKeyWrapMetadata encryptionKeyWrapMetadata)
        {
            ClientEncryptionKeyResponse clientEncrytionKeyResponse = await database.RewrapClientEncryptionKeyAsync(
                   cekId,
                   encryptionKeyWrapMetadata);

            Assert.AreEqual(HttpStatusCode.OK, clientEncrytionKeyResponse.StatusCode);
            Assert.IsTrue(clientEncrytionKeyResponse.RequestCharge > 0);
            Assert.IsNotNull(clientEncrytionKeyResponse.ETag);
            return clientEncrytionKeyResponse;
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            if (MdeEncryptionTests.database != null)
            {
                using (await MdeEncryptionTests.database.DeleteStreamAsync()) { }
            }

            if (MdeEncryptionTests.client != null)
            {
                MdeEncryptionTests.client.Dispose();
            }
        }

        [TestMethod]
        public async Task EncryptionBulkCrud()
        {
            TestDoc docToReplace = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            docToReplace.NonSensitive = Guid.NewGuid().ToString();
            docToReplace.Sensitive_StringFormat = Guid.NewGuid().ToString();

            TestDoc docToUpsert = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            docToUpsert.NonSensitive = Guid.NewGuid().ToString();
            docToUpsert.Sensitive_StringFormat = Guid.NewGuid().ToString();

            TestDoc docToDelete = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            CosmosClient clientWithBulk = TestCommon.CreateCosmosClient(builder => builder
                .WithBulkExecution(true)
                .Build());

            CosmosClient encryptionCosmosClientWithBulk = clientWithBulk.WithEncryption(new TestEncryptionKeyStoreProvider());
            Database databaseWithBulk = encryptionCosmosClientWithBulk.GetDatabase(MdeEncryptionTests.database.Id);

            Container encryptionContainerWithBulk = databaseWithBulk.GetContainer(MdeEncryptionTests.encryptionContainer.Id);

            List<Task> tasks = new List<Task>()
            {
                MdeEncryptionTests.MdeCreateItemAsync(encryptionContainerWithBulk),
                MdeEncryptionTests.MdeUpsertItemAsync(encryptionContainerWithBulk, TestDoc.Create(), HttpStatusCode.Created),
                MdeEncryptionTests.MdeReplaceItemAsync(encryptionContainerWithBulk, docToReplace),
                MdeEncryptionTests.MdeUpsertItemAsync(encryptionContainerWithBulk, docToUpsert, HttpStatusCode.OK),
                MdeEncryptionTests.MdeDeleteItemAsync(encryptionContainerWithBulk, docToDelete)
            };

            await Task.WhenAll(tasks);
        }

        [TestMethod]
        public async Task EncryptionCreateClientEncryptionKey()
        {
            string cekId = "anotherCek";
			
            EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, cekId, "testmetadata1");
			
            ClientEncryptionKeyProperties clientEncryptionKeyProperties = await MdeEncryptionTests.CreateClientEncryptionKeyAsync(
                cekId,
                metadata1);

            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, name: cekId, value: metadata1.Value),
                clientEncryptionKeyProperties.EncryptionKeyWrapMetadata);

            // creating another key with same id should fail
            metadata1 = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, cekId, "testmetadata2");

            try
            {
                await MdeEncryptionTests.CreateClientEncryptionKeyAsync(
                cekId,
                metadata1);
                Assert.Fail("Creating two keys with same client encryption key id should have failed.");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is CosmosException);
                if (ex is CosmosException cosmosException)
                    Assert.AreEqual(HttpStatusCode.Conflict, cosmosException.StatusCode);
            }
        }

        [TestMethod]
        public async Task EncryptionRewrapClientEncryptionKey()
        {
            string cekId = "rewrapkeytest";
            EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, cekId, "testmetadata1");
            ClientEncryptionKeyProperties clientEncryptionKeyProperties = await MdeEncryptionTests.CreateClientEncryptionKeyAsync(
                cekId,
                metadata1);

            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, name: cekId, value: metadata1.Value),
                clientEncryptionKeyProperties.EncryptionKeyWrapMetadata);

            EncryptionKeyWrapMetadata updatedMetaData = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, cekId, metadata1 + "updatedmetadata");
            clientEncryptionKeyProperties = await MdeEncryptionTests.RewarpClientEncryptionKeyAsync(
                cekId,
                updatedMetaData);

            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, name: cekId, value: updatedMetaData.Value),
                clientEncryptionKeyProperties.EncryptionKeyWrapMetadata);

        }

        [TestMethod]
        public async Task EncryptionCreateItemWithoutPartitionKey()
        {
            TestDoc testDoc = TestDoc.Create();
            try
            {
                await MdeEncryptionTests.encryptionContainer.CreateItemAsync(
                    testDoc,
                    null);
                Assert.Fail("CreateItem should've failed because PartitionKey was not provided.");
            }
            catch (NotSupportedException ex)
            {
                Assert.AreEqual("partitionKey cannot be null for operations using EncryptionContainer.", ex.Message);
            }
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithNullProperty()
        {
            CosmosClient clientWithNoCaching = TestCommon.CreateCosmosClient(builder => builder
                .Build());

            TestEncryptionKeyStoreProvider testEncryptionKeyStoreProvider = new TestEncryptionKeyStoreProvider
            {
                DataEncryptionKeyCacheTimeToLive = TimeSpan.Zero
            };

            CosmosClient encryptionCosmosClient = clientWithNoCaching.WithEncryption(testEncryptionKeyStoreProvider);
            Database database = encryptionCosmosClient.GetDatabase(MdeEncryptionTests.database.Id);

            Container encryptionContainer = database.GetContainer(MdeEncryptionTests.encryptionContainer.Id);

            TestDoc testDoc = TestDoc.Create();

            testDoc.Sensitive_ArrayFormat = null;
            testDoc.Sensitive_StringFormat = null;
            testDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_StringFormatL2 = null;

            ItemResponse<TestDoc> createResponse = await encryptionContainer.CreateItemAsync(
                   testDoc,
                   new PartitionKey(testDoc.PK));

            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            VerifyExpectedDocResponse(testDoc, createResponse.Resource);

            // run query on document with null property value.
            QueryDefinition withEncryptedParameter = encryptionContainer.CreateQueryDefinition(
                    "SELECT * FROM c where c.Sensitive_StringFormat = @Sensitive_StringFormat AND c.Sensitive_ArrayFormat = @Sensitive_ArrayFormat" +
                    " AND c.Sensitive_IntFormat = @Sensitive_IntFormat" +
                    " AND c.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_StringFormatL2 = @Sensitive_StringFormatL2" +
                    " AND c.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_DecimalFormatL2 = @Sensitive_DecimalFormatL2");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_StringFormat",
                    testDoc.Sensitive_StringFormat,
                    "/Sensitive_StringFormat");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_ArrayFormat",
                    testDoc.Sensitive_ArrayFormat,
                    "/Sensitive_ArrayFormat");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_IntFormat",
                    testDoc.Sensitive_IntFormat,
                    "/Sensitive_IntFormat");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_StringFormatL2",
                    testDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_StringFormatL2,
                    "/Sensitive_NestedObjectFormatL1");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_DecimalFormatL2",
                    testDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_DecimalFormatL2,
                    "/Sensitive_NestedObjectFormatL1");

            TestDoc expectedDoc = new TestDoc(testDoc);
            await MdeEncryptionTests.ValidateQueryResultsAsync(
                encryptionContainer,
                queryDefinition: withEncryptedParameter,
                expectedDoc: expectedDoc);

            // no access to key.
            testEncryptionKeyStoreProvider.RevokeAccessSet = true;

            testDoc = TestDoc.Create();

            testDoc.Sensitive_ArrayFormat = null;
            testDoc.Sensitive_StringFormat = null;

            createResponse = await encryptionContainer.CreateItemAsync(
                    testDoc,
                    new PartitionKey(testDoc.PK));

            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            VerifyExpectedDocResponse(testDoc, createResponse.Resource);

            testEncryptionKeyStoreProvider.RevokeAccessSet = false;
        }


        [TestMethod]
        public async Task EncryptionResourceTokenAuthRestricted()
        {
            TestDoc testDoc = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            User restrictedUser = MdeEncryptionTests.database.GetUser(Guid.NewGuid().ToString());
            await MdeEncryptionTests.database.CreateUserAsync(restrictedUser.Id);

            PermissionProperties restrictedUserPermission = await restrictedUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, MdeEncryptionTests.encryptionContainer));

            CosmosClient clientForRestrictedUser = TestCommon.CreateCosmosClient(
                restrictedUserPermission.Token);


            CosmosClient encryptedclientForRestrictedUser = clientForRestrictedUser.WithEncryption(new TestEncryptionKeyStoreProvider());

            Database databaseForRestrictedUser = encryptedclientForRestrictedUser.GetDatabase(MdeEncryptionTests.database.Id);                  

            try
            {
                string cekId = "testingcekID";
                EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, cekId, "testmetadata1");

                ClientEncryptionKeyResponse clientEncrytionKeyResponse = await databaseForRestrictedUser.CreateClientEncryptionKeyAsync(
                       cekId,
                       DataEncryptionKeyAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256,
                       metadata1);
                Assert.Fail("CreateClientEncryptionKeyAsync should have failed due to restrictions");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
            }

            try
            {
                string cekId = "testingcekID";
                EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, cekId, "testmetadata1" + "updated");

                ClientEncryptionKeyResponse clientEncrytionKeyResponse = await databaseForRestrictedUser.RewrapClientEncryptionKeyAsync(
                       cekId,
                       metadata1);
                Assert.Fail("RewrapClientEncryptionKeyAsync should have failed due to restrictions");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
            }
        }

        [TestMethod]
        public async Task ClientEncryptionPolicyTests()
        {
            string containerId = "containerWithUnsuportedPolicy1";
            ClientEncryptionIncludedPath restrictedPathId = new ClientEncryptionIncludedPath()
            {
                Path = "/id",
                ClientEncryptionKeyId = "key1",
                EncryptionType = "unsupported",
                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
            };

            try
            {
                await database.DefineContainer(containerId, "/PK")
                    .WithClientEncryptionPolicy()
                    .WithIncludedPath(restrictedPathId)
                    .Attach()
                    .CreateAsync(throughput: 1000);

                Assert.Fail("Container Creation should have failed with incorrect Policy cofigured");
            }
            catch(Exception)
            {
            }            
        }

        [TestMethod]
        public async Task EncryptionCreateItemAndQuery()
        {
            TestDoc testDoc = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            TestDoc testDoc2 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, testDoc);

            await MdeEncryptionTests.VerifyItemByReadStreamAsync(MdeEncryptionTests.encryptionContainer, testDoc2);

            TestDoc expectedDoc = new TestDoc(testDoc);

            await MdeEncryptionTests.ValidateQueryResultsAsync(
                MdeEncryptionTests.encryptionContainer,
                query: null,
                expectedDoc);

            await MdeEncryptionTests.ValidateQueryResultsAsync(
                MdeEncryptionTests.encryptionContainer,
                "SELECT * FROM c",
                expectedDoc);

            await MdeEncryptionTests.ValidateQueryResultsAsync(
                MdeEncryptionTests.encryptionContainer,
                string.Format(
                    "SELECT * FROM c where c.PK = '{0}' and c.id = '{1}' and c.NonSensitive = '{2}'",
                    expectedDoc.PK,
                    expectedDoc.Id,
                    expectedDoc.NonSensitive),
                expectedDoc);

            await MdeEncryptionTests.ValidateQueryResultsAsync(
                MdeEncryptionTests.encryptionContainer,
                string.Format("SELECT * FROM c where c.Sensitive_IntFormat = '{0}'", testDoc.Sensitive_IntFormat),
                expectedDoc: null);

            await MdeEncryptionTests.ValidateQueryResultsAsync(
                MdeEncryptionTests.encryptionContainer,
                queryDefinition: new QueryDefinition(
                    "select * from c where c.id = @theId and c.PK = @thePK")
                         .WithParameter("@theId", expectedDoc.Id)
                         .WithParameter("@thePK", expectedDoc.PK),
                expectedDoc: expectedDoc);

            expectedDoc.Sensitive_IntMultiDimArray = null;
            expectedDoc.Sensitive_ArrayMultiTypes = null;
            expectedDoc.Sensitive_NestedObjectFormatL1 = null;
            expectedDoc.Sensitive_ArrayFormat = null;
            expectedDoc.Sensitive_DecimalFormat = 0;
            expectedDoc.Sensitive_IntFormat = 0;
            expectedDoc.Sensitive_FloatFormat = 0;
            expectedDoc.Sensitive_BoolFormat = false;
            expectedDoc.Sensitive_DateFormat = new DateTime();
            expectedDoc.Sensitive_StringFormat = null;
            expectedDoc.Sensitive_IntArray = null;
            expectedDoc.Sensitive_ObjectArrayType = null;

            await MdeEncryptionTests.ValidateQueryResultsAsync(
                MdeEncryptionTests.encryptionContainer,
                "SELECT c.id, c.PK, c.NonSensitive, c.NonSensitiveInt FROM c",
                expectedDoc,
                expectedPropertiesDecryptedCount: 0);
        }

        [TestMethod]
        public async Task QueryOnEncryptedProperties()
        {
            TestDoc testDoc1 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            // string/int
            string[] arrayofStringValues = new string[] { testDoc1.Sensitive_StringFormat, "randomValue", null };

            QueryDefinition withEncryptedParameter = MdeEncryptionTests.encryptionContainer.CreateQueryDefinition(
                    "SELECT * FROM c where array_contains(@Sensitive_StringFormat, c.Sensitive_StringFormat) " +
                    "AND c.Sensitive_IntArray = @Sensitive_IntArray " +
                    "AND c.Sensitive_NestedObjectFormatL1 = @Sensitive_NestedObjectFormatL1");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_StringFormat",
                    arrayofStringValues,
                    "/Sensitive_StringFormat");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_IntArray",
                    testDoc1.Sensitive_IntArray,
                    "/Sensitive_IntArray");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_NestedObjectFormatL1",
                    testDoc1.Sensitive_NestedObjectFormatL1,
                    "/Sensitive_NestedObjectFormatL1");

            TestDoc expectedDoc = new TestDoc(testDoc1);
            await MdeEncryptionTests.ValidateQueryResultsAsync(
                MdeEncryptionTests.encryptionContainer,
                queryDefinition:withEncryptedParameter,
                expectedDoc: expectedDoc);

            // bool and float type

            withEncryptedParameter = MdeEncryptionTests.encryptionContainer.CreateQueryDefinition(
                    "SELECT * FROM c where c.Sensitive_BoolFormat = @Sensitive_BoolFormat AND c.Sensitive_FloatFormat = @Sensitive_FloatFormat");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_BoolFormat",
                    testDoc1.Sensitive_BoolFormat,
                    "/Sensitive_BoolFormat");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_FloatFormat",
                    testDoc1.Sensitive_FloatFormat,
                    "/Sensitive_FloatFormat");

            expectedDoc = new TestDoc(testDoc1);
            await MdeEncryptionTests.ValidateQueryResultsAsync(
                MdeEncryptionTests.encryptionContainer,
                queryDefinition: withEncryptedParameter,
                expectedDoc: expectedDoc);

            // with encrypted and non encrypted properties
            TestDoc testDoc2 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            
            withEncryptedParameter =
                    MdeEncryptionTests.encryptionContainer.CreateQueryDefinition(
                    "SELECT * FROM c where c.NonSensitive = @NonSensitive AND c.Sensitive_IntFormat = @Sensitive_IntFormat");

            await withEncryptedParameter.AddParameterAsync(
                    "@NonSensitive",
                    testDoc2.NonSensitive,
                    "/NonSensitive");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_IntFormat",
                    testDoc2.Sensitive_IntFormat,
                    "/Sensitive_IntFormat");

            expectedDoc = new TestDoc(testDoc2);
            await MdeEncryptionTests.ValidateQueryResultsAsync(
                MdeEncryptionTests.encryptionContainer,
                queryDefinition: withEncryptedParameter,
                expectedDoc: expectedDoc);

            withEncryptedParameter = new QueryDefinition(
                    "SELECT c.Sensitive_DateFormat FROM c");

            FeedIterator<TestDoc> queryResponseIterator;
            queryResponseIterator = MdeEncryptionTests.encryptionContainer.GetItemQueryIterator<TestDoc>(withEncryptedParameter);
            FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();

            Assert.AreNotEqual(0, readDocs.Count);
            VerifyDiagnostics(readDocs.Diagnostics, encryptOperation: false, expectedPropertiesDecryptedCount: 2);
        }

        [TestMethod]
        public async Task EncryptionTransactionBatchCrud()
        {
            string partitionKey = "thePK";

            TestDoc doc1ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc2ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc3ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc4ToCreate = TestDoc.Create(partitionKey);

            ItemResponse<TestDoc> doc1ToReplaceCreateResponse = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer, partitionKey);
            TestDoc doc1ToReplace = doc1ToReplaceCreateResponse.Resource;
            doc1ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc1ToReplace.Sensitive_StringFormat = Guid.NewGuid().ToString();

            TestDoc doc2ToReplace = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer, partitionKey);
            doc2ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc2ToReplace.Sensitive_StringFormat = Guid.NewGuid().ToString();

            TestDoc doc1ToUpsert = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer, partitionKey);
            doc1ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc1ToUpsert.Sensitive_StringFormat = Guid.NewGuid().ToString();

            TestDoc doc2ToUpsert = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer, partitionKey);
            doc2ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc2ToUpsert.Sensitive_StringFormat = Guid.NewGuid().ToString();

            TestDoc docToDelete = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer, partitionKey);

            TestDoc docToPatch = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer, partitionKey);
            docToPatch.Sensitive_StringFormat = Guid.NewGuid().ToString();
            docToPatch.Sensitive_DecimalFormat = 11.11m;

            List<PatchOperation> patchOperations = new List<PatchOperation>
            {
                PatchOperation.Replace("/Sensitive_StringFormat", docToPatch.Sensitive_StringFormat),
                PatchOperation.Replace("/Sensitive_DecimalFormat", docToPatch.Sensitive_DecimalFormat),
            };

            TransactionalBatchResponse batchResponse = await MdeEncryptionTests.encryptionContainer.CreateTransactionalBatch(new Cosmos.PartitionKey(partitionKey))
                .CreateItem(doc1ToCreate)
                .CreateItemStream(doc2ToCreate.ToStream())
                .ReplaceItem(doc1ToReplace.Id, doc1ToReplace, new TransactionalBatchItemRequestOptions { IfMatchEtag = doc1ToReplaceCreateResponse.ETag })
                .CreateItem(doc3ToCreate)
                .CreateItem(doc4ToCreate) // empty PathsToEncrypt list
                .ReplaceItemStream(doc2ToReplace.Id, doc2ToReplace.ToStream())
                .UpsertItem(doc1ToUpsert)
                .DeleteItem(docToDelete.Id)
                .UpsertItemStream(doc2ToUpsert.ToStream())
                .PatchItem(docToPatch.Id, patchOperations)
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);
            VerifyDiagnostics(batchResponse.Diagnostics, encryptOperation: false, expectedPropertiesDecryptedCount: 0);

            TransactionalBatchOperationResult<TestDoc> doc1 = batchResponse.GetOperationResultAtIndex<TestDoc>(0);
            VerifyExpectedDocResponse(doc1ToCreate, doc1.Resource);

            TransactionalBatchOperationResult<TestDoc> doc2 = batchResponse.GetOperationResultAtIndex<TestDoc>(1);
            VerifyExpectedDocResponse(doc2ToCreate, doc2.Resource);

            TransactionalBatchOperationResult<TestDoc> doc3 = batchResponse.GetOperationResultAtIndex<TestDoc>(2);
            VerifyExpectedDocResponse(doc1ToReplace, doc3.Resource);

            TransactionalBatchOperationResult<TestDoc> doc4 = batchResponse.GetOperationResultAtIndex<TestDoc>(3);
            VerifyExpectedDocResponse(doc3ToCreate, doc4.Resource);

            TransactionalBatchOperationResult<TestDoc> doc5 = batchResponse.GetOperationResultAtIndex<TestDoc>(4);
            VerifyExpectedDocResponse(doc4ToCreate, doc5.Resource);

            TransactionalBatchOperationResult<TestDoc> doc6 = batchResponse.GetOperationResultAtIndex<TestDoc>(5);
            VerifyExpectedDocResponse(doc2ToReplace, doc6.Resource);

            TransactionalBatchOperationResult<TestDoc> doc7 = batchResponse.GetOperationResultAtIndex<TestDoc>(6);
            VerifyExpectedDocResponse(doc1ToUpsert, doc7.Resource);

            TransactionalBatchOperationResult<TestDoc> doc8 = batchResponse.GetOperationResultAtIndex<TestDoc>(8);
            VerifyExpectedDocResponse(doc2ToUpsert, doc8.Resource);

            TransactionalBatchOperationResult<TestDoc> doc9 = batchResponse.GetOperationResultAtIndex<TestDoc>(9);
            VerifyExpectedDocResponse(docToPatch, doc9.Resource);
            
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc1ToCreate);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc2ToCreate);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc3ToCreate);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc4ToCreate);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc1ToReplace);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc2ToReplace);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc1ToUpsert);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc2ToUpsert);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, docToPatch);

            ResponseMessage readResponseMessage = await MdeEncryptionTests.encryptionContainer.ReadItemStreamAsync(docToDelete.Id, new PartitionKey(docToDelete.PK));
            Assert.AreEqual(HttpStatusCode.NotFound, readResponseMessage.StatusCode);
        }

        [TestMethod]
        public async Task EncryptionTransactionalBatchConflictResponse()
        {
            string partitionKey = "thePK";

            ItemResponse<TestDoc> doc1CreatedResponse = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer, partitionKey);
            TestDoc doc1ToCreateAgain = doc1CreatedResponse.Resource;
            doc1ToCreateAgain.NonSensitive = Guid.NewGuid().ToString();
            doc1ToCreateAgain.Sensitive_StringFormat = Guid.NewGuid().ToString();

            TransactionalBatchResponse batchResponse = await MdeEncryptionTests.encryptionContainer.CreateTransactionalBatch(new Cosmos.PartitionKey(partitionKey))
                .CreateItem(doc1ToCreateAgain)
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.Conflict, batchResponse.StatusCode);
            Assert.AreEqual(1, batchResponse.Count);
        }

        [TestMethod]
        public async Task EncryptionReadManyItemAsync()
        {
            TestDoc testDoc = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            TestDoc testDoc2 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>
            {
                (testDoc.Id, new PartitionKey(testDoc.PK)),
                (testDoc2.Id, new PartitionKey(testDoc2.PK))
            };

            FeedResponse<TestDoc> response = await encryptionContainer.ReadManyItemsAsync<TestDoc>(itemList);
            VerifyDiagnostics(response.Diagnostics, encryptOperation: false, expectedPropertiesDecryptedCount: 24);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(2, response.Count);
            VerifyExpectedDocResponse(testDoc, response.Resource.ElementAt(0));
            VerifyExpectedDocResponse(testDoc2, response.Resource.ElementAt(1));

            // stream test.
            TestDoc testDoc3 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            itemList.Add((testDoc3.Id, new PartitionKey(testDoc3.PK)));

            ResponseMessage responseStream = await encryptionContainer.ReadManyItemsStreamAsync(itemList);
            VerifyDiagnostics(responseStream.Diagnostics, encryptOperation: false, expectedPropertiesDecryptedCount: 36);

            Assert.IsTrue(responseStream.IsSuccessStatusCode);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            JObject contentJObjects = TestCommon.FromStream<JObject>(responseStream.Content);

            if(contentJObjects.SelectToken(Constants.DocumentsResourcePropertyName) is JArray documents)
            {
                VerifyExpectedDocResponse(testDoc, documents.ElementAt(0).ToObject<TestDoc>());
                VerifyExpectedDocResponse(testDoc2, documents.ElementAt(1).ToObject<TestDoc>());
                VerifyExpectedDocResponse(testDoc3, documents.ElementAt(2).ToObject<TestDoc>());
            }
            else
            {
                Assert.Fail("ResponseMessage from ReadManyItemsStreamAsync did not have a valid response. ");
            }
        }

        [TestMethod]
        public async Task EncryptionChangeFeedDecryptionSuccessful()
        {
            TestDoc testDoc1 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainerForChangeFeed);
            TestDoc testDoc2 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainerForChangeFeed);

            // change feed iterator
            await this.ValidateChangeFeedIteratorResponse(MdeEncryptionTests.encryptionContainerForChangeFeed, testDoc1, testDoc2);

            // change feed processor
            await this.ValidateChangeFeedProcessorResponse(MdeEncryptionTests.encryptionContainerForChangeFeed, testDoc1, testDoc2);

            // change feed processor with feed handler
            await this.ValidateChangeFeedProcessorWithFeedHandlerResponse(MdeEncryptionTests.encryptionContainerForChangeFeed, testDoc1, testDoc2);

            // change feed processor with manual checkpoint
            await this.ValidateChangeFeedProcessorWithManualCheckpointResponse(MdeEncryptionTests.encryptionContainerForChangeFeed, testDoc1, testDoc2);

            // change feed processor with feed stream handler
            await this.ValidateChangeFeedProcessorWithFeedStreamHandlerResponse(MdeEncryptionTests.encryptionContainerForChangeFeed, testDoc1, testDoc2);

            // change feed processor manual checkpoint with feed stream handler
            await this.ValidateChangeFeedProcessorStreamWithManualCheckpointResponse(MdeEncryptionTests.encryptionContainerForChangeFeed, testDoc1, testDoc2);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleDocs()
        {
            TestDoc testDoc1 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            TestDoc testDoc2 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            // test GetItemLinqQueryable
            await MdeEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(MdeEncryptionTests.encryptionContainer, testDoc1, testDoc2, null, expectedPropertiesDecryptedCount: 24);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";
            await MdeEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(MdeEncryptionTests.encryptionContainer, testDoc1, testDoc2, query, expectedPropertiesDecryptedCount: 24);

            // ORDER BY query
            query += " ORDER BY c._ts";
            await MdeEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(MdeEncryptionTests.encryptionContainer, testDoc1, testDoc2, query, expectedPropertiesDecryptedCount: 24);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryValueResponse()
        {
            await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            string query = "SELECT VALUE COUNT(1) FROM c";

            await MdeEncryptionTests.ValidateQueryResponseAsync(MdeEncryptionTests.encryptionContainer, query);
        }

        [TestMethod]
        public async Task EncryptionDecryptGroupByQueryResultTest()
        {
            string partitionKey = Guid.NewGuid().ToString();

            TestDoc testDoc1 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            string query = $"SELECT COUNT(c.Id), c.PK " +
                           $"FROM c WHERE c.PK = '{partitionKey}' " +
                           $"GROUP BY c.PK ";

            await MdeEncryptionTests.ValidateQueryResponseAsync(MdeEncryptionTests.encryptionContainer, query);

            QueryDefinition withEncryptedParameter =
                    MdeEncryptionTests.encryptionContainer.CreateQueryDefinition(
                    "SELECT COUNT(c.Id), c.Sensitive_IntFormat FROM c WHERE c.Sensitive_IntFormat = @Sensitive_IntFormat GROUP BY c.Sensitive_IntFormat ");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_IntFormat",
                    testDoc1.Sensitive_IntFormat,
                    "/Sensitive_IntFormat"); 

            FeedIterator feedIterator = MdeEncryptionTests.encryptionContainer.GetItemQueryStreamIterator(withEncryptedParameter);

            while (feedIterator.HasMoreResults)
            {
                ResponseMessage response = await feedIterator.ReadNextAsync();
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.IsNull(response.ErrorMessage);
            }
        }

        [TestMethod]
        public async Task EncryptionRestrictedProperties()
        {
            // restricted path id
            ClientEncryptionIncludedPath restrictedPathId = new ClientEncryptionIncludedPath()
            {
                Path = "/id",
                ClientEncryptionKeyId = "key1",
                EncryptionType = "Deterministic",
                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
            };

            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath> { restrictedPathId };
            try
            {
                ClientEncryptionPolicy clientEncryptionPolicyId = new ClientEncryptionPolicy(paths);
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("Invalid path '/id'.", ex.Message);
            }          
            
            // restricted path PK
            ClientEncryptionIncludedPath restrictedPathPk = new ClientEncryptionIncludedPath()
            {
                Path = "/PK",
                ClientEncryptionKeyId = "key2",
                EncryptionType = "Deterministic",
                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
            };

            Collection<ClientEncryptionIncludedPath> pathsRestrictedPathPk = new Collection<ClientEncryptionIncludedPath> { restrictedPathPk };
            ClientEncryptionPolicy clientEncryptionPolicyPk = new ClientEncryptionPolicy(pathsRestrictedPathPk);

            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK") { ClientEncryptionPolicy = clientEncryptionPolicyPk };

            try
            {
                Container encryptionContainer = await database.CreateContainerAsync(containerProperties, 400);
                Assert.Fail("CreateContainerAsync operation with PK specified to be encrypted should have failed. ");
            }
            catch (ArgumentException)
            {
            }

            // duplicate paths in policy.
            ClientEncryptionIncludedPath pathdup1 = new ClientEncryptionIncludedPath()
            {
                Path = "/Sensitive_StringFormat",
                ClientEncryptionKeyId = "key2",
                EncryptionType = "Deterministic",
                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
            };

            ClientEncryptionIncludedPath pathdup2 = new ClientEncryptionIncludedPath()
            {
                Path = "/Sensitive_StringFormat",
                ClientEncryptionKeyId = "key1",
                EncryptionType = "Deterministic",
                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
            };


            Collection<ClientEncryptionIncludedPath> pathsWithDups = new Collection<ClientEncryptionIncludedPath> { pathdup1 , pathdup2 };

            try
            {
                ClientEncryptionPolicy clientEncryptionPolicyWithDupPaths = new ClientEncryptionPolicy(pathsWithDups);
                Assert.Fail("Client Encryption Policy Creation Should have Failed.");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        public async Task EncryptionValidatePolicyRefreshPostContainerDeleteWithBulk()
        {
            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>()
            {
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_IntArray",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_ArrayFormat",
                    ClientEncryptionKeyId = "key2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_NestedObjectFormatL1",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
            };

            ClientEncryptionPolicy clientEncryptionPolicy = new ClientEncryptionPolicy(paths);

            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK") { ClientEncryptionPolicy = clientEncryptionPolicy };

            Container encryptionContainerToDelete = await database.CreateContainerAsync(containerProperties, 400);
            await encryptionContainerToDelete.InitializeEncryptionAsync();

            // FIXME Set WithBulkExecution to true post SDK/Backend fix.
            CosmosClient otherClient = TestCommon.CreateCosmosClient(builder => builder
                .WithBulkExecution(false)
                .Build());

            CosmosClient otherEncryptionClient = otherClient.WithEncryption(new TestEncryptionKeyStoreProvider());
            Database otherDatabase = otherEncryptionClient.GetDatabase(MdeEncryptionTests.database.Id);

            Container otherEncryptionContainer = otherDatabase.GetContainer(encryptionContainerToDelete.Id);

            await MdeEncryptionTests.MdeCreateItemAsync(otherEncryptionContainer);

            // Client 1 Deletes the Container referenced in Client 2 and Recreate with different policy
            using (await database.GetContainer(encryptionContainerToDelete.Id).DeleteContainerStreamAsync())
            { }

            paths = new Collection<ClientEncryptionIncludedPath>()
            {
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_StringFormat",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_DateFormat",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_BoolFormat",
                    ClientEncryptionKeyId = "key2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
            };

            clientEncryptionPolicy = new ClientEncryptionPolicy(paths);

            containerProperties = new ContainerProperties(encryptionContainerToDelete.Id, "/PK") { ClientEncryptionPolicy = clientEncryptionPolicy };

            ContainerResponse containerResponse = await database.CreateContainerAsync(containerProperties, 400);

            TestDoc docToReplace = await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainerToDelete);

            docToReplace.Sensitive_StringFormat = "docTobeReplace";

            TestDoc docToUpsert = await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainerToDelete);
            docToUpsert.Sensitive_StringFormat = "docTobeUpserted";

            List<Task> tasks = new List<Task>()
            {
                MdeEncryptionTests.MdeUpsertItemAsync(otherEncryptionContainer, docToUpsert, HttpStatusCode.OK),
                MdeEncryptionTests.MdeReplaceItemAsync(otherEncryptionContainer, docToReplace),
                MdeEncryptionTests.MdeCreateItemAsync(otherEncryptionContainer),
            };

            await Task.WhenAll(tasks);

            tasks = new List<Task>()
            {
                MdeEncryptionTests.VerifyItemByReadAsync(encryptionContainerToDelete, docToReplace),
                MdeEncryptionTests.MdeCreateItemAsync(encryptionContainerToDelete),
                MdeEncryptionTests.VerifyItemByReadAsync(encryptionContainerToDelete, docToUpsert),
            };

            await Task.WhenAll(tasks);

            // validate if the right policy was used, by reading them all back.
            FeedIterator<TestDoc> queryResponseIterator = otherEncryptionContainer.GetItemQueryIterator<TestDoc>("select * from c");

            while (queryResponseIterator.HasMoreResults)
            {
                FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();
            }
        }

        [TestMethod]
        public async Task EncryptionValidatePolicyRefreshPostContainerDeleteTransactionBatch()
        {
            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>()
            {
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_StringFormat",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_DateFormat",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
            };

            ClientEncryptionPolicy clientEncryptionPolicy = new ClientEncryptionPolicy(paths);

            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK") { ClientEncryptionPolicy = clientEncryptionPolicy };

            Container encryptionContainerToDelete = await database.CreateContainerAsync(containerProperties, 400);
            await encryptionContainerToDelete.InitializeEncryptionAsync();

            CosmosClient otherClient = TestCommon.CreateCosmosClient(builder => builder
                .Build());

            CosmosClient otherEncryptionClient = otherClient.WithEncryption(new TestEncryptionKeyStoreProvider());
            Database otherDatabase = otherEncryptionClient.GetDatabase(MdeEncryptionTests.database.Id);

            Container otherEncryptionContainer = otherDatabase.GetContainer(encryptionContainerToDelete.Id);

            await MdeEncryptionTests.MdeCreateItemAsync(otherEncryptionContainer);

            // Client 1 Deletes the Container referenced in Client 2 and Recreate with different policy
            using (await database.GetContainer(encryptionContainerToDelete.Id).DeleteContainerStreamAsync())
            { }

            paths = new Collection<ClientEncryptionIncludedPath>()
            {
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_IntArray",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_NestedObjectFormatL1",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
            };

            clientEncryptionPolicy = new ClientEncryptionPolicy(paths);

            containerProperties = new ContainerProperties(encryptionContainerToDelete.Id, "/PK") { ClientEncryptionPolicy = clientEncryptionPolicy };

            ContainerResponse containerResponse = await database.CreateContainerAsync(containerProperties, 400);

            TestDoc testDoc = await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainerToDelete);

            string partitionKey = "thePK";

            TestDoc doc1ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc2ToCreate = TestDoc.Create(partitionKey);

            // check w.r.t to Batch if we are able to fail and update the policy.
            TransactionalBatchResponse batchResponse = null;
            try
            {
                batchResponse = await otherEncryptionContainer.CreateTransactionalBatch(new Cosmos.PartitionKey(partitionKey))
                   .CreateItem(doc1ToCreate)
                   .CreateItemStream(doc2ToCreate.ToStream())
                   .ReadItem(doc1ToCreate.Id)
                   .DeleteItem(doc2ToCreate.Id)
                   .ExecuteAsync();

                Assert.Fail("CreateTransactionalBatch should have failed. ");
            }
            catch(CosmosException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Operation has failed due to a possible mismatch in Client Encryption Policy configured on the container."));
            }

            // the previous failure would have updated the policy in the cache.
            batchResponse = await otherEncryptionContainer.CreateTransactionalBatch(new Cosmos.PartitionKey(partitionKey))
               .CreateItem(doc1ToCreate)
               .CreateItemStream(doc2ToCreate.ToStream())
               .ReadItem(doc1ToCreate.Id)
               .DeleteItem(doc2ToCreate.Id)
               .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);

            TransactionalBatchOperationResult<TestDoc> doc1 = batchResponse.GetOperationResultAtIndex<TestDoc>(0);
            VerifyExpectedDocResponse(doc1ToCreate, doc1.Resource);

            TransactionalBatchOperationResult<TestDoc> doc2 = batchResponse.GetOperationResultAtIndex<TestDoc>(1);
            VerifyExpectedDocResponse(doc2ToCreate, doc2.Resource);
        }       

        [TestMethod]
        public async Task EncryptionValidatePolicyRefreshPostContainerDeleteQuery()
        {
            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>()
            {
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_StringFormat",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_NestedObjectFormatL1",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
            };

            ClientEncryptionPolicy clientEncryptionPolicy = new ClientEncryptionPolicy(paths);

            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK") { ClientEncryptionPolicy = clientEncryptionPolicy };

            Container encryptionContainerToDelete = await database.CreateContainerAsync(containerProperties, 400);
            await encryptionContainerToDelete.InitializeEncryptionAsync();

            CosmosClient otherClient = TestCommon.CreateCosmosClient(builder => builder
                .Build());

            CosmosClient otherEncryptionClient = otherClient.WithEncryption(new TestEncryptionKeyStoreProvider());
            Database otherDatabase = otherEncryptionClient.GetDatabase(MdeEncryptionTests.database.Id);

            Container otherEncryptionContainer = otherDatabase.GetContainer(encryptionContainerToDelete.Id);

            await MdeEncryptionTests.MdeCreateItemAsync(otherEncryptionContainer);

            // Client 1 Deletes the Container referenced in Client 2 and Recreate with different policy
            using (await database.GetContainer(encryptionContainerToDelete.Id).DeleteContainerStreamAsync())
            { }

            paths = new Collection<ClientEncryptionIncludedPath>()
            {
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_IntArray",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_ArrayFormat",
                    ClientEncryptionKeyId = "key2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
            };

            clientEncryptionPolicy = new ClientEncryptionPolicy(paths);

            containerProperties = new ContainerProperties(encryptionContainerToDelete.Id, "/PK") { ClientEncryptionPolicy = clientEncryptionPolicy };

            ContainerResponse containerResponse = await database.CreateContainerAsync(containerProperties, 400);

            TestDoc testDoc = await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainerToDelete);

            // check w.r.t to query if we are able to fail and update the policy
            try
            {
                await MdeEncryptionTests.ValidateQueryResultsAsync(
                       otherEncryptionContainer,
                       "SELECT * FROM c",
                       testDoc);

                Assert.Fail("ValidateQueryResultAsync should have failed. ");
            }
            catch(CosmosException ex)
            {
                if (ex.SubStatusCode != 1024)
                {
                    Assert.Fail("Query should have failed with 1024 SubStatusCode. ");
                }

                Assert.IsTrue(ex.Message.Contains("Operation has failed due to a possible mismatch in Client Encryption Policy configured on the container."));
            }

            // previous failure would have updated the policy in the cache.
            await MdeEncryptionTests.ValidateQueryResultsAsync(
                otherEncryptionContainer,
                "SELECT * FROM c",
                testDoc,
                expectedPropertiesDecryptedCount: 2);
        }

        [TestMethod]
        public async Task EncryptionValidatePolicyRefreshPostDatabaseDelete()
        {
            CosmosClient mainClient = TestCommon.CreateCosmosClient(builder => builder
                .Build());

            TestEncryptionKeyStoreProvider testEncryptionKeyStoreProvider = new TestEncryptionKeyStoreProvider
            {
                DataEncryptionKeyCacheTimeToLive = TimeSpan.FromMinutes(30),
            };

            EncryptionKeyWrapMetadata keyWrapMetadata = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, "myCek", "mymetadata1");
            CosmosClient encryptionCosmosClient = mainClient.WithEncryption(testEncryptionKeyStoreProvider);
            Database mainDatabase = await encryptionCosmosClient.CreateDatabaseAsync("databaseToBeDeleted");

            ClientEncryptionKeyResponse clientEncrytionKeyResponse = await mainDatabase.CreateClientEncryptionKeyAsync(
                   keyWrapMetadata.Name,
                   DataEncryptionKeyAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256,
                   keyWrapMetadata);

            Collection<ClientEncryptionIncludedPath> originalPaths = new Collection<ClientEncryptionIncludedPath>()
            {
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_StringFormat",
                    ClientEncryptionKeyId = "myCek",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_ArrayFormat",
                    ClientEncryptionKeyId = "myCek",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_NestedObjectFormatL1",
                    ClientEncryptionKeyId = "myCek",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
            };

            ClientEncryptionPolicy clientEncryptionPolicy = new ClientEncryptionPolicy(originalPaths);

            ContainerProperties containerProperties = new ContainerProperties("containerToBeDeleted", "/PK") { ClientEncryptionPolicy = clientEncryptionPolicy };

            Container encryptionContainerToDelete = await mainDatabase.CreateContainerAsync(containerProperties, 400);
            await encryptionContainerToDelete.InitializeEncryptionAsync();

            await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainerToDelete);

            CosmosClient otherClient1 = TestCommon.CreateCosmosClient(builder => builder
                .Build());

            TestEncryptionKeyStoreProvider testEncryptionKeyStoreProvider2 = new TestEncryptionKeyStoreProvider
            {
                DataEncryptionKeyCacheTimeToLive = TimeSpan.Zero,
            };

            CosmosClient otherEncryptionClient = otherClient1.WithEncryption(testEncryptionKeyStoreProvider2);
            Database otherDatabase = otherEncryptionClient.GetDatabase(mainDatabase.Id);

            Container otherEncryptionContainer = otherDatabase.GetContainer(encryptionContainerToDelete.Id);

            await MdeEncryptionTests.MdeCreateItemAsync(otherEncryptionContainer);

            // Client 1 Deletes the Database and  Container referenced in Client 2 and Recreate with different policy
            // delete database and recreate with same key name
            await mainClient.GetDatabase("databaseToBeDeleted").DeleteStreamAsync();

            mainDatabase = await encryptionCosmosClient.CreateDatabaseAsync("databaseToBeDeleted");

            keyWrapMetadata = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider2.ProviderName, "myCek", "mymetadata2");
            clientEncrytionKeyResponse = await mainDatabase.CreateClientEncryptionKeyAsync(
                   keyWrapMetadata.Name,
                   DataEncryptionKeyAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256,
                   keyWrapMetadata);

            using (await mainDatabase.GetContainer(encryptionContainerToDelete.Id).DeleteContainerStreamAsync())
            { }

            Collection<ClientEncryptionIncludedPath> newModifiedPath = new Collection<ClientEncryptionIncludedPath>()
            {
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_IntArray",
                    ClientEncryptionKeyId = "myCek",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_DateFormat",
                    ClientEncryptionKeyId = "myCek",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_BoolFormat",
                    ClientEncryptionKeyId = "myCek",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
            };

            clientEncryptionPolicy = new ClientEncryptionPolicy(newModifiedPath);

            containerProperties = new ContainerProperties(encryptionContainerToDelete.Id, "/PK") { ClientEncryptionPolicy = clientEncryptionPolicy };

            ContainerResponse containerResponse = await mainDatabase.CreateContainerAsync(containerProperties, 400);
            
            TestDoc testDoc = await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainerToDelete);

            // retry should be a success.
            await MdeEncryptionTests.VerifyItemByReadAsync(otherEncryptionContainer, testDoc);

            // create new container in other client.
            // The test basically validates if the new key created is referenced, Since the other client would have had the old key cached.
            // and here we would not hit the incorrect container rid issue.
            ClientEncryptionIncludedPath newModifiedPath2 = new ClientEncryptionIncludedPath()
            {
                Path = "/Sensitive_StringFormat",
                ClientEncryptionKeyId = "myCek",
                EncryptionType = "Deterministic",
                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
            };

            Container otherEncryptionContainer2 = await otherDatabase.DefineContainer("otherContainer2", "/PK")
                .WithClientEncryptionPolicy()
                .WithIncludedPath(newModifiedPath2)                
                .Attach()
                .CreateAsync(throughput: 1000);


            // create an item
            TestDoc newdoc = await MdeEncryptionTests.MdeCreateItemAsync(otherEncryptionContainer2);

            CosmosClient otherClient2 = TestCommon.CreateCosmosClient(builder => builder
                .WithBulkExecution(true)
                .Build());

            TestEncryptionKeyStoreProvider testEncryptionKeyStoreProvider3 = new TestEncryptionKeyStoreProvider
            {
                DataEncryptionKeyCacheTimeToLive = TimeSpan.FromMinutes(30),
            };

            CosmosClient otherEncryptionClient2 = otherClient2.WithEncryption(testEncryptionKeyStoreProvider3);
            Database otherDatabase2 = otherEncryptionClient2.GetDatabase(mainDatabase.Id);

            Container otherEncryptionContainer3 = otherDatabase2.GetContainer(otherEncryptionContainer2.Id);
            await MdeEncryptionTests.VerifyItemByReadAsync(otherEncryptionContainer3, newdoc);

            // validate from other client that we indeed are using the key with metadata 2
            Container otherEncryptionContainerFromClient2 = otherDatabase2.GetContainer(encryptionContainerToDelete.Id);
            await MdeEncryptionTests.VerifyItemByReadAsync(otherEncryptionContainerFromClient2, testDoc);

            // previous refrenced container.
            await MdeEncryptionTests.MdeCreateItemAsync(otherEncryptionContainer);

            List<Task> tasks = new List<Task>()
            {
                // readback item and create item from other Client.  
                MdeEncryptionTests.MdeCreateItemAsync(otherEncryptionContainer),
                MdeEncryptionTests.VerifyItemByReadAsync(otherEncryptionContainer, testDoc),                
            };

            await Task.WhenAll(tasks);

            testDoc = await MdeEncryptionTests.MdeCreateItemAsync(otherEncryptionContainer);

            // to be sure if it was indeed encrypted with the new key.
            await MdeEncryptionTests.VerifyItemByReadAsync(encryptionContainerToDelete, testDoc);


            // validate if the right policy was used, by reading them all back.
            FeedIterator<TestDoc> queryResponseIterator = encryptionContainerToDelete.GetItemQueryIterator<TestDoc>("select * from c");

            while (queryResponseIterator.HasMoreResults)
            {
                await queryResponseIterator.ReadNextAsync();
            }

            queryResponseIterator = otherEncryptionContainer.GetItemQueryIterator<TestDoc>("select * from c");

            while (queryResponseIterator.HasMoreResults)
            {
                await queryResponseIterator.ReadNextAsync();
            }

            await mainClient.GetDatabase("databaseToBeDeleted").DeleteStreamAsync();
        }

        [TestMethod]
        public async Task VerifyKekRevokeHandling()
        {
            CosmosClient clientWithNoCaching = TestCommon.CreateCosmosClient(builder => builder
                .Build());

            TestEncryptionKeyStoreProvider testEncryptionKeyStoreProvider = new TestEncryptionKeyStoreProvider
            {
                DataEncryptionKeyCacheTimeToLive = TimeSpan.Zero
            };

            CosmosClient encryptionCosmosClient = clientWithNoCaching.WithEncryption(testEncryptionKeyStoreProvider);
            Database database = encryptionCosmosClient.GetDatabase(MdeEncryptionTests.database.Id);

            // Once a Dek gets cached and the Kek is revoked, calls to unwrap/wrap keys would fail since KEK is revoked.
            // The Dek should be rewrapped if the KEK is revoked.
            // When an access to KeyVault fails, the Dek is fetched from the backend(force refresh to update the stale DEK) and cache is updated.
            ClientEncryptionIncludedPath pathwithRevokedKek = new ClientEncryptionIncludedPath()
            {
                Path = "/Sensitive_NestedObjectFormatL1",
                ClientEncryptionKeyId = "keywithRevokedKek",
                EncryptionType = "Deterministic",
                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
            };

            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath> { pathwithRevokedKek };

            ClientEncryptionPolicy clientEncryptionPolicyWithRevokedKek = new ClientEncryptionPolicy(paths);       

            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK") { ClientEncryptionPolicy = clientEncryptionPolicyWithRevokedKek };

            Container encryptionContainer = await database.CreateContainerAsync(containerProperties, 400);

            TestDoc testDoc1 = await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainer);

            testEncryptionKeyStoreProvider.RevokeAccessSet = true;

            // try creating it and it should fail as it has been revoked.
            try
            {
                await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainer);
                Assert.Fail("Create Item should have failed.");
            }
            catch(RequestFailedException)
            {               
            }

            // testing query read fail due to revoked access.
            try
            {
                await MdeEncryptionTests.ValidateQueryResultsAsync(
                encryptionContainer,
                "SELECT * FROM c",
                testDoc1);
                Assert.Fail("Query should have failed, since property path /Sensitive_NestedObjectFormatL1 has been encrypted using Cek with revoked access. ");
            }
            catch (RequestFailedException)
            {
            }

            // for unwrap to succeed 
            testEncryptionKeyStoreProvider.RevokeAccessSet = false;

            // lets rewrap it.
            await database.RewrapClientEncryptionKeyAsync("keywithRevokedKek", MdeEncryptionTests.metadata2);

            testEncryptionKeyStoreProvider.RevokeAccessSet = true;
            // Should fail but will try to fetch the lastest from the Backend and updates the cache.
            await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainer);
            testEncryptionKeyStoreProvider.RevokeAccessSet = false;
            testEncryptionKeyStoreProvider.DataEncryptionKeyCacheTimeToLive = TimeSpan.FromMinutes(120);
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithNoClientEncryptionPolicy()
        {
            await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            // a database can have both types of Containers - with and without ClientEncryptionPolicy configured
            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK");

            Container encryptionContainerWithNoPolicy = await database.CreateContainerAsync(containerProperties, 400);
            await encryptionContainerWithNoPolicy.InitializeEncryptionAsync();

            TestDoc testDoc = TestDoc.Create();

            ItemResponse<TestDoc> createResponse = await encryptionContainerWithNoPolicy.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            VerifyExpectedDocResponse(testDoc, createResponse.Resource);

            QueryDefinition withEncryptedParameter = encryptionContainerWithNoPolicy.CreateQueryDefinition(
                "SELECT * FROM c where c.Sensitive_StringFormat = @Sensitive_StringFormat AND c.Sensitive_IntFormat = @Sensitive_IntFormat");

            await withEncryptedParameter.AddParameterAsync(
                "@Sensitive_StringFormat",
                testDoc.Sensitive_StringFormat,
                "/Sensitive_StringFormat");

            await withEncryptedParameter.AddParameterAsync(
                "@Sensitive_IntFormat",
                testDoc.Sensitive_IntFormat,
                "/Sensitive_IntFormat");

            TestDoc expectedDoc = new TestDoc(testDoc);
            await MdeEncryptionTests.ValidateQueryResultsAsync(
                encryptionContainerWithNoPolicy,
                queryDefinition: withEncryptedParameter,
                expectedDoc: expectedDoc,
                decryptOperation: false);

            await encryptionContainerWithNoPolicy.DeleteContainerAsync();
        }

        [TestMethod]
        public async Task CreateAndDeleteDatabaseWithoutKeys()
        {
            Database database = await MdeEncryptionTests.encryptionCosmosClient.CreateDatabaseAsync("NoCEKDatabase");
            ContainerProperties containerProperties = new ContainerProperties("NoCEPContainer", "/PK");

            Container encryptionContainer = await database.CreateContainerAsync(containerProperties, 400);
            await encryptionContainer.InitializeEncryptionAsync();

            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await encryptionContainer.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            VerifyExpectedDocResponse(testDoc, createResponse.Resource);

            await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainer);
            await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainer);
            await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainer);


            await database.DeleteStreamAsync();
        }

        [TestMethod]
        public async Task EncryptionStreamIteratorValidation()
        {
            await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            // test GetItemLinqQueryable with ToEncryptionStreamIterator extension
            await MdeEncryptionTests.ValidateQueryResponseAsync(MdeEncryptionTests.encryptionContainer, expectedPropertiesDecryptedCount: 24);
        }

        [TestMethod]
        public async Task EncryptionDiagnosticsTest()
        {
            ItemResponse<TestDoc> createResponse = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            VerifyDiagnostics(createResponse.Diagnostics);

            TestDoc testDoc = createResponse.Resource;

            ResponseMessage readResponse = await MdeEncryptionTests.encryptionContainer.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK));
            VerifyDiagnostics(readResponse.Diagnostics, encryptOperation: false, decryptOperation: true);

            TestDoc testDoc1 = TestDoc.Create();
            testDoc1.NonSensitive = Guid.NewGuid().ToString();
            testDoc1.Sensitive_StringFormat = Guid.NewGuid().ToString();
            ItemResponse<TestDoc> upsertResponse = await MdeEncryptionTests.MdeUpsertItemAsync(
                MdeEncryptionTests.encryptionContainer,
                testDoc1,
                HttpStatusCode.Created);
            TestDoc upsertedDoc = upsertResponse.Resource;
            VerifyDiagnostics(upsertResponse.Diagnostics);

            upsertedDoc.NonSensitive = Guid.NewGuid().ToString();
            upsertedDoc.Sensitive_StringFormat = Guid.NewGuid().ToString();

            ItemResponse<TestDoc> replaceResponse = await MdeEncryptionTests.MdeReplaceItemAsync(
                MdeEncryptionTests.encryptionContainer,
                upsertedDoc,
                upsertResponse.ETag);
            VerifyDiagnostics(replaceResponse.Diagnostics);
        }

        [TestMethod]
        public async Task EncryptionRudItem()
        {
            TestDoc testDoc = await MdeEncryptionTests.MdeUpsertItemAsync(
                MdeEncryptionTests.encryptionContainer,
                TestDoc.Create(),
                HttpStatusCode.Created);

            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, testDoc);

            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive_StringFormat = Guid.NewGuid().ToString();

            ItemResponse<TestDoc> upsertResponse = await MdeEncryptionTests.MdeUpsertItemAsync(
                MdeEncryptionTests.encryptionContainer,
                testDoc,
                HttpStatusCode.OK);
            TestDoc updatedDoc = upsertResponse.Resource;

            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, updatedDoc);

            updatedDoc.NonSensitive = Guid.NewGuid().ToString();
            updatedDoc.Sensitive_StringFormat = Guid.NewGuid().ToString();

            TestDoc replacedDoc = await MdeEncryptionTests.MdeReplaceItemAsync(
                MdeEncryptionTests.encryptionContainer,
                updatedDoc,
                upsertResponse.ETag);

            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, replacedDoc);

            await MdeEncryptionTests.MdeDeleteItemAsync(MdeEncryptionTests.encryptionContainer, replacedDoc);
        }

        [TestMethod]
        public async Task EncryptionPatchItem()
        {
            TestDoc docPostPatching = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            docPostPatching.NonSensitive = Guid.NewGuid().ToString();
            docPostPatching.NonSensitiveInt++;
            docPostPatching.Sensitive_StringFormat = Guid.NewGuid().ToString();
            docPostPatching.Sensitive_DateFormat = new DateTime(2020, 02, 02);
            docPostPatching.Sensitive_DecimalFormat = 11.11m;
            docPostPatching.Sensitive_IntArray[1] = 1;
            docPostPatching.Sensitive_IntMultiDimArray[1, 0] = 7;
            docPostPatching.Sensitive_IntFormat = 2020;
            docPostPatching.Sensitive_BoolFormat = false;
            docPostPatching.Sensitive_FloatFormat = 2020.20f;

            // Maximum 10 operations at a time (current limit)
            List<PatchOperation> patchOperations = new List<PatchOperation>
            {
                PatchOperation.Increment("/NonSensitiveInt", 1),
                PatchOperation.Replace("/NonSensitive", docPostPatching.NonSensitive),
                PatchOperation.Replace("/Sensitive_StringFormat", docPostPatching.Sensitive_StringFormat),
                PatchOperation.Replace("/Sensitive_DateFormat", docPostPatching.Sensitive_DateFormat),
                PatchOperation.Replace("/Sensitive_DecimalFormat", docPostPatching.Sensitive_DecimalFormat),
                PatchOperation.Set("/Sensitive_IntArray/1", docPostPatching.Sensitive_IntArray[1]),
                PatchOperation.Set("/Sensitive_IntMultiDimArray/1/0", docPostPatching.Sensitive_IntMultiDimArray[1,0]),
                PatchOperation.Replace("/Sensitive_IntFormat", docPostPatching.Sensitive_IntFormat),
                PatchOperation.Replace("/Sensitive_BoolFormat", docPostPatching.Sensitive_BoolFormat),
                PatchOperation.Replace("/Sensitive_FloatFormat", docPostPatching.Sensitive_FloatFormat),
            };

            ItemResponse<TestDoc> patchResponse = await MdeEncryptionTests.MdePatchItemAsync(
                MdeEncryptionTests.encryptionContainer,
                patchOperations,
                docPostPatching,
                HttpStatusCode.OK);

            VerifyDiagnostics(patchResponse.Diagnostics, expectedPropertiesEncryptedCount: 8);

            docPostPatching.Sensitive_ArrayFormat = new TestDoc.Sensitive_ArrayData[]
            {
                new TestDoc.Sensitive_ArrayData
                {
                    Sensitive_ArrayIntFormat = 1111,
                    Sensitive_ArrayDecimalFormat = 1111.11m
                },
                new TestDoc.Sensitive_ArrayData
                {
                    Sensitive_ArrayIntFormat = 2222,
                    Sensitive_ArrayDecimalFormat = 2222.22m
                }
            };

            docPostPatching.Sensitive_ArrayMultiTypes.SetValue(
                new TestDoc.Sensitive_ArrayMultiType()
                {
                    Sensitive_NestedObjectFormatL0 = new TestDoc.Sensitive_NestedObjectL0()
                    {
                        Sensitive_IntFormatL0 = 123,
                        Sensitive_DecimalFormatL0 = 123.1m,
                    },
                    Sensitive_StringArrayMultiType = new string[1] { "sensitivedata" },
                    Sensitive_ArrayMultiTypeDecimalFormat = 123.2m,
                    Sensitive_IntArrayMultiType = new int[2] { 1, 2 }
                },
                0,
                1);

            docPostPatching.Sensitive_NestedObjectFormatL1 = new TestDoc.Sensitive_NestedObjectL1()
            {
                Sensitive_IntArrayL1 = new int[2] { 999, 100 },
                Sensitive_IntFormatL1 = 1999,
                Sensitive_DecimalFormatL1 = 1999.1m,
                Sensitive_ArrayFormatL1 = new TestDoc.Sensitive_ArrayData[]
                {
                    new TestDoc.Sensitive_ArrayData
                    {
                        Sensitive_ArrayIntFormat = 0,
                        Sensitive_ArrayDecimalFormat = 0.1m
                    },
                    new TestDoc.Sensitive_ArrayData
                    {
                        Sensitive_ArrayIntFormat = 1,
                        Sensitive_ArrayDecimalFormat = 2.1m
                    },
                    new TestDoc.Sensitive_ArrayData
                    {
                        Sensitive_ArrayIntFormat = 2,
                        Sensitive_ArrayDecimalFormat = 3.1m
                    }
                }
            };

            patchOperations.Clear();
            patchOperations.Add(PatchOperation.Add("/Sensitive_ArrayFormat/1", docPostPatching.Sensitive_ArrayFormat[1]));
            patchOperations.Add(PatchOperation.Replace("/Sensitive_ArrayMultiTypes/0/1", docPostPatching.Sensitive_ArrayMultiTypes[0, 1]));
            patchOperations.Add(PatchOperation.Remove("/Sensitive_NestedObjectFormatL1/Sensitive_NestedObjectFormatL2"));
            patchOperations.Add(PatchOperation.Set("/Sensitive_NestedObjectFormatL1/Sensitive_ArrayFormatL1/0", docPostPatching.Sensitive_NestedObjectFormatL1.Sensitive_ArrayFormatL1[0]));

            patchResponse = await MdeEncryptionTests.MdePatchItemAsync(
                MdeEncryptionTests.encryptionContainer,
                patchOperations,
                docPostPatching,
                HttpStatusCode.OK);

            VerifyDiagnostics(patchResponse.Diagnostics, expectedPropertiesEncryptedCount: 3);

            patchOperations.Add(PatchOperation.Increment("/Sensitive_IntFormat", 1));
            try
            {
                await MdeEncryptionTests.encryptionContainer.PatchItemAsync<TestDoc>(
                    docPostPatching.Id,
                    new PartitionKey(docPostPatching.PK),
                    patchOperations);
            }
            catch(InvalidOperationException ex)
            {
                Assert.AreEqual("Increment patch operation is not allowed for encrypted path '/Sensitive_IntFormat'.", ex.Message);
            }
        }

        [TestMethod]
        public async Task EncryptionTransactionalBatchWithCustomSerializer()
        {
            CustomSerializer customSerializer = new CustomSerializer();
            CosmosClient clientWithCustomSerializer = TestCommon.CreateCosmosClient(builder => builder
                .WithCustomSerializer(customSerializer)
                .Build());

            CosmosClient encryptionCosmosClientWithCustomSerializer = clientWithCustomSerializer.WithEncryption(new TestEncryptionKeyStoreProvider());
            Database databaseWithCustomSerializer = encryptionCosmosClientWithCustomSerializer.GetDatabase(MdeEncryptionTests.database.Id);

            Container encryptionContainerWithCustomSerializer = databaseWithCustomSerializer.GetContainer(MdeEncryptionTests.encryptionContainer.Id);
            string partitionKey = "thePK";
            TestDoc doc1ToCreate = TestDoc.Create(partitionKey);

            ItemResponse<TestDoc> doc1ToReplaceCreateResponse = await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainerWithCustomSerializer, partitionKey);

            TestDoc doc1ToReplace = doc1ToReplaceCreateResponse.Resource;
            doc1ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc1ToReplace.Sensitive_StringFormat = Guid.NewGuid().ToString();

            TransactionalBatchResponse batchResponse = await encryptionContainerWithCustomSerializer.CreateTransactionalBatch(new Cosmos.PartitionKey(partitionKey))
                .CreateItem(doc1ToCreate)
                .ReplaceItem(doc1ToReplace.Id, doc1ToReplace, new TransactionalBatchItemRequestOptions { IfMatchEtag = doc1ToReplaceCreateResponse.ETag })
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);
            // FromStream is called as part of CreateItem request
            Assert.AreEqual(1, customSerializer.FromStreamCalled);

            TransactionalBatchOperationResult<TestDoc> doc1 = batchResponse.GetOperationResultAtIndex<TestDoc>(0);
            VerifyExpectedDocResponse(doc1ToCreate, doc1.Resource);
            Assert.AreEqual(2, customSerializer.FromStreamCalled);

            TransactionalBatchOperationResult<TestDoc> doc2 = batchResponse.GetOperationResultAtIndex<TestDoc>(1);
            VerifyExpectedDocResponse(doc1ToReplace, doc2.Resource);
            Assert.AreEqual(3, customSerializer.FromStreamCalled);

            await MdeEncryptionTests.VerifyItemByReadAsync(encryptionContainerWithCustomSerializer, doc1ToCreate);
            await MdeEncryptionTests.VerifyItemByReadAsync(encryptionContainerWithCustomSerializer, doc1ToReplace);

            // Query on container with Custom Serializer.
            QueryDefinition withEncryptedParameter = encryptionContainerWithCustomSerializer.CreateQueryDefinition(
                   "SELECT * FROM c where c.Sensitive_StringFormat = @Sensitive_StringFormat AND c.Sensitive_IntFormat = @Sensitive_IntFormat");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_StringFormat",
                    doc1ToReplace.Sensitive_StringFormat,
                    "/Sensitive_StringFormat");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_IntFormat",
                    doc1ToReplace.Sensitive_IntFormat,
                    "/Sensitive_IntFormat");

            TestDoc expectedDoc = new TestDoc(doc1ToReplaceCreateResponse);
            await MdeEncryptionTests.ValidateQueryResultsAsync(
                encryptionContainerWithCustomSerializer,
                queryDefinition: withEncryptedParameter,
                expectedDoc: expectedDoc);
        }

        [TestMethod]
        public async Task ValidateCachingofProtectedDataEncryptionKey()
        {
            // Default cache TTL 2 hours.
            TestEncryptionKeyStoreProvider newtestEncryptionKeyStoreProvider = new TestEncryptionKeyStoreProvider();           
            CosmosClient newEncryptionClient = MdeEncryptionTests.client.WithEncryption(newtestEncryptionKeyStoreProvider);
            Database database = newEncryptionClient.GetDatabase(MdeEncryptionTests.database.Id);

            Container encryptionContainer = database.GetContainer(MdeEncryptionTests.encryptionContainer.Id);

            for (int i = 0; i < 2; i++)
            {
                await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainer);
            }

            newtestEncryptionKeyStoreProvider.UnWrapKeyCallsCount.TryGetValue(metadata1.Value, out int unwrapcount);
            // expecting just one unwrap.
            Assert.AreEqual(1, unwrapcount);

            // no caching.
            newtestEncryptionKeyStoreProvider = new TestEncryptionKeyStoreProvider()
            {
                DataEncryptionKeyCacheTimeToLive = TimeSpan.Zero,
            };

            newEncryptionClient = MdeEncryptionTests.client.WithEncryption(newtestEncryptionKeyStoreProvider);
            database = newEncryptionClient.GetDatabase(MdeEncryptionTests.database.Id);

            encryptionContainer = database.GetContainer(MdeEncryptionTests.encryptionContainer.Id);

            for (int i = 0; i < 2; i++)
            {
                await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainer);
            }

            newtestEncryptionKeyStoreProvider.UnWrapKeyCallsCount.TryGetValue(metadata1.Value, out unwrapcount);
            Assert.IsTrue(unwrapcount > 1, "The actual unwrap count was not greater than 1");
        }

        private static async Task ValidateQueryResultsMultipleDocumentsAsync(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2,
            string query,
            bool compareEncryptedProperty = true,
            int expectedPropertiesDecryptedCount = 0)
        {
            FeedIterator<TestDoc> queryResponseIterator;

            if (query == null)
            {
                IOrderedQueryable<TestDoc> linqQueryable = container.GetItemLinqQueryable<TestDoc>();
                queryResponseIterator = container.ToEncryptionFeedIterator<TestDoc>(linqQueryable);
            }
            else
            {
                queryResponseIterator = container.GetItemQueryIterator<TestDoc>(query);
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
                    if (compareEncryptedProperty)
                    {
                        VerifyExpectedDocResponse(readDocs.ElementAt(index), testDoc1);
                    }
                    else
                    {
                        testDoc1.EqualsExceptEncryptedProperty(readDocs.ElementAt(index));
                    }
                }
                else if (readDocs.ElementAt(index).Id.Equals(testDoc2.Id))
                {
                    if (compareEncryptedProperty)
                    {
                        VerifyExpectedDocResponse(readDocs.ElementAt(index), testDoc2);
                    }
                    else
                    {
                        testDoc2.EqualsExceptEncryptedProperty(readDocs.ElementAt(index));
                    }
                }
            }
            
            VerifyDiagnostics(readDocs.Diagnostics, encryptOperation: false, expectedPropertiesDecryptedCount: expectedPropertiesDecryptedCount);
        }

        private static async Task ValidateQueryResponseAsync(
            Container container,
            string query = null,
            int expectedPropertiesDecryptedCount = 0)
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
                VerifyDiagnostics(response.Diagnostics, encryptOperation: false, expectedPropertiesDecryptedCount: expectedPropertiesDecryptedCount);
            }
        }

        private async Task ValidateChangeFeedIteratorResponse(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2)
        {
            FeedIterator<TestDoc> changeIterator = container.GetChangeFeedIterator<TestDoc>(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.Incremental);

            while (changeIterator.HasMoreResults)
            {
                FeedResponse<TestDoc> testDocs = await changeIterator.ReadNextAsync();
                if (testDocs.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }

                Assert.AreEqual(testDocs.Count, 2);

                VerifyExpectedDocResponse(testDoc1, testDocs.Resource.ElementAt(0));
                VerifyExpectedDocResponse(testDoc2, testDocs.Resource.ElementAt(1));

                VerifyDiagnostics(testDocs.Diagnostics, encryptOperation: false, expectedPropertiesDecryptedCount: 24);
            }
        }

        private async Task ValidateChangeFeedProcessorResponse(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2)
        {
            Database leaseDatabase = await MdeEncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            Container leaseContainer = await leaseDatabase.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id: "leases", partitionKeyPath: "/id"));
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);
            int processedDocCount = 0;

            List<TestDoc> changeFeedReturnedDocs = new List<TestDoc>();
            ChangeFeedProcessor cfp = container.GetChangeFeedProcessorBuilder(
                "testCFP",
                (IReadOnlyCollection<TestDoc> changes, CancellationToken cancellationToken) =>
                {
                    changeFeedReturnedDocs.AddRange(changes);
                    processedDocCount += changes.Count();
                    if(processedDocCount == 2)
                    {
                        allDocsProcessed.Set();
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithLeaseContainer(leaseContainer)
                .WithStartTime(DateTime.MinValue.ToUniversalTime())
                .Build();

            await cfp.StartAsync();
            bool isStartOk = allDocsProcessed.WaitOne(60000);
            await cfp.StopAsync();

            Assert.AreEqual(changeFeedReturnedDocs.Count, 2);

            VerifyExpectedDocResponse(testDoc1, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 2]);
            VerifyExpectedDocResponse(testDoc2, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 1]);

            if (leaseDatabase != null)
            {
                using (await leaseDatabase.DeleteStreamAsync()) { }
            }
        }

        private async Task ValidateChangeFeedProcessorWithFeedHandlerResponse(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2)
        {
            Database leaseDatabase = await MdeEncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            Container leaseContainer = await leaseDatabase.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id: "leases", partitionKeyPath: "/id"));
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);
            int processedDocCount = 0;

            List<TestDoc> changeFeedReturnedDocs = new List<TestDoc>();
            ChangeFeedProcessor cfp = container.GetChangeFeedProcessorBuilder(
                "testCFPWithFeedHandler",
                (
                    ChangeFeedProcessorContext context,
                    IReadOnlyCollection<TestDoc> changes,
                    CancellationToken cancellationToken) =>
                {
                    changeFeedReturnedDocs.AddRange(changes);
                    processedDocCount += changes.Count();
                    if (processedDocCount == 2)
                    {
                        allDocsProcessed.Set();
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithLeaseContainer(leaseContainer)
                .WithStartTime(DateTime.MinValue.ToUniversalTime())
                .Build();

            await cfp.StartAsync();
            bool isStartOk = allDocsProcessed.WaitOne(60000);
            await cfp.StopAsync();

            Assert.AreEqual(changeFeedReturnedDocs.Count, 2);

            VerifyExpectedDocResponse(testDoc1, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 2]);
            VerifyExpectedDocResponse(testDoc2, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 1]);

            if (leaseDatabase != null)
            {
                using (await leaseDatabase.DeleteStreamAsync()) { }
            }
        }

        private async Task ValidateChangeFeedProcessorWithManualCheckpointResponse(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2)
        {
            Database leaseDatabase = await MdeEncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            Container leaseContainer = await leaseDatabase.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id: "leases", partitionKeyPath: "/id"));
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);
            int processedDocCount = 0;

            List<TestDoc> changeFeedReturnedDocs = new List<TestDoc>();
            ChangeFeedProcessor cfp = container.GetChangeFeedProcessorBuilderWithManualCheckpoint(
                "testCFPWithManualCheckpoint",
                (
                    ChangeFeedProcessorContext context,
                    IReadOnlyCollection<TestDoc> changes,
                    Func<Task> tryCheckpointAsync,
                    CancellationToken cancellationToken) =>
                {
                    changeFeedReturnedDocs.AddRange(changes);
                    processedDocCount += changes.Count();
                    if (processedDocCount == 2)
                    {
                        allDocsProcessed.Set();
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithLeaseContainer(leaseContainer)
                .WithStartTime(DateTime.MinValue.ToUniversalTime())
                .Build();

            await cfp.StartAsync();
            bool isStartOk = allDocsProcessed.WaitOne(60000);
            await cfp.StopAsync();

            Assert.AreEqual(changeFeedReturnedDocs.Count, 2);

            VerifyExpectedDocResponse(testDoc1, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 2]);
            VerifyExpectedDocResponse(testDoc2, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 1]);

            if (leaseDatabase != null)
            {
                using (await leaseDatabase.DeleteStreamAsync()) { }
            }
        }

        private async Task ValidateChangeFeedProcessorWithFeedStreamHandlerResponse(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2)
        {
            Database leaseDatabase = await MdeEncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            Container leaseContainer = await leaseDatabase.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id: "leases", partitionKeyPath: "/id"));
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);
            int processedDocCount = 0;

            ChangeFeedProcessor cfp = container.GetChangeFeedProcessorBuilder(
                "testCFPWithFeedStreamHandler",
                (
                    ChangeFeedProcessorContext context,
                    Stream changes,
                    CancellationToken cancellationToken) =>
                {
                    string changeFeed = string.Empty;
                    using (StreamReader streamReader = new StreamReader(changes))
                    {
                        changeFeed = streamReader.ReadToEnd();
                    }

                    if (changeFeed.Contains(testDoc1.Id))
                    {
                        processedDocCount++;
                    }
                    
                    if (changeFeed.Contains(testDoc2.Id))
                    { 
                        processedDocCount++;
                    }

                    if (processedDocCount == 2)
                    {
                        allDocsProcessed.Set();
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithLeaseContainer(leaseContainer)
                .WithStartTime(DateTime.MinValue.ToUniversalTime())
                .Build();

            await cfp.StartAsync();
            bool isStartOk = allDocsProcessed.WaitOne(60000);
            await cfp.StopAsync();

            if (leaseDatabase != null)
            {
                using (await leaseDatabase.DeleteStreamAsync()) { }
            }
        }

        private async Task ValidateChangeFeedProcessorStreamWithManualCheckpointResponse(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2)
        {
            Database leaseDatabase = await MdeEncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            Container leaseContainer = await leaseDatabase.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id: "leases", partitionKeyPath: "/id"));
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);
            int processedDocCount = 0;

            ChangeFeedProcessor cfp = container.GetChangeFeedProcessorBuilderWithManualCheckpoint(
                "testCFPStreamWithManualCheckpoint",
                (
                    ChangeFeedProcessorContext context,
                    Stream changes,
                    Func<Task> tryCheckpointAsync,
                    CancellationToken cancellationToken) =>
                {
                    string changeFeed = string.Empty;
                    using (StreamReader streamReader = new StreamReader(changes))
                    {
                        changeFeed = streamReader.ReadToEnd();
                    }

                    if (changeFeed.Contains(testDoc1.Id))
                    {
                        processedDocCount++;
                    }

                    if (changeFeed.Contains(testDoc2.Id))
                    {
                        processedDocCount++;
                    }

                    if (processedDocCount == 2)
                    {
                        allDocsProcessed.Set();
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithLeaseContainer(leaseContainer)
                .WithStartTime(DateTime.MinValue.ToUniversalTime())
                .Build();

            await cfp.StartAsync();
            bool isStartOk = allDocsProcessed.WaitOne(60000);
            await cfp.StopAsync();

            if (leaseDatabase != null)
            {
                using (await leaseDatabase.DeleteStreamAsync()) { }
            }
        }

        // One of query or queryDefinition is to be passed in non-null
        private static async Task ValidateQueryResultsAsync(
            Container container,
            string query = null,
            TestDoc expectedDoc = null,
            QueryDefinition queryDefinition = null,
            bool decryptOperation = true,
            int expectedPropertiesDecryptedCount = 12)
        {
            QueryRequestOptions requestOptions = expectedDoc != null
                ? new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey(expectedDoc.PK),
                }
                : null;

            FeedIterator<TestDoc> queryResponseIterator = query != null
                ? container.GetItemQueryIterator<TestDoc>(query, requestOptions: requestOptions)
                : container.GetItemQueryIterator<TestDoc>(queryDefinition, requestOptions: requestOptions);
            FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();
            Assert.AreEqual(null, readDocs.ContinuationToken);
          
            if (expectedDoc != null)
            {
                Assert.AreEqual(1, readDocs.Count);
                TestDoc readDoc = readDocs.Single();
                VerifyExpectedDocResponse(expectedDoc, readDoc);
                VerifyDiagnostics(readDocs.Diagnostics, encryptOperation: false, decryptOperation: decryptOperation, expectedPropertiesDecryptedCount: expectedPropertiesDecryptedCount);
            }
            else
            {
                Assert.AreEqual(0, readDocs.Count);
            }
        }

        private static async Task<ItemResponse<TestDoc>> MdeCreateItemAsync(
            Container container,
            string partitionKey = null)
        {
            TestDoc testDoc = TestDoc.Create(partitionKey);

            ItemResponse<TestDoc> createResponse = await container.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK));           

            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            VerifyExpectedDocResponse(testDoc, createResponse.Resource);
            return createResponse;
        }

        private static async Task VerifyItemByReadStreamAsync(Container container, TestDoc testDoc, ItemRequestOptions requestOptions = null)
        {
            ResponseMessage readResponseMessage = await container.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK), requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, readResponseMessage.StatusCode);
            Assert.IsNotNull(readResponseMessage.Content);
            TestDoc readDoc = TestCommon.FromStream<TestDoc>(readResponseMessage.Content);
            VerifyExpectedDocResponse(testDoc, readDoc);
        }

        private static async Task VerifyItemByReadAsync(Container container, TestDoc testDoc, ItemRequestOptions requestOptions = null)
        {
            ItemResponse<TestDoc> readResponse = await container.ReadItemAsync<TestDoc>(testDoc.Id, new PartitionKey(testDoc.PK), requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            VerifyExpectedDocResponse(testDoc, readResponse.Resource);           
        }
       
        private static async Task<ItemResponse<TestDoc>> MdeUpsertItemAsync(
            Container container,
            TestDoc testDoc,
            HttpStatusCode expectedStatusCode)
        {
            ItemResponse<TestDoc> upsertResponse = await container.UpsertItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK));
            Assert.AreEqual(expectedStatusCode, upsertResponse.StatusCode);
            VerifyExpectedDocResponse(testDoc, upsertResponse.Resource);
            return upsertResponse;
        }

        private static async Task<ItemResponse<TestDoc>> MdeReplaceItemAsync(
            Container encryptedContainer,
            TestDoc testDoc,
            string etag = null)
        {
            ItemResponse<TestDoc> replaceResponse = await encryptedContainer.ReplaceItemAsync(
                testDoc,
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                MdeEncryptionTests.MdeGetRequestOptions(etag));

            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            VerifyExpectedDocResponse(testDoc, replaceResponse.Resource);
            return replaceResponse;
        }

        private static async Task<ItemResponse<TestDoc>> MdePatchItemAsync(
            Container container,
            List<PatchOperation> patchOperations,
            TestDoc expectedTestDoc,
            HttpStatusCode expectedStatusCode)
        {
            ItemResponse<TestDoc> patchResponse = await container.PatchItemAsync<TestDoc>(
                expectedTestDoc.Id,
                new PartitionKey(expectedTestDoc.PK),
                patchOperations);

            Assert.AreEqual(expectedStatusCode, patchResponse.StatusCode);
            VerifyExpectedDocResponse(expectedTestDoc, patchResponse.Resource);
            return patchResponse;
        }

        private static ItemRequestOptions MdeGetRequestOptions(
            string ifMatchEtag = null)
        {
            return new ItemRequestOptions
            {
                IfMatchEtag = ifMatchEtag
            };
        }

        private static async Task<ItemResponse<TestDoc>> MdeDeleteItemAsync(
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

        private static void VerifyExpectedDocResponse(TestDoc expectedDoc, TestDoc verifyDoc)
        {
            Assert.AreEqual(expectedDoc.Id, verifyDoc.Id);
            Assert.AreEqual(expectedDoc.Sensitive_StringFormat, verifyDoc.Sensitive_StringFormat);
            if (expectedDoc.Sensitive_ArrayFormat != null)
            {
                Assert.AreEqual(expectedDoc.Sensitive_ArrayFormat[0].Sensitive_ArrayDecimalFormat, verifyDoc.Sensitive_ArrayFormat[0].Sensitive_ArrayDecimalFormat);
                Assert.AreEqual(expectedDoc.Sensitive_ArrayFormat[0].Sensitive_ArrayIntFormat, verifyDoc.Sensitive_ArrayFormat[0].Sensitive_ArrayIntFormat);               
            }
            else
            {
                Assert.AreEqual(expectedDoc.Sensitive_ArrayFormat, verifyDoc.Sensitive_ArrayFormat);
            }

            if (expectedDoc.Sensitive_IntArray != null)
            {
                for(int i = 0; i< expectedDoc.Sensitive_IntArray.Length; i++ )
                Assert.AreEqual(expectedDoc.Sensitive_IntArray[i], verifyDoc.Sensitive_IntArray[i]);
            }
            else
            {
                Assert.AreEqual(expectedDoc.Sensitive_IntArray, verifyDoc.Sensitive_IntArray);
            }

            if (expectedDoc.Sensitive_IntMultiDimArray != null)
            {
                for (int i = 0; i < expectedDoc.Sensitive_IntMultiDimArray.GetLength(0); i++)
                {
                    for (int j = 0; j < expectedDoc.Sensitive_IntMultiDimArray.GetLength(1); j++)
                    {
                        Assert.AreEqual(expectedDoc.Sensitive_IntMultiDimArray[i, j], verifyDoc.Sensitive_IntMultiDimArray[i, j]);
                    }
                }
            }

            if(expectedDoc.Sensitive_ObjectArrayType != null)
            {
                TestDoc.Sensitive_ArrayData expectedValue = expectedDoc.Sensitive_ObjectArrayType[0] is JObject jObjectValue
                    ? jObjectValue.ToObject<TestDoc.Sensitive_ArrayData>()
                    : (TestDoc.Sensitive_ArrayData)expectedDoc.Sensitive_ObjectArrayType[0];
                jObjectValue = (JObject)verifyDoc.Sensitive_ObjectArrayType[0];
                TestDoc.Sensitive_ArrayData test = jObjectValue.ToObject<TestDoc.Sensitive_ArrayData>();

                Assert.AreEqual(expectedValue.Sensitive_ArrayDecimalFormat, test.Sensitive_ArrayDecimalFormat);
                Assert.AreEqual(expectedValue.Sensitive_ArrayIntFormat, test.Sensitive_ArrayIntFormat);

                Assert.AreEqual(expectedDoc.Sensitive_ObjectArrayType[1], verifyDoc.Sensitive_ObjectArrayType[1]);
            }

            if (expectedDoc.Sensitive_ArrayMultiTypes != null)
            {
                for (int i = 0; i < expectedDoc.Sensitive_ArrayMultiTypes.GetLength(0); i++)
                {
                    for (int j = 0; j < expectedDoc.Sensitive_ArrayMultiTypes.GetLength(1); j++)
                    {
                        Assert.AreEqual(
                        expectedDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_NestedObjectFormatL0.Sensitive_DecimalFormatL0,
                        verifyDoc.Sensitive_ArrayMultiTypes[i, j].Sensitive_NestedObjectFormatL0.Sensitive_DecimalFormatL0);
                        Assert.AreEqual(
                            expectedDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_NestedObjectFormatL0.Sensitive_IntFormatL0,
                            verifyDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_NestedObjectFormatL0.Sensitive_IntFormatL0);

                        for (int l = 0; l < expectedDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_StringArrayMultiType.Length; l++)
                        {
                            Assert.AreEqual(expectedDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_StringArrayMultiType[l],
                                verifyDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_StringArrayMultiType[l]);
                        }

                        Assert.AreEqual(expectedDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_ArrayMultiTypeDecimalFormat,
                            verifyDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_ArrayMultiTypeDecimalFormat);

                        for (int k = 0; k < expectedDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_IntArrayMultiType.Length; k++)
                        {
                            Assert.AreEqual(expectedDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_IntArrayMultiType[k],
                                verifyDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_IntArrayMultiType[k]);
                        }
                    }
                }
            }

            if (expectedDoc.Sensitive_NestedObjectFormatL1 != null)
            {
                Assert.AreEqual(expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_IntFormatL1, verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_IntFormatL1);

                if (expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2 == null)
                {
                    Assert.IsNull(verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2);
                }
                else
                {
                    Assert.AreEqual(
                        expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_StringFormatL2,
                        verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_StringFormatL2);

                    Assert.AreEqual(
                        expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_IntFormatL3,
                        verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_IntFormatL3);

                    Assert.AreEqual(
                       expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_DecimalFormatL3,
                       verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_DecimalFormatL3);

                    Assert.AreEqual(
                       expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayFormatL3[0].Sensitive_ArrayIntFormat,
                       verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayFormatL3[0].Sensitive_ArrayIntFormat);

                    Assert.AreEqual(
                       expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayFormatL3[0].Sensitive_ArrayDecimalFormat,
                       verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayFormatL3[0].Sensitive_ArrayDecimalFormat);

                    Assert.AreEqual(
                       expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_ArrayDecimalFormat,
                       verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_ArrayDecimalFormat);

                    Assert.AreEqual(
                       expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_ArrayIntFormat,
                       verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_ArrayIntFormat);

                    Assert.AreEqual(
                      expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_NestedObjectFormatL0.Sensitive_IntFormatL0,
                      verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_NestedObjectFormatL0.Sensitive_IntFormatL0);

                    Assert.AreEqual(
                      expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_NestedObjectFormatL0.Sensitive_DecimalFormatL0,
                      verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_NestedObjectFormatL0.Sensitive_DecimalFormatL0);
                }
            }
            else
            {
                Assert.AreEqual(expectedDoc.Sensitive_NestedObjectFormatL1, verifyDoc.Sensitive_NestedObjectFormatL1);
            }

            Assert.AreEqual(expectedDoc.Sensitive_DateFormat, verifyDoc.Sensitive_DateFormat);
            Assert.AreEqual(expectedDoc.Sensitive_DecimalFormat, verifyDoc.Sensitive_DecimalFormat);
            Assert.AreEqual(expectedDoc.Sensitive_IntFormat, verifyDoc.Sensitive_IntFormat);
            Assert.AreEqual(expectedDoc.Sensitive_FloatFormat, verifyDoc.Sensitive_FloatFormat);
            Assert.AreEqual(expectedDoc.Sensitive_BoolFormat, verifyDoc.Sensitive_BoolFormat);
            Assert.AreEqual(expectedDoc.NonSensitive, verifyDoc.NonSensitive);
            Assert.AreEqual(expectedDoc.NonSensitiveInt, verifyDoc.NonSensitiveInt);
        }

        private static void VerifyDiagnostics(
            CosmosDiagnostics diagnostics, 
            bool encryptOperation = true, 
            bool decryptOperation = true,
            int expectedPropertiesEncryptedCount = 12,
            int expectedPropertiesDecryptedCount = 12)
        {
            Assert.IsNotNull(diagnostics);
            JObject diagnosticsObject = JObject.Parse(diagnostics.ToString());

            JObject coreDiagnostics = diagnosticsObject.Value<JObject>(Constants.DiagnosticsCoreDiagnostics);
            Assert.IsNotNull(coreDiagnostics);
            
            JObject encryptionDiagnostics = diagnosticsObject.Value<JObject>(Constants.DiagnosticsEncryptionDiagnostics);
            Assert.IsNotNull(encryptionDiagnostics);

            if (encryptOperation)
            {
                JObject encryptOperationDiagnostics = encryptionDiagnostics.Value<JObject>(Constants.DiagnosticsEncryptOperation);
                Assert.IsNotNull(encryptOperationDiagnostics);
                Assert.IsNotNull(encryptOperationDiagnostics.GetValue(Constants.DiagnosticsStartTime));
                Assert.IsNotNull(encryptOperationDiagnostics.GetValue(Constants.DiagnosticsDuration));
                int propertiesEncrypted = encryptOperationDiagnostics.Value<int>(Constants.DiagnosticsPropertiesEncryptedCount);
                Assert.AreEqual(expectedPropertiesEncryptedCount, propertiesEncrypted);
            }

            if (decryptOperation)
            {
                JObject decryptOperationDiagnostics = encryptionDiagnostics.Value<JObject>(Constants.DiagnosticsDecryptOperation);
                Assert.IsNotNull(decryptOperationDiagnostics);
                Assert.IsNotNull(decryptOperationDiagnostics.GetValue(Constants.DiagnosticsStartTime));
                Assert.IsNotNull(decryptOperationDiagnostics.GetValue(Constants.DiagnosticsDuration));
                if (expectedPropertiesDecryptedCount > 0)
                {
                    int propertiesDecrypted = decryptOperationDiagnostics.Value<int>(Constants.DiagnosticsPropertiesDecryptedCount);
                    Assert.IsTrue(propertiesDecrypted >= expectedPropertiesDecryptedCount);
                }
            }
        }

        public class TestDoc
        {          

            [JsonProperty("id")]
            public string Id { get; set; }

            public string PK { get; set; }

            public string NonSensitive { get; set; }

            public int NonSensitiveInt { get; set; }

            public string Sensitive_StringFormat { get; set; }

            public DateTime Sensitive_DateFormat { get; set; }

            public decimal Sensitive_DecimalFormat { get; set; }

            public bool Sensitive_BoolFormat { get; set; }

            public int Sensitive_IntFormat { get; set; }

            public float Sensitive_FloatFormat { get; set; }

            public int[] Sensitive_IntArray { get; set; }

            public int[,] Sensitive_IntMultiDimArray { get; set; }

            public Sensitive_ArrayData[] Sensitive_ArrayFormat { get; set; }

            public Sensitive_ArrayMultiType[,] Sensitive_ArrayMultiTypes { get; set; }

            public object[] Sensitive_ObjectArrayType { get; set; }

            public Sensitive_NestedObjectL1 Sensitive_NestedObjectFormatL1 { get; set; }

            public TestDoc()
            {
            }

            public class Sensitive_ArrayData
            {
                public int Sensitive_ArrayIntFormat { get; set; }
                public decimal Sensitive_ArrayDecimalFormat { get; set; }
            }

            public class Sensitive_ArrayMultiType
            {
                public Sensitive_NestedObjectL0 Sensitive_NestedObjectFormatL0 { get; set; }
                public string[] Sensitive_StringArrayMultiType { get; set; }
                public decimal Sensitive_ArrayMultiTypeDecimalFormat { get; set; }
                public int[] Sensitive_IntArrayMultiType { get; set; }                
            }

            public class Sensitive_ArrayDataWithObject
            {
                public int Sensitive_ArrayIntFormat { get; set; }
                public decimal Sensitive_ArrayDecimalFormat { get; set; }
                public Sensitive_NestedObjectL0 Sensitive_NestedObjectFormatL0 { get; set; }
            }

            public class Sensitive_NestedObjectL0
            {
                public int Sensitive_IntFormatL0 { get; set; }
                public decimal Sensitive_DecimalFormatL0 { get; set; }
            }

            public class Sensitive_NestedObjectL1
            {
                public Sensitive_NestedObjectL2 Sensitive_NestedObjectFormatL2 { get; set; }
                public int Sensitive_IntFormatL1 { get; set; }
                public int[] Sensitive_IntArrayL1 { get; set; }                
                public decimal Sensitive_DecimalFormatL1 { get; set; }
                public Sensitive_ArrayData[] Sensitive_ArrayFormatL1 { get; set; }                
            }

            public class Sensitive_NestedObjectL2
            {
                public string Sensitive_StringFormatL2 { get; set; }
                public decimal Sensitive_DecimalFormatL2 { get; set; }
                public Sensitive_ArrayData[] Sensitive_ArrayFormatL2 { get; set; }
                public Sensitive_NestedObjectL3 Sensitive_NestedObjectFormatL3 { get; set; }
            }

            public class Sensitive_NestedObjectL3
            {
                public int Sensitive_IntFormatL3 { get; set; }
                public Sensitive_ArrayData[] Sensitive_ArrayFormatL3 { get; set; }
                public decimal Sensitive_DecimalFormatL3 { get; set; }
                public Sensitive_ArrayDataWithObject[] Sensitive_ArrayWithObjectFormat { get; set; }
            }

            public TestDoc(TestDoc other)
            {
                this.Id = other.Id;
                this.PK = other.PK;
                this.NonSensitive = other.NonSensitive;
                this.NonSensitiveInt = other.NonSensitiveInt;
                this.Sensitive_StringFormat = other.Sensitive_StringFormat;
                this.Sensitive_DateFormat = other.Sensitive_DateFormat;
                this.Sensitive_DecimalFormat = other.Sensitive_DecimalFormat;
                this.Sensitive_IntFormat = other.Sensitive_IntFormat;
                this.Sensitive_BoolFormat = other.Sensitive_BoolFormat;
                this.Sensitive_FloatFormat = other.Sensitive_FloatFormat;
                this.Sensitive_ArrayFormat = other.Sensitive_ArrayFormat;
                this.Sensitive_IntArray = other.Sensitive_IntArray;
                this.Sensitive_ObjectArrayType = other.Sensitive_ObjectArrayType;
                this.Sensitive_NestedObjectFormatL1 = other.Sensitive_NestedObjectFormatL1;
                this.Sensitive_ArrayMultiTypes = other.Sensitive_ArrayMultiTypes;
            }

            public override bool Equals(object obj)
            {
                return obj is TestDoc doc
                       && this.Id == doc.Id
                       && this.PK == doc.PK
                       && this.NonSensitive == doc.NonSensitive
                       && this.NonSensitiveInt == doc.NonSensitiveInt
                       && this.Sensitive_StringFormat == doc.Sensitive_StringFormat
                       && this.Sensitive_DateFormat == doc.Sensitive_DateFormat
                       && this.Sensitive_DecimalFormat == doc.Sensitive_DecimalFormat
                       && this.Sensitive_IntFormat == doc.Sensitive_IntFormat
                       && this.Sensitive_ArrayFormat == doc.Sensitive_ArrayFormat
                       && this.Sensitive_BoolFormat == doc.Sensitive_BoolFormat
                       && this.Sensitive_FloatFormat == doc.Sensitive_FloatFormat
                       && this.Sensitive_IntArray == doc.Sensitive_IntArray
                       && this.Sensitive_ObjectArrayType == doc.Sensitive_ObjectArrayType
                       && this.Sensitive_NestedObjectFormatL1 != doc.Sensitive_NestedObjectFormatL1
                       && this.Sensitive_ArrayMultiTypes != doc.Sensitive_ArrayMultiTypes;
            }

            public bool EqualsExceptEncryptedProperty(object obj)
            {
                return obj is TestDoc doc
                       && this.Id == doc.Id
                       && this.PK == doc.PK
                       && this.NonSensitive == doc.NonSensitive
                       && this.NonSensitiveInt == doc.NonSensitiveInt
                       && this.Sensitive_StringFormat != doc.Sensitive_StringFormat;
            }
            public override int GetHashCode()
            {
                int hashCode = 1652434776;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Id);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.PK);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.NonSensitive);
                hashCode = (hashCode * -1521134295) + EqualityComparer<int>.Default.GetHashCode(this.NonSensitiveInt);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Sensitive_StringFormat);
                hashCode = (hashCode * -1521134295) + EqualityComparer<DateTime>.Default.GetHashCode(this.Sensitive_DateFormat);
                hashCode = (hashCode * -1521134295) + EqualityComparer<Decimal>.Default.GetHashCode(this.Sensitive_DecimalFormat);
                hashCode = (hashCode * -1521134295) + EqualityComparer<int>.Default.GetHashCode(this.Sensitive_IntFormat);
                hashCode = (hashCode * -1521134295) + EqualityComparer<Object>.Default.GetHashCode(this.Sensitive_ObjectArrayType);
                hashCode = (hashCode * -1521134295) + EqualityComparer<Array>.Default.GetHashCode(this.Sensitive_ArrayFormat);
                hashCode = (hashCode * -1521134295) + EqualityComparer<bool>.Default.GetHashCode(this.Sensitive_BoolFormat);
                hashCode = (hashCode * -1521134295) + EqualityComparer<float>.Default.GetHashCode(this.Sensitive_FloatFormat);
                hashCode = (hashCode * -1521134295) + EqualityComparer<Object>.Default.GetHashCode(this.Sensitive_NestedObjectFormatL1);
                hashCode = (hashCode * -1521134295) + EqualityComparer<Object>.Default.GetHashCode(this.Sensitive_ArrayMultiTypes);
                return hashCode;
            }

            public static TestDoc Create(string partitionKey = null)
            {
                return new TestDoc()
                {
                    Id = Guid.NewGuid().ToString(),
                    PK = partitionKey ?? Guid.NewGuid().ToString(),
                    NonSensitive = Guid.NewGuid().ToString(),
                    NonSensitiveInt = 10,
                    Sensitive_StringFormat = Guid.NewGuid().ToString(),
                    Sensitive_DateFormat = new DateTime(1987, 12, 25),
                    Sensitive_DecimalFormat = 472.3108m,
                    Sensitive_IntArray = new int[2] { 999, 1000 },
                    Sensitive_IntMultiDimArray = new[,] { { 1, 2 }, { 2, 3 }, { 4, 5 } },
                    Sensitive_ObjectArrayType = new object[2]
                    {
                        (Sensitive_ArrayData)new Sensitive_ArrayData
                        {
                            Sensitive_ArrayIntFormat = 18273,
                            Sensitive_ArrayDecimalFormat = 1234.11m
                        },
                        (Int64)9823
                    },                    
                    Sensitive_IntFormat = 1965,
                    Sensitive_BoolFormat = true,
                    Sensitive_FloatFormat = 8923.124f,
                    Sensitive_ArrayFormat = new Sensitive_ArrayData[]
                    {
                        new Sensitive_ArrayData
                        {
                            Sensitive_ArrayIntFormat = 1111,
                            Sensitive_ArrayDecimalFormat = 1111.11m
                        }
                    },
                    Sensitive_ArrayMultiTypes = new Sensitive_ArrayMultiType[,]
                    {
                        {
                            new Sensitive_ArrayMultiType()
                            {
                                Sensitive_NestedObjectFormatL0 = new Sensitive_NestedObjectL0()
                                {
                                    Sensitive_IntFormatL0 = 888,
                                    Sensitive_DecimalFormatL0 = 888.1m,
                                },
                                Sensitive_StringArrayMultiType = new string[3] { "sensitivedata1a", "verysensitivedata1a", null},
                                Sensitive_ArrayMultiTypeDecimalFormat = 10.2m,
                                Sensitive_IntArrayMultiType = new int[2] { 999, 1000 }
                            },
                            new Sensitive_ArrayMultiType()
                            {
                                Sensitive_NestedObjectFormatL0 = new Sensitive_NestedObjectL0()
                                {
                                    Sensitive_IntFormatL0 = 888,
                                    Sensitive_DecimalFormatL0 = 888.1m,
                                },
                                Sensitive_StringArrayMultiType = new string[2] { "sensitivedata1b", "verysensitivedata1b"},
                                Sensitive_ArrayMultiTypeDecimalFormat = 12.2m,
                                Sensitive_IntArrayMultiType = new int[2] { 888, 1010 }
                            }
                        },
                        {
                            new Sensitive_ArrayMultiType()
                            {
                                Sensitive_NestedObjectFormatL0 = new Sensitive_NestedObjectL0()
                                {
                                    Sensitive_IntFormatL0 = 111,
                                    Sensitive_DecimalFormatL0 = 222.3m,
                                },
                                Sensitive_StringArrayMultiType = new string[2] { "sensitivedata2a", "verysensitivedata2a"},
                                Sensitive_ArrayMultiTypeDecimalFormat = 9876.2m,
                                Sensitive_IntArrayMultiType = new int[2] { 1, 2 }
                            },
                            new Sensitive_ArrayMultiType()
                            {
                                Sensitive_NestedObjectFormatL0 = new Sensitive_NestedObjectL0()
                                {
                                    Sensitive_IntFormatL0 = 878,
                                    Sensitive_DecimalFormatL0 = 188.1m,
                                },
                                Sensitive_StringArrayMultiType = new string[2] { "sensitivedata2b", "verysensitivedata2b"},
                                Sensitive_ArrayMultiTypeDecimalFormat = 14.2m,
                                Sensitive_IntArrayMultiType = new int[2] { 929, 1050 }
                            }
                        }
                    },
                    Sensitive_NestedObjectFormatL1 = new Sensitive_NestedObjectL1()
                    {
                        Sensitive_IntArrayL1 = new int[2] { 999, 100 },
                        Sensitive_IntFormatL1 = 1999,
                        Sensitive_DecimalFormatL1 = 1999.1m,
                        Sensitive_ArrayFormatL1 = new Sensitive_ArrayData[]
                        {
                            new Sensitive_ArrayData
                            {
                                Sensitive_ArrayIntFormat = 1,
                                Sensitive_ArrayDecimalFormat = 2.1m
                            },
                            new Sensitive_ArrayData
                            {
                                Sensitive_ArrayIntFormat = 2,
                                Sensitive_ArrayDecimalFormat = 3.1m
                            }
                        },
                        Sensitive_NestedObjectFormatL2 = new Sensitive_NestedObjectL2()
                        {
                            Sensitive_StringFormatL2 = "sensitiveData",
                            Sensitive_DecimalFormatL2 = 2000.1m,
                            Sensitive_ArrayFormatL2 = new Sensitive_ArrayData[]
                            {
                                new Sensitive_ArrayData
                                {
                                    Sensitive_ArrayIntFormat = 2,
                                    Sensitive_ArrayDecimalFormat = 3.1m
                                }
                            },
                            Sensitive_NestedObjectFormatL3 = new Sensitive_NestedObjectL3()
                            {
                                Sensitive_IntFormatL3 = 3000,
                                Sensitive_DecimalFormatL3 = 3000.1m,
                                Sensitive_ArrayFormatL3 = new Sensitive_ArrayData[]
                                {
                                    new Sensitive_ArrayData
                                    {
                                        Sensitive_ArrayIntFormat = 3,
                                        Sensitive_ArrayDecimalFormat = 4.1m,
                                    }
                                },
                                Sensitive_ArrayWithObjectFormat = new Sensitive_ArrayDataWithObject[]
                                {
                                    new Sensitive_ArrayDataWithObject
                                    {
                                        Sensitive_ArrayIntFormat = 4,
                                        Sensitive_ArrayDecimalFormat = 5.1m,
                                        Sensitive_NestedObjectFormatL0 = new Sensitive_NestedObjectL0()
                                        {
                                            Sensitive_IntFormatL0 = 888,
                                            Sensitive_DecimalFormatL0 = 888.1m,
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
            }

            public Stream ToStream()
            {
                return TestCommon.ToStream(this);
            }
        }

        internal class TestEncryptionKeyStoreProvider : EncryptionKeyStoreProvider
        {
            readonly Dictionary<string, int> keyinfo = new Dictionary<string, int>
            {
                {"tempmetadata1", 1},
                {"tempmetadata2", 2},
            };

            public bool RevokeAccessSet { get; set; }
            public Dictionary<string, int> WrapKeyCallsCount { get; set; }
            public Dictionary<string, int> UnWrapKeyCallsCount { get; set; }
            public TestEncryptionKeyStoreProvider()
            {
                this.WrapKeyCallsCount = new Dictionary<string, int>();
                this.UnWrapKeyCallsCount = new Dictionary<string, int>();
                this.RevokeAccessSet = false;
            }

            public override string ProviderName => "TESTKEYSTORE_VAULT";

            public override byte[] UnwrapKey(string masterKeyPath, KeyEncryptionKeyAlgorithm encryptionAlgorithm, byte[] encryptedKey)
            {
                if (masterKeyPath.Equals("revokedKek-metadata") && this.RevokeAccessSet)
                {
                    throw new RequestFailedException((int)HttpStatusCode.Forbidden, "Forbidden");
                }

                return this.GetOrCreateDataEncryptionKey(encryptedKey.ToHexString(), DecryptEncryptionKey);

                byte[] DecryptEncryptionKey()
                {
                    if (!this.UnWrapKeyCallsCount.ContainsKey(masterKeyPath))
                    {
                        this.UnWrapKeyCallsCount[masterKeyPath] = 1;
                    }
                    else
                    {
                        this.UnWrapKeyCallsCount[masterKeyPath]++;
                    }

                    this.keyinfo.TryGetValue(masterKeyPath, out int moveBy);
                    byte[] plainkey = encryptedKey.Select(b => (byte)(b - moveBy)).ToArray();
                    return plainkey;
                }
            }

            public override byte[] WrapKey(string masterKeyPath, KeyEncryptionKeyAlgorithm encryptionAlgorithm, byte[] key)
            {
                if (!this.WrapKeyCallsCount.ContainsKey(masterKeyPath))
                {
                    this.WrapKeyCallsCount[masterKeyPath] = 1;
                }
                else
                {
                    this.WrapKeyCallsCount[masterKeyPath]++;
                }

                this.keyinfo.TryGetValue(masterKeyPath, out int moveBy);
                byte[] encryptedkey = key.Select(b => (byte)(b + moveBy)).ToArray();
                return encryptedkey;
            }

            public override byte[] Sign(string masterKeyPath, bool allowEnclaveComputations)
            {
                return null;
            }

            public override bool Verify(string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
            {
                return true;
            }
        }       

        internal class CustomSerializer : CosmosSerializer
        {
            private readonly JsonSerializer serializer = new JsonSerializer();
            public int FromStreamCalled = 0;

            public override T FromStream<T>(Stream stream)
            {
                this.FromStreamCalled++;
                using (StreamReader sr = new StreamReader(stream))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    return this.serializer.Deserialize<T>(reader);
                }
            }

            public override Stream ToStream<T>(T input)
            {
                MemoryStream streamPayload = new MemoryStream();
                using (StreamWriter streamWriter = new StreamWriter(streamPayload, encoding: UTF8Encoding.UTF8, bufferSize: 1024, leaveOpen: true))
                {
                    using (JsonWriter writer = new JsonTextWriter(streamWriter))
                    {
                        writer.Formatting = Newtonsoft.Json.Formatting.None;
                        this.serializer.Serialize(writer, input);
                        writer.Flush();
                        streamWriter.Flush();
                    }
                }

                streamPayload.Position = 0;
                return streamPayload;
            }
        }
    }
}
