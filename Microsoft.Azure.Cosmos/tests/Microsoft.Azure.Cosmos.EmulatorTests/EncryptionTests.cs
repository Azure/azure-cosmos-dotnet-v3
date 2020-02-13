//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    [Ignore]
    public class EncryptionTests
    {
        private static EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata("metadata1");
        private static EncryptionKeyWrapMetadata metadata2 = new EncryptionKeyWrapMetadata("metadata2");
        private const string metadataUpdateSuffix = "updated";
        private static TimeSpan cacheTTL = TimeSpan.FromDays(1);

        private const string dekId = "mydek";

        private static CosmosClient client;

        private DatabaseCore database;
        private ContainerCore container;
        private ContainerInlineCore containerInlineCore;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            EncryptionTests.client = EncryptionTests.GetClient();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if(EncryptionTests.client != null)
            {
                EncryptionTests.client.Dispose();
            }
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.database = (DatabaseInlineCore)await EncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            this.containerInlineCore = (ContainerInlineCore)await this.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/PK", 400);
            this.container = this.containerInlineCore;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.database != null)
            {
                await this.database.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task EncryptionCreateDek()
        {
            DataEncryptionKeyProperties dekProperties = await this.CreateDekAsync(EncryptionTests.dekId);
            Assert.IsNotNull(dekProperties);
            Assert.IsNotNull(dekProperties.CreatedTime);
            Assert.IsNotNull(dekProperties.LastModified);
            Assert.IsNotNull(dekProperties.SelfLink);
            Assert.IsNotNull(dekProperties.ResourceId);

            // Assert.AreEqual(dekProperties.LastModified, dekProperties.CreatedTime);
            Assert.AreEqual(new EncryptionKeyWrapMetadata(EncryptionTests.metadata1.Value + EncryptionTests.metadataUpdateSuffix), dekProperties.EncryptionKeyWrapMetadata);

            // Use a different client instance to avoid (unintentional) cache impact
            using (CosmosClient client = EncryptionTests.GetClient())
            {
                DataEncryptionKeyProperties readProperties = await ((DatabaseCore)(DatabaseInlineCore)client.GetDatabase(this.database.Id)).GetDataEncryptionKey(dekId).ReadAsync();
                Assert.AreEqual(dekProperties, readProperties);
            }
        }

        [TestMethod]
        public async Task EncryptionDekReadFeed()
        {
            string contosoV1 = "Contoso_v001";
            string contosoV2 = "Contoso_v002";
            string fabrikamV1 = "Fabrikam_v001";
            string fabrikamV2 = "Fabrikam_v002";

            await this.CreateDekAsync(contosoV1);
            await this.CreateDekAsync(contosoV2);
            await this.CreateDekAsync(fabrikamV1);
            await this.CreateDekAsync(fabrikamV2);

            // Test getting all keys
            await this.IterateDekFeedAsync(
                new List<string> { contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                isExpectedDeksCompleteSetForRequest: true,
                isResultOrderExpected: false);

            // Test getting specific subset of keys
            await this.IterateDekFeedAsync(
                new List<string> { contosoV2 },
                isExpectedDeksCompleteSetForRequest: false,
                isResultOrderExpected: true,
                startId: "Contoso_v000",
                endId: "Contoso_v999",
                isDescending: true,
                itemCountInPage: 1);

            // Ensure only required results are returned (ascending)
            await this.IterateDekFeedAsync(
                  new List<string> { contosoV1, contosoV2 },
                  isExpectedDeksCompleteSetForRequest: true,
                  isResultOrderExpected: true,
                  startId: "Contoso_v000",
                  endId: "Contoso_v999",
                  isDescending: false);

            // Test startId inclusive and endId inclusive (ascending)
            await this.IterateDekFeedAsync(
                new List<string> { contosoV1, contosoV2 },
                isExpectedDeksCompleteSetForRequest: true,
                isResultOrderExpected: true,
                startId: "Contoso_v001",
                endId: "Contoso_v002",
                isDescending: false);

            // Ensure only required results are returned (descending)
            await this.IterateDekFeedAsync(
                new List<string> { contosoV2, contosoV1 },
                isExpectedDeksCompleteSetForRequest: true,
                isResultOrderExpected: true,
                startId: "Contoso_v000",
                endId: "Contoso_v999",
                isDescending: true);

            // Test startId inclusive and endId inclusive (descending)
            await this.IterateDekFeedAsync(
                new List<string> { contosoV2, contosoV1 },
                isExpectedDeksCompleteSetForRequest: true,
                isResultOrderExpected: true,
                startId: "Contoso_v001",
                endId: "Contoso_v002",
                isDescending: true);

            // Test pagination
            await this.IterateDekFeedAsync(
                new List<string> { contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                isExpectedDeksCompleteSetForRequest: true,
                isResultOrderExpected: false,
                itemCountInPage: 3);
        }

        [TestMethod]
        public async Task EncryptionCreateItem()
        {
            await this.CreateDekAsync(EncryptionTests.dekId);

            TestDoc testDoc = await this.CreateItemAsync(EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            await this.VerifyItemByReadAsync(testDoc);

            await this.VerifyItemByReadStreamAsync(this.container, testDoc);

            TestDoc expectedDoc = new TestDoc(testDoc);
            expectedDoc.Sensitive = null;

            await this.ValidateQueryResultsAsync("SELECT * FROM c", expectedDoc);
            await this.ValidateQueryResultsAsync(
                string.Format(
                    "SELECT * FROM c where c.PK = '{0}' and c.id = '{1}' and c.NonSensitive = '{2}'",
                    expectedDoc.PK,
                    expectedDoc.Id,
                    expectedDoc.NonSensitive),
                expectedDoc);
            await this.ValidateQueryResultsAsync("SELECT c.id, c.PK, c.Sensitive, c.NonSensitive FROM c", expectedDoc);
            await this.ValidateQueryResultsAsync("SELECT c.id, c.PK, c.NonSensitive FROM c", expectedDoc);
            await this.ValidateQueryResultsAsync(string.Format("SELECT * FROM c where c.Sensitive = '{0}'", testDoc.Sensitive), expectedDoc: null);

            QueryDefinition queryDefinition = new QueryDefinition(
            "select * from c where c.id = @theId and c.PK = @thePK")
                 .WithParameter("@theId", expectedDoc.Id)
                 .WithParameter("@thePK", expectedDoc.PK);
            await this.ValidateQueryResultsAsync(queryDefinition: queryDefinition, expectedDoc: expectedDoc);

            await this.ValidateSprocResultsAsync(expectedDoc);
        }

        private async Task ValidateSprocResultsAsync(TestDoc expectedDoc)
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
                await this.container.Scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);

            StoredProcedureExecuteResponse<TestDoc> sprocResponse = await this.container.Scripts.ExecuteStoredProcedureAsync<TestDoc>(
                sprocId,
                new PartitionKey(expectedDoc.PK),
                parameters: new dynamic[] { expectedDoc.Id });

            Assert.AreEqual(expectedDoc, sprocResponse.Resource);
        }

        // One of query or queryDefinition is to be passed in non-null
        private async Task ValidateQueryResultsAsync(string query = null, TestDoc expectedDoc = null, QueryDefinition queryDefinition = null)
        {
            QueryRequestOptions requestOptions = expectedDoc != null ? new QueryRequestOptions() { PartitionKey = new PartitionKey(expectedDoc.PK) } : null;
            FeedIterator<TestDoc> queryResponseIterator;
            if (query != null)
            {
                queryResponseIterator = this.container.GetItemQueryIterator<TestDoc>(query, requestOptions: requestOptions);
            }
            else
            {
                queryResponseIterator = this.container.GetItemQueryIterator<TestDoc>(queryDefinition, requestOptions: requestOptions);
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

        [TestMethod]
        public async Task EncryptionUpsertAndReplaceItem()
        {
            await this.CreateDekAsync(EncryptionTests.dekId);

            TestDoc testDoc = await this.UpsertItemAsync(TestDoc.Create(), EncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.Created);

            await this.VerifyItemByReadAsync(testDoc);

            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            ItemResponse<TestDoc> upsertResponse = await this.UpsertItemAsync(testDoc, EncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.OK);
            TestDoc updatedDoc = upsertResponse.Resource;

            await this.VerifyItemByReadAsync(updatedDoc);

            updatedDoc.NonSensitive = Guid.NewGuid().ToString();
            updatedDoc.Sensitive = Guid.NewGuid().ToString();

            TestDoc replacedDoc = await this.ReplaceItemAsync(
                updatedDoc,
                EncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                upsertResponse.ETag);

            await this.VerifyItemByReadAsync(replacedDoc);
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuth()
        {
           DataEncryptionKeyProperties dekProperties = await this.CreateDekAsync(EncryptionTests.dekId);

            User user = this.database.GetUser(Guid.NewGuid().ToString());
            await this.database.CreateUserAsync(user.Id);

            PermissionProperties permission = await user.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, this.containerInlineCore));

            TestDoc testDoc = await this.CreateItemAsync(EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            (string endpoint, string _) = TestCommon.GetAccountInfo();
            CosmosClient resourceTokenBasedClient = new CosmosClientBuilder(endpoint, permission.Token)
                .WithEncryptionSettings(new EncryptionSettings(new TestKeyWrapProvider()))
                .Build();

            Container containerForTokenClient = resourceTokenBasedClient.GetDatabase(this.database.Id).GetContainer(this.container.Id);

            try
            {
                TestDoc readDoc = await containerForTokenClient.ReadItemAsync<TestDoc>(testDoc.Id, new PartitionKey(testDoc.PK));
                Assert.Fail("Expected resource token based client to not be able to decrypt data");
            }
            catch(CosmosException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
            }

            await this.VerifyItemByReadStreamAsync(containerForTokenClient, testDoc);
        }

        [TestMethod]
        public async Task EncryptionRestrictedProperties()
        {
            await this.CreateDekAsync(EncryptionTests.dekId);

            TestDoc testDoc = TestDoc.Create();

            try
            {
                await this.CreateItemAsync(EncryptionTests.dekId, new List<string>() { "/id" });
                Assert.Fail("Expected item creation with id specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }

            try
            {
                await this.CreateItemAsync(EncryptionTests.dekId, new List<string>() { "/PK" });
                Assert.Fail("Expected item creation with PK specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }
        }
        private static CosmosClient GetClient()
        {
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            return new CosmosClientBuilder(endpoint, authKey)
                .WithEncryptionSettings(new EncryptionSettings(new TestKeyWrapProvider()))
                .Build();
        }

        private async Task IterateDekFeedAsync(
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

            FeedIterator<DataEncryptionKeyProperties> dekIterator = this.database.GetDataEncryptionKeyIterator(
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

        private async Task<ItemResponse<TestDoc>> UpsertItemAsync(TestDoc testDoc, string dekId, List<string> pathsToEncrypt, HttpStatusCode expectedStatusCode)
        {
            ItemResponse<TestDoc> upsertResponse = await this.container.UpsertItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                 requestOptions: new ItemRequestOptions
                 {
                     EncryptionOptions = new EncryptionOptions
                     {
                         DataEncryptionKey = this.database.GetDataEncryptionKey(dekId),
                         PathsToEncrypt = pathsToEncrypt
                     }
                 });
            Assert.AreEqual(expectedStatusCode, upsertResponse.StatusCode);
            Assert.AreEqual(testDoc, upsertResponse.Resource);
            return upsertResponse;
        }

        private async Task<ItemResponse<TestDoc>> CreateItemAsync(string dekId, List<string> pathsToEncrypt)
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await this.container.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                 requestOptions: new ItemRequestOptions
                 {
                     EncryptionOptions = new EncryptionOptions
                     {
                         DataEncryptionKey = this.database.GetDataEncryptionKey(dekId),
                         PathsToEncrypt = pathsToEncrypt
                     }
                 });
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
            return createResponse;
        }

        private async Task<ItemResponse<TestDoc>> ReplaceItemAsync(TestDoc testDoc, string dekId, List<string> pathsToEncrypt, string etag = null)
        {
            ItemResponse<TestDoc> replaceResponse = await this.container.ReplaceItemAsync(
                testDoc,
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                 requestOptions: new ItemRequestOptions
                 {
                     EncryptionOptions = new EncryptionOptions
                     {
                         DataEncryptionKey = this.database.GetDataEncryptionKey(dekId),
                         PathsToEncrypt = pathsToEncrypt
                     },
                     IfMatchEtag = etag
                 });
            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            Assert.AreEqual(testDoc, replaceResponse.Resource);
            return replaceResponse;
        }

        private async Task VerifyItemByReadStreamAsync(Container container, TestDoc testDoc)
        {
            // ReadItemStream should not decrypt
            ResponseMessage readResponseMessage = await container.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.OK, readResponseMessage.StatusCode);
            Assert.IsNotNull(readResponseMessage.Content);
            TestDoc readDocEncrypted = TestCommon.SerializerCore.FromStream<TestDoc>(readResponseMessage.Content);
            Assert.AreEqual(testDoc.Id, readDocEncrypted.Id);
            Assert.AreEqual(testDoc.PK, readDocEncrypted.PK);
            Assert.AreEqual(testDoc.NonSensitive, readDocEncrypted.NonSensitive);
            Assert.IsNull(readDocEncrypted.Sensitive);
        }

        private async Task VerifyItemByReadAsync(TestDoc testDoc)
        {
            // Read should decrypt properly
            ItemResponse<TestDoc> readResponse = await this.container.ReadItemAsync<TestDoc>(testDoc.Id, new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.AreEqual(testDoc, readResponse.Resource);
        }

        private async Task<DataEncryptionKeyProperties> CreateDekAsync(string dekId)
        {
            DataEncryptionKeyResponse dekResponse = await this.database.CreateDataEncryptionKeyAsync(
                dekId,
                CosmosEncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_256_RANDOMIZED,
                EncryptionTests.metadata1);

            Assert.AreEqual(HttpStatusCode.Created, dekResponse.StatusCode);
            Assert.IsTrue(dekResponse.RequestCharge > 0);
            Assert.IsNotNull(dekResponse.ETag);

            DataEncryptionKeyProperties dekProperties = dekResponse.Resource;
            Assert.AreEqual(dekResponse.ETag, dekProperties.ETag);
            Assert.AreEqual(dekId, dekProperties.Id);
            return dekProperties;
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
