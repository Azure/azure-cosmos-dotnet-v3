//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Data.Encryption.Cryptography;
    using Newtonsoft.Json;
    using Utils;
    using VisualStudio.TestTools.UnitTesting;
    using DataEncryptionKey = Custom.DataEncryptionKey;

    /// <summary>
    /// Initial emulator coverage for StreamProcessor (streaming JSON encryption/decryption).
    /// Focus: basic CRUD roundtrip with primitives + containers (no compression scenarios here; those are covered by unit tests).
    /// Additional scenarios can be appended (change feed, batch, patch, rewrap) in follow-up PRs.
    /// </summary>
    [TestClass]
    public class StreamProcessorEmulatorTests
    {
        private const string DekId = "streamingDek";
        private static CosmosClient _client;
        private static Database _database;
        private static Container _keyContainer;
        private static Container _plainContainer;
        private static Container _encContainer;
        private static CosmosDataEncryptionKeyProvider _dekProvider;
        private static TestEncryptor _encryptor;

        [ClassInitialize]
        public static async Task Init(TestContext ctx)
        {
            _ = ctx;
            _client = TestCommon.CreateCosmosClient();
            _database = await _client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            _keyContainer = await _database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            _plainContainer = await _database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk", 400);

            _dekProvider = new CosmosDataEncryptionKeyProvider(new TestEncryptionKeyStoreProvider());
            await _dekProvider.InitializeAsync(_database, _keyContainer.Id);
            await CreateDekAsync(_dekProvider, DekId);
            _encryptor = new TestEncryptor(_dekProvider);
            _encContainer = _plainContainer.WithEncryptor(_encryptor);
        }

        [ClassCleanup]
        public static async Task Cleanup()
        {
            if (_database != null)
            {
                using (await _database.DeleteStreamAsync()) { }
            }
            _client?.Dispose();
        }

        private static async Task<DataEncryptionKeyProperties> CreateDekAsync(CosmosDataEncryptionKeyProvider provider, string id)
        {
            ItemResponse<DataEncryptionKeyProperties> response = await provider.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                id,
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                new EncryptionKeyWrapMetadata(name: "metadata1", value: "value1"));
            return response.Resource;
        }

        private static EncryptionOptions CreateStreamingOptions(IEnumerable<string> paths)
        {
            // Component (emulator) tests purposefully do not exercise compression (validated by unit tests already).
            return new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                JsonProcessor = JsonProcessor.Stream,
                PathsToEncrypt = paths,
                // CompressionOptions omitted intentionally.
            };
        }

        private class Doc
        {
            // Map to required lowercase JSON field names for Cosmos DB (SDK default serializer is Newtonsoft.Json)
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }
            [JsonProperty(PropertyName = "pk")]
            public string Pk { get; set; }
            public string SecretStr { get; set; }
            public long SecretNum { get; set; }
            public object SecretObj { get; set; }
            public object[] SecretArr { get; set; }
            public string Plain { get; set; }
        }

        private static async Task<string> GetRawJsonAsync(string id, string pk)
        {
            using ResponseMessage raw = await _plainContainer.ReadItemStreamAsync(id, new PartitionKey(pk));
            raw.EnsureSuccessStatusCode();
            using MemoryStream ms = new MemoryStream();
            await raw.Content.CopyToAsync(ms);
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static async Task ValidateRawEncryptedAsync(string id, string pk, IEnumerable<string> encryptedPlaintextValues, string expectedPlainValue)
        {
            string rawJson = await GetRawJsonAsync(id, pk);
            EncryptionVerificationTestHelper.AssertEncryptedRawJson(rawJson, encryptedPlaintextValues, string.IsNullOrEmpty(expectedPlainValue) ? null : new[] { expectedPlainValue });
        }

        [TestMethod]
        public async Task StreamingEncryption_BasicCrud_Roundtrip()
        {
            Doc doc = new Doc
            {
                Id = Guid.NewGuid().ToString(),
                Pk = "p",
                SecretStr = "hello world",
                SecretNum = 1234567890123,
                SecretObj = new { a = 5, b = "text" },
                SecretArr = new object[] { 1, 2, 3 },
                Plain = "plainValue"
            };

            string[] paths = { "/SecretStr", "/SecretNum", "/SecretObj", "/SecretArr" };
            ItemResponse<Doc> createResp = await _encContainer.CreateItemAsync(doc, new PartitionKey(doc.Pk), new EncryptionItemRequestOptions
            {
                EncryptionOptions = CreateStreamingOptions(paths)
            });
            Assert.AreEqual(HttpStatusCode.Created, createResp.StatusCode);

            ItemResponse<Doc> readResp = await _encContainer.ReadItemAsync<Doc>(doc.Id, new PartitionKey(doc.Pk), new ItemRequestOptions
            {
                // Streaming decrypt path is automatically chosen by EncryptionContainer when _ei present
            });
            Doc roundtrip = readResp.Resource;
            Assert.AreEqual(doc.SecretStr, roundtrip.SecretStr);
            Assert.AreEqual(doc.SecretNum, roundtrip.SecretNum);
            Assert.AreEqual(doc.Plain, roundtrip.Plain);

            await ValidateRawEncryptedAsync(doc.Id, doc.Pk, new[] { doc.SecretStr }, doc.Plain);
        }

        [TestMethod]
        public async Task StreamingEncryption_TransactionalBatch_Roundtrip()
        {
            string pk = "batchPK";
            string[] paths = { "/SecretStr" };
            // Prepare two docs
            Doc d1 = new Doc { Id = Guid.NewGuid().ToString(), Pk = pk, SecretStr = "secret_batch_value_one", SecretNum = 1, SecretObj = new { v = 1 }, SecretArr = new object[] { 1 }, Plain = "p1" };
            Doc d2 = new Doc { Id = Guid.NewGuid().ToString(), Pk = pk, SecretStr = "secret_batch_value_two", SecretNum = 2, SecretObj = new { v = 2 }, SecretArr = new object[] { 2 }, Plain = "p2" };

            TransactionalBatch batch = _encContainer.CreateTransactionalBatch(new PartitionKey(pk))
                .CreateItem(d1, new EncryptionTransactionalBatchItemRequestOptions { EncryptionOptions = CreateStreamingOptions(paths) })
                .CreateItem(d2, new EncryptionTransactionalBatchItemRequestOptions { EncryptionOptions = CreateStreamingOptions(paths) });

            TransactionalBatchResponse resp = await batch.ExecuteAsync();
            Assert.IsTrue(resp.IsSuccessStatusCode, "Batch should succeed");
            Assert.AreEqual(2, resp.Count);

            ItemResponse<Doc> read1 = await _encContainer.ReadItemAsync<Doc>(d1.Id, new PartitionKey(pk));
            ItemResponse<Doc> read2 = await _encContainer.ReadItemAsync<Doc>(d2.Id, new PartitionKey(pk));
            Assert.AreEqual(d1.SecretStr, read1.Resource.SecretStr);
            Assert.AreEqual(d2.SecretStr, read2.Resource.SecretStr);

            await ValidateRawEncryptedAsync(d1.Id, pk, new[] { d1.SecretStr }, d1.Plain);
            await ValidateRawEncryptedAsync(d2.Id, pk, new[] { d2.SecretStr }, d2.Plain);
        }

        [TestMethod]
        public async Task StreamingEncryption_ChangeFeed_Roundtrip()
        {
            string pk = "cfPK";
            string[] paths = { "/SecretStr" };
            // Create both items before starting iterator and then read from beginning to ensure we see them.
            Doc d = new Doc { Id = Guid.NewGuid().ToString(), Pk = pk, SecretStr = "secret_cf_value_one", SecretNum = 42, SecretObj = new { v = 99 }, SecretArr = new object[] { 3 }, Plain = "plainCF" };
            Doc d2 = new Doc { Id = Guid.NewGuid().ToString(), Pk = pk, SecretStr = "secret_cf_value_two", SecretNum = 43, SecretObj = new { v = 100 }, SecretArr = new object[] { 4 }, Plain = "plainCF2" };
            await _encContainer.CreateItemAsync(d, new PartitionKey(pk), new EncryptionItemRequestOptions { EncryptionOptions = CreateStreamingOptions(paths) });
            await _encContainer.CreateItemAsync(d2, new PartitionKey(pk), new EncryptionItemRequestOptions { EncryptionOptions = CreateStreamingOptions(paths) });

            FeedIterator<Doc> iterator = _encContainer.GetChangeFeedIterator<Doc>(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.Incremental);

            List<Doc> changes = new List<Doc>();
            for (int i = 0; i < 5 && iterator.HasMoreResults && changes.Count < 1; i++)
            {
                FeedResponse<Doc> fr = await iterator.ReadNextAsync();
                foreach (Doc cd in fr)
                {
                    if (cd.Pk == pk)
                    {
                        changes.Add(cd);
                    }
                }
                if (changes.Count == 0)
                {
                    await Task.Delay(500);
                }
            }

            Assert.IsTrue(changes.Count >= 2, "Expected at least two changes for encrypted items");
            Assert.AreEqual("secret_cf_value_two", changes[^1].SecretStr);

            await ValidateRawEncryptedAsync(d.Id, pk, new[] { d.SecretStr }, d.Plain);
            await ValidateRawEncryptedAsync(d2.Id, pk, new[] { d2.SecretStr }, d2.Plain);
        }

        // Local minimal encryptor implementation
        private sealed class TestEncryptor : Encryptor
        {
            private readonly CosmosEncryptor inner;
            public TestEncryptor(DataEncryptionKeyProvider provider)
            {
                this.inner = new CosmosEncryptor(provider);
            }

            public override Task<byte[]> DecryptAsync(byte[] cipherText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                return this.inner.DecryptAsync(cipherText, dataEncryptionKeyId, encryptionAlgorithm, cancellationToken);
            }

            public override Task<byte[]> EncryptAsync(byte[] plainText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                return this.inner.EncryptAsync(plainText, dataEncryptionKeyId, encryptionAlgorithm, cancellationToken);
            }

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                return this.inner.GetEncryptionKeyAsync(dataEncryptionKeyId, encryptionAlgorithm, cancellationToken);
            }
        }

        private sealed class TestEncryptionKeyStoreProvider : EncryptionKeyStoreProvider
        {
            public override string ProviderName => "LOCAL";
            public override byte[] UnwrapKey(string masterKeyPath, KeyEncryptionKeyAlgorithm encryptionAlgorithm, byte[] encryptedKey)
            {
                return encryptedKey;
            }

            public override byte[] WrapKey(string masterKeyPath, KeyEncryptionKeyAlgorithm encryptionAlgorithm, byte[] key)
            {
                return key;
            }

            public override byte[] Sign(string masterKeyPath, bool allowEnclaveComputations)
            {
                return new byte[32];
            }

            public override bool Verify(string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
            {
                return true;
            }
        }
    }
}
#endif
