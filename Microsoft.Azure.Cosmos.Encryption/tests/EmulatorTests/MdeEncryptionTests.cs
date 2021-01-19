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
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption;
    using Microsoft.Data.Encryption.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    using EncryptionKeyWrapMetadata = Cosmos.EncryptionKeyWrapMetadata;

    [TestClass]
    public class MdeEncryptionTests
    {
        private static readonly EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata("key1", "tempmetadata1");
        private static readonly EncryptionKeyWrapMetadata metadata2 = new EncryptionKeyWrapMetadata("key2", "tempmetadata2");

        private static CosmosClient client;
        private static CosmosClient encryptionCosmosClient;
        private static Database database;
        private static Container encryptionContainer;
        private static TestEncryptionKeyStoreProvider testEncryptionKeyStoreProvider;

        [ClassInitialize]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The ClassInitialize method takes a single parameter of type TestContext.")]
        public static async Task ClassInitialize(TestContext context)
        {
            MdeEncryptionTests.client = TestCommon.CreateCosmosClient();
            testEncryptionKeyStoreProvider = new TestEncryptionKeyStoreProvider();
            MdeEncryptionTests.encryptionCosmosClient = MdeEncryptionTests.client.WithEncryption(testEncryptionKeyStoreProvider);
            MdeEncryptionTests.database = await MdeEncryptionTests.encryptionCosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());

            await MdeEncryptionTests.CreateClientEncryptionKeyAsync(
               "key1",
               metadata1);

            await MdeEncryptionTests.CreateClientEncryptionKeyAsync(
                "key2",
                metadata2);

            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>()
            {
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_StringFormat",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = CosmosEncryptionType.Deterministic,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_ArrayFormat",
                    ClientEncryptionKeyId = "key2",
                    EncryptionType = CosmosEncryptionType.Deterministic,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_NestedObjectFormatL1",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = CosmosEncryptionType.Deterministic,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_IntArray",
                    ClientEncryptionKeyId = "key2",
                    EncryptionType = CosmosEncryptionType.Deterministic,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_IntFormat",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = CosmosEncryptionType.Deterministic,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_DecimalFormat",
                    ClientEncryptionKeyId = "key2",
                    EncryptionType = CosmosEncryptionType.Deterministic,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_DateFormat",
                    ClientEncryptionKeyId = "key1",
                    EncryptionType = CosmosEncryptionType.Deterministic,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
                },
            };


            ClientEncryptionPolicy clientEncryptionPolicy = new ClientEncryptionPolicy(paths);
           

            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK") { ClientEncryptionPolicy = clientEncryptionPolicy };

            encryptionContainer = await database.CreateContainerAsync(containerProperties, 400);
            await encryptionContainer.InitializeEncryptionAsync();           
        }

        private static async Task<ClientEncryptionKeyResponse> CreateClientEncryptionKeyAsync(string cekId, Cosmos.EncryptionKeyWrapMetadata encryptionKeyWrapMetadata)
        {
            ClientEncryptionKeyResponse clientEncrytionKeyResponse = await database.CreateClientEncryptionKeyAsync(
                   cekId,
                   CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
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
            EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata(cekId, "testmetadata1");
            ClientEncryptionKeyProperties clientEncryptionKeyProperties = await MdeEncryptionTests.CreateClientEncryptionKeyAsync(
                cekId,
                metadata1);

            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(name: cekId, value: metadata1.Value),
                clientEncryptionKeyProperties.EncryptionKeyWrapMetadata);

            // creating another key with same id should fail
            metadata1 = new EncryptionKeyWrapMetadata(cekId, "testmetadata2");

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
            EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata(cekId, "testmetadata1");
            ClientEncryptionKeyProperties clientEncryptionKeyProperties = await MdeEncryptionTests.CreateClientEncryptionKeyAsync(
                cekId,
                metadata1);

            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(name: cekId, value: metadata1.Value),
                clientEncryptionKeyProperties.EncryptionKeyWrapMetadata);

            EncryptionKeyWrapMetadata updatedMetaData = new EncryptionKeyWrapMetadata(cekId, metadata1 + "updatedmetadata");
            clientEncryptionKeyProperties = await MdeEncryptionTests.RewarpClientEncryptionKeyAsync(
                cekId,
                updatedMetaData);

            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(name: cekId, value: updatedMetaData.Value),
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
            TestDoc testDoc = TestDoc.Create();

            testDoc.Sensitive_ArrayFormat = null;
            testDoc.Sensitive_StringFormat = null;
            ItemResponse<TestDoc> createResponse = await MdeEncryptionTests.encryptionContainer.CreateItemAsync(
                    testDoc,
                    new PartitionKey(testDoc.PK));

            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            VerifyExpectedDocResponse(testDoc, createResponse.Resource);
        }


        [TestMethod]
        public async Task EncryptionResourceTokenAuthRestricted()
        {
            TestDoc testDoc = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            User restrictedUser = MdeEncryptionTests.database.GetUser(Guid.NewGuid().ToString());
            await MdeEncryptionTests.database.CreateUserAsync(restrictedUser.Id);

            PermissionProperties restrictedUserPermission = await restrictedUser.CreatePermissionAsync(
                MdeEncryptionTests.encryptionContainer.GetPermissionPropertiesForEncryptionContainer(Guid.NewGuid().ToString(), PermissionMode.All));


            CosmosClient clientForRestrictedUser = TestCommon.CreateCosmosClient(
                restrictedUserPermission.Token);


            CosmosClient encryptedclientForRestrictedUser = clientForRestrictedUser.WithEncryption(new TestEncryptionKeyStoreProvider());

            Database databaseForRestrictedUser = encryptedclientForRestrictedUser.GetDatabase(MdeEncryptionTests.database.Id);                  

            try
            {
                string cekId = "testingcekID";
                EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata(cekId, "testmetadata1");

                ClientEncryptionKeyResponse clientEncrytionKeyResponse = await databaseForRestrictedUser.CreateClientEncryptionKeyAsync(
                       cekId,
                       CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
                       metadata1);
                Assert.Fail("CreateClientEncryptionKeyAsync should have failed due to restrictions");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
            }

            try
            {
                string cekId = "testingcekID";
                EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata(cekId, "testmetadata1" + "updated");

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
        public async Task EncryptionFailsWithUnknownClientEncryptionKey()
        {
            ClientEncryptionIncludedPath unknownKeyConfigured = new ClientEncryptionIncludedPath()
            {
                Path = "/",
                ClientEncryptionKeyId = "unknownKey",
                EncryptionType = Encryption.CosmosEncryptionType.Deterministic,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
            };

            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>  { unknownKeyConfigured };
            ClientEncryptionPolicy clientEncryptionPolicyId = new ClientEncryptionPolicy(paths);            

            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK") { ClientEncryptionPolicy = clientEncryptionPolicyId };

            Container encryptionContainer = await database.CreateContainerAsync(containerProperties, 400);           

            try
            {
                await encryptionContainer.InitializeEncryptionAsync();
                await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainer);
                Assert.Fail("Expected item creation should fail since client encryption policy is configured with unknown key.");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is InvalidOperationException);
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
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
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

            expectedDoc.Sensitive_NestedObjectFormatL1 = null;
            expectedDoc.Sensitive_ArrayFormat = null;
            expectedDoc.Sensitive_DecimalFormat = 0;
            expectedDoc.Sensitive_IntFormat = 0;
            expectedDoc.Sensitive_FloatFormat = 0;
            expectedDoc.Sensitive_BoolFormat = false;
            expectedDoc.Sensitive_DateFormat = new DateTime();
            expectedDoc.Sensitive_StringFormat = null;
            expectedDoc.Sensitive_IntArray = null;

            await MdeEncryptionTests.ValidateQueryResultsAsync(
                MdeEncryptionTests.encryptionContainer,
                "SELECT c.id, c.PK, c.NonSensitive FROM c",
                expectedDoc);
        }

        [TestMethod]
        public async Task QueryOnEncryptedProperties()
        {
            TestDoc testDoc1 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            QueryDefinition withEncryptedParameter = MdeEncryptionTests.encryptionContainer.CreateQueryDefinition(
                    "SELECT * FROM c where c.Sensitive_StringFormat = @Sensitive_StringFormat AND c.Sensitive_IntFormat = @Sensitive_IntFormat");

            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_StringFormat",
                    testDoc1.Sensitive_StringFormat,
                    "/Sensitive_StringFormat");
            
            await withEncryptedParameter.AddParameterAsync(
                    "@Sensitive_IntFormat",
                    testDoc1.Sensitive_IntFormat,
                    "/Sensitive_IntFormat");

            TestDoc expectedDoc = new TestDoc(testDoc1);
            await MdeEncryptionTests.ValidateQueryResultsAsync(
                MdeEncryptionTests.encryptionContainer,
                queryDefinition:withEncryptedParameter,
                expectedDoc: expectedDoc);


            TestDoc testDoc2 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            // with encrypted and non encrypted properties
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
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);

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

            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc1ToCreate);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc2ToCreate);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc3ToCreate);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc4ToCreate);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc1ToReplace);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc2ToReplace);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc1ToUpsert);
            await MdeEncryptionTests.VerifyItemByReadAsync(MdeEncryptionTests.encryptionContainer, doc2ToUpsert);

            ResponseMessage readResponseMessage = await MdeEncryptionTests.encryptionContainer.ReadItemStreamAsync(docToDelete.Id, new PartitionKey(docToDelete.PK));
            Assert.AreEqual(HttpStatusCode.NotFound, readResponseMessage.StatusCode);
            
        }

        [TestMethod]
        public async Task EncryptionChangeFeedDecryptionSuccessful()
        {


            TestDoc testDoc1 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            TestDoc testDoc2 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            // change feed iterator
            await this.ValidateChangeFeedIteratorResponse(MdeEncryptionTests.encryptionContainer, testDoc1, testDoc2);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleDocs()
        {
            TestDoc testDoc1 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            TestDoc testDoc2 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            // test GetItemLinqQueryable
            await MdeEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(MdeEncryptionTests.encryptionContainer, testDoc1, testDoc2, null);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";
            await MdeEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(MdeEncryptionTests.encryptionContainer, testDoc1, testDoc2, query);

            // ORDER BY query
            query += " ORDER BY c._ts";
            await MdeEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(MdeEncryptionTests.encryptionContainer, testDoc1, testDoc2, query);
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
        public async Task EncryptionHandleDecryptionFailure()
        {
            TestDoc testDoc1 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            TestDoc testDoc2 = await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";

            // success
            await MdeEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(MdeEncryptionTests.encryptionContainer, testDoc1, testDoc2, query);           
        }

        [TestMethod]
        public async Task EncryptionRestrictedProperties()
        {
            ClientEncryptionIncludedPath restrictedPathId = new ClientEncryptionIncludedPath()
            {
                Path = "/id",
                ClientEncryptionKeyId = "key1",
                EncryptionType = CosmosEncryptionType.Deterministic,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
            };

            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath> { restrictedPathId };
            ClientEncryptionPolicy clientEncryptionPolicyId = new ClientEncryptionPolicy(paths);           

            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK") { ClientEncryptionPolicy = clientEncryptionPolicyId };

            Container encryptionContainer = await database.CreateContainerAsync(containerProperties, 400);
            await encryptionContainer.InitializeEncryptionAsync();

            try
            {
                await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainer);
                Assert.Fail("Expected item creation with id specified to be encrypted to fail.");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("Microsoft.Azure.Cosmos.ClientEncryptionIncludedPath includes an invalid path: '/id'. ", ex.Message);
            }

            ClientEncryptionIncludedPath restrictedPathPk = new ClientEncryptionIncludedPath()
            {
                Path = "/PK",
                ClientEncryptionKeyId = "key2",
                EncryptionType = CosmosEncryptionType.Deterministic,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
            };


            Collection<ClientEncryptionIncludedPath> pathsRestrictedPathPk = new Collection<ClientEncryptionIncludedPath> { restrictedPathPk };
            ClientEncryptionPolicy clientEncryptionPolicyPk = new ClientEncryptionPolicy(pathsRestrictedPathPk);

            containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK") { ClientEncryptionPolicy = clientEncryptionPolicyPk };

            encryptionContainer = await database.CreateContainerAsync(containerProperties, 400);
            await encryptionContainer.InitializeEncryptionAsync();

            try
            {
                await MdeEncryptionTests.MdeCreateItemAsync(encryptionContainer);
                Assert.Fail("Expected item creation with PK specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }

            ClientEncryptionIncludedPath pathdup1 = new ClientEncryptionIncludedPath()
            {
                Path = "/Sensitive_StringFormat",
                ClientEncryptionKeyId = "key2",
                EncryptionType = CosmosEncryptionType.Deterministic,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
            };

            ClientEncryptionIncludedPath pathdup2 = new ClientEncryptionIncludedPath()
            {
                Path = "/Sensitive_StringFormat",
                ClientEncryptionKeyId = "key1",
                EncryptionType = CosmosEncryptionType.Deterministic,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AeadAes256CbcHmacSha256,
            };


            Collection<ClientEncryptionIncludedPath> pathsWithDups = new Collection<ClientEncryptionIncludedPath> { pathdup1 , pathdup2 };

            try
            {
                ClientEncryptionPolicy clientEncryptionPolicyWithDupPaths = new ClientEncryptionPolicy(pathsWithDups);
                containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK") { ClientEncryptionPolicy = clientEncryptionPolicyWithDupPaths };
                encryptionContainer = await database.CreateContainerAsync(containerProperties, 400);
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithNoClientEncryptionPolicy()
        {
            // a database can have both Containers with Policies Configured and with no Encryption Policy
            await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK");

            Container encryptionContainer = await database.CreateContainerAsync(containerProperties, 400);
            await encryptionContainer.InitializeEncryptionAsync();

            TestDoc testDoc = TestDoc.Create();

            ItemResponse<TestDoc> createResponse = await encryptionContainer.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            VerifyExpectedDocResponse(testDoc, createResponse.Resource);

            QueryDefinition withEncryptedParameter = encryptionContainer.CreateQueryDefinition(
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
                encryptionContainer,
                queryDefinition: withEncryptedParameter,
                expectedDoc: expectedDoc);

            await encryptionContainer.DeleteContainerAsync();
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

            await database.DeleteStreamAsync();
        }

        [TestMethod]
        public async Task EncryptionStreamIteratorValidation()
        {
            await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);
            await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            // test GetItemLinqQueryable with ToEncryptionStreamIterator extension
            await MdeEncryptionTests.ValidateQueryResponseAsync(MdeEncryptionTests.encryptionContainer);
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
        }

        [TestMethod]
        public async Task ValidateCachingofProtectedDataEncryptionKey()
        {
            for (int i = 0; i < 6; i++)
                await MdeEncryptionTests.MdeCreateItemAsync(MdeEncryptionTests.encryptionContainer);

            testEncryptionKeyStoreProvider.UnWrapKeyCallsCount.TryGetValue(metadata1.Value, out int unwrapcount);
            Assert.AreEqual(3, unwrapcount);
        }
        private static async Task ValidateQueryResultsMultipleDocumentsAsync(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2,
            string query,
            bool compareEncryptedProperty = true)
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
            TestDoc testDoc2)
        {
            FeedIterator<TestDoc> changeIterator = container.GetChangeFeedIterator<TestDoc>(
                ChangeFeedStartFrom.Beginning());

            List<TestDoc> changeFeedReturnedDocs = new List<TestDoc>();
            while (changeIterator.HasMoreResults)
            {
                try
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
                catch (CosmosException ex)
                {
                    Assert.IsTrue(ex.Message.Contains("Response status code does not indicate success: NotModified (304)"));
                    break;
                }
            }

            Assert.AreEqual(changeFeedReturnedDocs.Count, 2);

            VerifyExpectedDocResponse(testDoc1, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 2]);
            VerifyExpectedDocResponse(testDoc2, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 1]);

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

            if (expectedDoc.Sensitive_NestedObjectFormatL1 != null)
            {
                Assert.AreEqual(expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_IntFormatL1, verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_IntFormatL1);
                Assert.AreEqual(
                    expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_IntFormatL2,
                    verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_IntFormatL2);

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
        }

        public class TestDoc
        {          

            [JsonProperty("id")]
            public string Id { get; set; }

            public string PK { get; set; }

            public string NonSensitive { get; set; }

            public string Sensitive_StringFormat { get; set; }

            public DateTime Sensitive_DateFormat { get; set; }

            public decimal Sensitive_DecimalFormat { get; set; }

            public bool Sensitive_BoolFormat { get; set; }

            public int Sensitive_IntFormat { get; set; }

            public float Sensitive_FloatFormat { get; set; }

            public int[] Sensitive_IntArray { get; set; }

            public Sensitive_ArrayData[] Sensitive_ArrayFormat { get; set; }

            public Sensitive_NestedObjectL1 Sensitive_NestedObjectFormatL1 { get; set; }

            public TestDoc()
            {
            }

            public class Sensitive_ArrayData
            {
                public int Sensitive_ArrayIntFormat { get; set; }
                public decimal Sensitive_ArrayDecimalFormat { get; set; }
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
                public int[] Sensitive_IntArrayL1 { get; set; }
                public int Sensitive_IntFormatL1 { get; set; }
                public decimal Sensitive_DecimalFormatL1 { get; set; }
                public Sensitive_ArrayData[] Sensitive_ArrayFormatL1 { get; set; }
                public Sensitive_NestedObjectL2 Sensitive_NestedObjectFormatL2 { get; set; }
            }

            public class Sensitive_NestedObjectL2
            {
                public int Sensitive_IntFormatL2 { get; set; }
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
                this.Sensitive_StringFormat = other.Sensitive_StringFormat;
                this.Sensitive_DateFormat = other.Sensitive_DateFormat;
                this.Sensitive_DecimalFormat = other.Sensitive_DecimalFormat;
                this.Sensitive_IntFormat = other.Sensitive_IntFormat;
                this.Sensitive_BoolFormat = other.Sensitive_BoolFormat;
                this.Sensitive_FloatFormat = other.Sensitive_FloatFormat;
                this.Sensitive_ArrayFormat = other.Sensitive_ArrayFormat;
                this.Sensitive_IntArray = other.Sensitive_IntArray;
                this.Sensitive_NestedObjectFormatL1 = other.Sensitive_NestedObjectFormatL1;
            }

            public override bool Equals(object obj)
            {
                return obj is TestDoc doc
                       && this.Id == doc.Id
                       && this.PK == doc.PK
                       && this.NonSensitive == doc.NonSensitive
                       && this.Sensitive_StringFormat == doc.Sensitive_StringFormat
                       && this.Sensitive_DateFormat == doc.Sensitive_DateFormat
                       && this.Sensitive_DecimalFormat == doc.Sensitive_DecimalFormat
                       && this.Sensitive_IntFormat == doc.Sensitive_IntFormat
                       && this.Sensitive_ArrayFormat == doc.Sensitive_ArrayFormat
                       && this.Sensitive_BoolFormat == doc.Sensitive_BoolFormat
                       && this.Sensitive_FloatFormat == doc.Sensitive_FloatFormat
                       && this.Sensitive_IntArray == doc.Sensitive_IntArray
                       && this.Sensitive_NestedObjectFormatL1 != doc.Sensitive_NestedObjectFormatL1;
            }

            public bool EqualsExceptEncryptedProperty(object obj)
            {
                return obj is TestDoc doc
                       && this.Id == doc.Id
                       && this.PK == doc.PK
                       && this.NonSensitive == doc.NonSensitive
                       && this.Sensitive_StringFormat != doc.Sensitive_StringFormat;
            }
            public override int GetHashCode()
            {
                int hashCode = 1652434776;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Id);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.PK);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.NonSensitive);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Sensitive_StringFormat);
                hashCode = (hashCode * -1521134295) + EqualityComparer<DateTime>.Default.GetHashCode(this.Sensitive_DateFormat);
                hashCode = (hashCode * -1521134295) + EqualityComparer<Decimal>.Default.GetHashCode(this.Sensitive_DecimalFormat);
                hashCode = (hashCode * -1521134295) + EqualityComparer<int>.Default.GetHashCode(this.Sensitive_IntFormat);
                hashCode = (hashCode * -1521134295) + EqualityComparer<Array>.Default.GetHashCode(this.Sensitive_ArrayFormat);
                hashCode = (hashCode * -1521134295) + EqualityComparer<bool>.Default.GetHashCode(this.Sensitive_BoolFormat);
                hashCode = (hashCode * -1521134295) + EqualityComparer<float>.Default.GetHashCode(this.Sensitive_FloatFormat);
                hashCode = (hashCode * -1521134295) + EqualityComparer<Object>.Default.GetHashCode(this.Sensitive_NestedObjectFormatL1);
                return hashCode;
            }

            public static TestDoc Create(string partitionKey = null)
            {
                return new TestDoc()
                {
                    Id = Guid.NewGuid().ToString(),
                    PK = partitionKey ?? Guid.NewGuid().ToString(),
                    NonSensitive = Guid.NewGuid().ToString(),
                    Sensitive_StringFormat = Guid.NewGuid().ToString(),
                    Sensitive_DateFormat = new DateTime(1987, 12, 25),
                    Sensitive_DecimalFormat = 472.3108m,
                    Sensitive_IntArray = new int[1] { 999 },
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
                            Sensitive_IntFormatL2 = 2000,
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

            public Dictionary<string, int> WrapKeyCallsCount { get; set; }
            public Dictionary<string, int> UnWrapKeyCallsCount { get; set; }
            public TestEncryptionKeyStoreProvider()
            {
                this.WrapKeyCallsCount = new Dictionary<string, int>();
                this.UnWrapKeyCallsCount = new Dictionary<string, int>();
            }

            public override string ProviderName => "TESTKEYSTORE_VAULT";

            public override byte[] UnwrapKey(string masterKeyPath, KeyEncryptionKeyAlgorithm encryptionAlgorithm, byte[] encryptedKey)
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
