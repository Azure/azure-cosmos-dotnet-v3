//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
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
    class EncryptionContainerTests
    {
        private static readonly EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata("metadata1");
        private const string metadataUpdateSuffix = "updated";
        private const string pdekId1 = "mypdek1";
        private const string pdekId2 = "mypdek2";
        private static CosmosClient client;
        private static Database database;
        private static DataEncryptionKeyProperties pdekProperties;
        private static Container propertyEncryptionContainer;
        private static Container multipropertyMultiDek;
        private static Container multipropertySingleDek;
        private static Container itemContainer;
        private static Container keyContainer;
        private static CosmosDataEncryptionKeyProvider dekProvider;
        private static TestEncryptor encryptor;
        private static TimeSpan cacheTTL = TimeSpan.FromDays(1);
        private static Dictionary<List<string>, string> PathsToEncrypt1 = new Dictionary<List<string>, string> { { TestDoc.PropertyPathsToEncrypt1, pdekId1 } };
        private static Dictionary<List<string>, string> PathsToEncrypt2 = new Dictionary<List<string>, string> { { TestDoc.PropertyPathsToEncrypt1, pdekId1}, { TestDoc.PropertyPathsToEncrypt2, pdekId2 } };
        private static Dictionary<List<string>, string> PathsToEncrypt3 = new Dictionary<List<string>, string> { { TestDoc.PropertyPathsToEncrypt3, pdekId1 } };

        [ClassInitialize]
        public async Task ClassInitialize(TestContext context)
        {
            EncryptionContainerTests.dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            EncryptionContainerTests.encryptor = new TestEncryptor(EncryptionContainerTests.dekProvider);

            EncryptionContainerTests.client = TestCommon.CreateCosmosClient();
            EncryptionContainerTests.database = await EncryptionContainerTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());

            EncryptionContainerTests.keyContainer = await EncryptionContainerTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            await EncryptionContainerTests.dekProvider.InitializeAsync(EncryptionContainerTests.database, EncryptionContainerTests.keyContainer.Id);

            EncryptionContainerTests.itemContainer = await EncryptionContainerTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/PK", 400);
            EncryptionContainerTests.propertyEncryptionContainer = EncryptionContainerTests.itemContainer.WithPropertyEncryptor(encryptor, EncryptionContainerTests.PathsToEncrypt1);
            EncryptionContainerTests.pdekProperties = await EncryptionContainerTests.CreatePropertyDekAsync(EncryptionContainerTests.dekProvider, EncryptionContainerTests.pdekId1);
            EncryptionContainerTests.multipropertyMultiDek = EncryptionContainerTests.itemContainer.WithPropertyEncryptor(encryptor, EncryptionContainerTests.PathsToEncrypt2);
            EncryptionContainerTests.multipropertySingleDek = EncryptionContainerTests.itemContainer.WithPropertyEncryptor(encryptor, EncryptionContainerTests.PathsToEncrypt3);
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            if (EncryptionContainerTests.database != null)
            {
                using (await EncryptionContainerTests.database.DeleteStreamAsync()) { }
            }

            if (EncryptionContainerTests.client != null)
            {
                EncryptionContainerTests.client.Dispose();
            }
        }

        [TestMethod]
        public async Task CreateItemWithPropertyEncr()
        {
            TestDoc testDoc = TestDoc.Create();

            ItemResponse<TestDoc> createResponse = await EncryptionContainerTests.propertyEncryptionContainer.CreateItemAsync(
                testDoc, new PartitionKey(testDoc.PK));

            await EncryptionContainerTests.VerifyItemByReadAsync(EncryptionContainerTests.propertyEncryptionContainer, testDoc);

            await EncryptionContainerTests.VerifyItemByReadStreamAsync(EncryptionContainerTests.propertyEncryptionContainer, testDoc);

            Assert.AreNotEqual(createResponse.Resource.Name, testDoc.Name);
        }

        [TestMethod]
        public async Task CreateStreamItemWithPropertyEncr()
        {
            TestDoc testDoc = TestDoc.Create();
            Stream testStream = testDoc.ToStream();

            await EncryptionContainerTests.propertyEncryptionContainer.CreateItemStreamAsync(
                testStream, new PartitionKey(testDoc.PK));

            await EncryptionContainerTests.VerifyItemByReadAsync(EncryptionContainerTests.propertyEncryptionContainer, testDoc);

            await EncryptionContainerTests.VerifyItemByReadStreamAsync(EncryptionContainerTests.propertyEncryptionContainer, testDoc);

        }

        [TestMethod]
        public async Task CreateItemWith2PropertyEncr()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await EncryptionContainerTests.multipropertyMultiDek.CreateItemAsync(
                testDoc, new PartitionKey(testDoc.PK));

            await EncryptionContainerTests.VerifyItemByReadAsync(EncryptionContainerTests.multipropertyMultiDek, testDoc);

            await EncryptionContainerTests.VerifyItemByReadStreamAsync(EncryptionContainerTests.multipropertyMultiDek, testDoc);

        }

        [TestMethod]
        public async Task CreateItemWith2Property1DekEncr()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await EncryptionContainerTests.multipropertySingleDek.CreateItemAsync(
                testDoc, new PartitionKey(testDoc.PK));

            await EncryptionContainerTests.VerifyItemByReadAsync(EncryptionContainerTests.multipropertySingleDek, testDoc);

            await EncryptionContainerTests.VerifyItemByReadStreamAsync(EncryptionContainerTests.multipropertySingleDek, testDoc);

        }

        [TestMethod]
        public async Task GetItemQuery()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponseb = await EncryptionContainerTests.propertyEncryptionContainer.CreateItemAsync(
                testDoc, new PartitionKey(testDoc.PK));
            TestDoc expectedDoc = new TestDoc(testDoc);

            await EncryptionContainerTests.ValidateQueryResultsAsync(
                  EncryptionContainerTests.propertyEncryptionContainer,
                  "SELECT * FROM c",
                  expectedDoc);

            await EncryptionContainerTests.ValidateQueryResultsAsync(
                EncryptionContainerTests.propertyEncryptionContainer,
                string.Format(
                    "SELECT * FROM c where c.Name = {0}",
                    expectedDoc.Name),
                    expectedDoc);

            await EncryptionContainerTests.ValidateQueryResultsAsync(
                EncryptionContainerTests.propertyEncryptionContainer,
                queryDefinition: new QueryDefinition(
                    "select * from c where c.Name = @Name")
                         .WithParameter("@Name", expectedDoc.Name),
                expectedDoc: expectedDoc);

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

        private static async Task ValidatePropertyEncryptedQueryResponseAsync(Container container,
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

            ResponseMessage response = await feedIterator.ReadNextAsync();
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.IsNull(response.ErrorMessage);
        }
        private static async Task<DataEncryptionKeyProperties> CreateDekAsync(CosmosDataEncryptionKeyProvider dekProvider, string dekId)
        {
            ItemResponse<DataEncryptionKeyProperties> dekResponse = await dekProvider.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                dekId,
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                EncryptionContainerTests.metadata1);

            Assert.AreEqual(HttpStatusCode.Created, dekResponse.StatusCode);

            return VerifyDekResponse(dekResponse,
                dekId);
        }

        private static async Task<DataEncryptionKeyProperties> CreatePropertyDekAsync(CosmosDataEncryptionKeyProvider dekProvider, string dekId)
        {
            ItemResponse<DataEncryptionKeyProperties> dekResponse = await dekProvider.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                dekId,
                CosmosEncryptionAlgorithm.AEADAes256CbcHmacSha256Deterministic,
                EncryptionContainerTests.metadata1);

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

        private class TestKeyWrapProvider : EncryptionKeyWrapProvider
        {
            public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(byte[] wrappedKey, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken)
            {
                int moveBy = metadata.Value == EncryptionContainerTests.metadata1.Value + EncryptionContainerTests.metadataUpdateSuffix ? 1 : 2;
                return Task.FromResult(new EncryptionKeyUnwrapResult(wrappedKey.Select(b => (byte)(b - moveBy)).ToArray(), EncryptionContainerTests.cacheTTL));
            }

            public override Task<EncryptionKeyWrapResult> WrapKeyAsync(byte[] key, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken)
            {
                EncryptionKeyWrapMetadata responseMetadata = new EncryptionKeyWrapMetadata(metadata.Value + EncryptionContainerTests.metadataUpdateSuffix);
                int moveBy = metadata.Value == EncryptionContainerTests.metadata1.Value ? 1 : 2;
                return Task.FromResult(new EncryptionKeyWrapResult(key.Select(b => (byte)(b + moveBy)).ToArray(), responseMetadata));
            }
        }

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
        }

        internal class TestDoc
        {
            public static List<string> PropertyPathsToEncrypt1 { get; } = new List<string> { "/Name" };
            public static List<string> PropertyPathsToEncrypt2 { get; } = new List<string> { "/City" };
            public static List<string> PropertyPathsToEncrypt3 { get; } = new List<string> { "/SSN", "/Name" };

            [JsonProperty("id")]
            public string Id { get; set; }

            public string PK { get; set; }

            public string Name { get; set; }
            public string City { get; set; }
            public int SSN { get; set; }

            public string Sensitive { get; set; }

            public TestDoc()
            {
            }

            public TestDoc(TestDoc other)
            {
                this.Id = other.Id;
                this.PK = other.PK;
                this.Name = other.Name;
                this.City = other.City;
                this.SSN = other.SSN;
                this.Sensitive = other.Sensitive;
            }

            public override bool Equals(object obj)
            {
                return obj is TestDoc doc
                       && this.Id == doc.Id
                       && this.PK == doc.PK
                       && this.Name == doc.Name
                       && this.City == doc.City
                       && this.SSN == doc.SSN
                       && this.Sensitive == this.Sensitive;
            }

            public override int GetHashCode()
            {
                int hashCode = 1652434776;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Id);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.PK);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Name);
                hashCode = (hashCode * -1521134295) + EqualityComparer<int>.Default.GetHashCode(this.SSN);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Sensitive);
                return hashCode;
            }

            public static TestDoc Create(string partitionKey = null)
            {
                return new TestDoc()
                {
                    Id = Guid.NewGuid().ToString(),
                    PK = partitionKey ?? Guid.NewGuid().ToString(),
                    Name = "myName",
                    City = "myCity",
                    SSN = new Random().Next(),
                    Sensitive = Guid.NewGuid().ToString()
                };
            }

            public Stream ToStream()
            {
                return TestCommon.ToStream(this);
            }
        }

    }

}