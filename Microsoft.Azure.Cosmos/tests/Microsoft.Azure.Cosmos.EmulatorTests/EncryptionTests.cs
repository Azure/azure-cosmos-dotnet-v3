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
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class EncryptionTests
    {
        private static KeyWrapMetadata metadata1 = new KeyWrapMetadata("metadata1");
        private static KeyWrapMetadata metadata2 = new KeyWrapMetadata("metadata2");
        private const string metadataUpdateSuffix = "updated";
        private static TimeSpan cacheTTL = TimeSpan.FromDays(1);

        private const string dekId = "mydek";

        private static CosmosClient client;

        private DatabaseCore database;
        private ContainerCore container;

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
            this.database = (DatabaseCore)await EncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            this.container = (ContainerCore)await this.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/PK", 400);
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
            Assert.AreEqual(new KeyWrapMetadata(EncryptionTests.metadata1.Value + EncryptionTests.metadataUpdateSuffix), dekProperties.KeyWrapMetadata);

            // Use a different client instance to avoid (unintentional) cache impact
            using (CosmosClient client = EncryptionTests.GetClient())
            {
                DataEncryptionKeyProperties readProperties = await client.GetDatabase(this.database.Id).GetDataEncryptionKey(dekId).ReadAsync();
                Assert.AreEqual(dekProperties, readProperties);
            }
        }

        [TestMethod]
        public async Task EncryptionCreateItem()
        {
            await this.CreateDekAsync(EncryptionTests.dekId);

            TestDoc testDoc = await this.CreateItemAsync(EncryptionTests.dekId);

            await this.VerifyItemByReadAsync(testDoc);

            // ReadItemStream should not decrypt
            ResponseMessage readResponseMessage = await this.container.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.OK, readResponseMessage.StatusCode);
            Assert.IsNotNull(readResponseMessage.Content);
            TestDoc readDocEncrypted = TestCommon.Serializer.FromStream<TestDoc>(readResponseMessage.Content);
            Assert.AreEqual(testDoc.Id, readDocEncrypted.Id);
            Assert.AreEqual(testDoc.PK, readDocEncrypted.PK);
            Assert.IsNull(readDocEncrypted.Sensitive);
        }

        [TestMethod]
        public async Task EncryptionUpsertItem()
        {
            await this.CreateDekAsync(EncryptionTests.dekId);

            TestDoc testDoc = await this.UpsertItemAsync(TestDoc.Create(), EncryptionTests.dekId, HttpStatusCode.Created);

            await this.VerifyItemByReadAsync(testDoc);

            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            TestDoc updatedDoc = await this.UpsertItemAsync(testDoc, EncryptionTests.dekId, HttpStatusCode.OK);

            await this.VerifyItemByReadAsync(updatedDoc);
        }

        [TestMethod]
        public async Task EncryptionReplaceItem()
        {
            await this.CreateDekAsync(EncryptionTests.dekId);
            ItemResponse<TestDoc> createResponse = await this.CreateItemAsync(EncryptionTests.dekId);

            TestDoc testDoc = createResponse.Resource;
            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            TestDoc replacedDoc = await this.ReplaceItemAsync(
                testDoc,
                EncryptionTests.dekId,
                createResponse.ETag);

            await this.VerifyItemByReadAsync(replacedDoc);
        }

        private static CosmosClient GetClient()
        {
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            return new CosmosClientBuilder(endpoint, authKey)
                .WithKeyWrapProvider(new TestKeyWrapProvider())
                .Build();
        }

        private async Task<ItemResponse<TestDoc>> UpsertItemAsync(TestDoc testDoc, string dekId, HttpStatusCode expectedStatusCode)
        {
            ItemResponse<TestDoc> upsertResponse = await this.container.UpsertItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                 requestOptions: new ItemRequestOptions
                 {
                     EncryptionOptions = new EncryptionOptions
                     {
                         DataEncryptionKey = this.database.GetDataEncryptionKey(dekId)
                     }
                 });
            Assert.AreEqual(expectedStatusCode, upsertResponse.StatusCode);
            Assert.AreEqual(testDoc, upsertResponse.Resource);
            return upsertResponse;
        }

        private async Task<ItemResponse<TestDoc>> CreateItemAsync(string dekId)
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await this.container.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                 requestOptions: new ItemRequestOptions
                 {
                     EncryptionOptions = new EncryptionOptions
                     {
                         DataEncryptionKey = this.database.GetDataEncryptionKey(dekId)
                     }
                 });
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
            return createResponse;
        }

        private async Task<ItemResponse<TestDoc>> ReplaceItemAsync(TestDoc testDoc, string dekId, string etag = null)
        {
            ItemResponse<TestDoc> replaceResponse = await this.container.ReplaceItemAsync(
                testDoc,
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                 requestOptions: new ItemRequestOptions
                 {
                     EncryptionOptions = new EncryptionOptions
                     {
                         DataEncryptionKey = this.database.GetDataEncryptionKey(dekId)
                     },
                     IfMatchEtag = etag
                 });
            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            Assert.AreEqual(testDoc, replaceResponse.Resource);
            return replaceResponse;
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
            DataEncryptionKeyResponse dekResponse = await this.database.CreateDataEncryptionKeyAsync(dekId, EncryptionTests.metadata1);
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
            [JsonProperty("id")]
            public string Id { get; set; }

            public string PK { get; set; }

            public string NonSensitive { get; set; }

            [CosmosEncrypt]
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

        private class TestKeyWrapProvider : KeyWrapProvider
        {
            public override Task<KeyUnwrapResponse> UnwrapKeyAsync(byte[] wrappedKey, KeyWrapMetadata metadata, CancellationToken cancellationToken)
            {
                int moveBy = metadata.Value == EncryptionTests.metadata1.Value + EncryptionTests.metadataUpdateSuffix ? 1 : 2;
                return Task.FromResult(new KeyUnwrapResponse(wrappedKey.Select(b => (byte)(b - moveBy)).ToArray(), EncryptionTests.cacheTTL));
            }

            public override Task<KeyWrapResponse> WrapKeyAsync(byte[] key, KeyWrapMetadata metadata, CancellationToken cancellationToken)
            {
                KeyWrapMetadata responseMetadata = new KeyWrapMetadata(metadata.Value + EncryptionTests.metadataUpdateSuffix);
                int moveBy = metadata.Value == EncryptionTests.metadata1.Value ? 1 : 2;
                return Task.FromResult(new KeyWrapResponse(key.Select(b => (byte)(b + moveBy)).ToArray(), responseMetadata));
            }
        }
    }
}
