//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests.Utils;
    using Microsoft.Data.Encryption.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Initial emulator coverage for StreamProcessor (streaming JSON encryption/decryption).
    /// Focus: basic CRUD roundtrip with primitives + containers and compression threshold behavior.
    /// Additional scenarios can be appended (change feed, batch, patch, rewrap) in follow-up PRs.
    /// </summary>
    [TestClass]
    public class StreamProcessorEmulatorTests
    {
        private const string DekId = "streamingDek";
        private static CosmosClient client;
        private static Database database;
        private static Container keyContainer;
        private static Container plainContainer;
        private static Container encContainer;
        private static CosmosDataEncryptionKeyProvider dekProvider;
    private static TestEncryptor encryptor;
        private static DataEncryptionKeyProperties dekProperties;

        [ClassInitialize]
        public static async Task Init(TestContext ctx)
        {
            _ = ctx;
            client = Utils.TestCommon.CreateCosmosClient();
            database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            keyContainer = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            plainContainer = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk", 400);

            dekProvider = new CosmosDataEncryptionKeyProvider(new TestEncryptionKeyStoreProvider());
            await dekProvider.InitializeAsync(database, keyContainer.Id);
            dekProperties = await CreateDekAsync(dekProvider, DekId);
            encryptor = new TestEncryptor(dekProvider);
            encContainer = plainContainer.WithEncryptor(encryptor);
        }

        [ClassCleanup]
        public static async Task Cleanup()
        {
            if (database != null)
            {
                using (await database.DeleteStreamAsync()) { }
            }
            client?.Dispose();
        }

        private static async Task<DataEncryptionKeyProperties> CreateDekAsync(CosmosDataEncryptionKeyProvider provider, string id)
        {
            ItemResponse<DataEncryptionKeyProperties> response = await provider.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                id,
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                new Custom.EncryptionKeyWrapMetadata(name: "metadata1", value: "value1"));
            return response.Resource;
        }

        private static EncryptionOptions CreateStreamingOptions(IEnumerable<string> paths, int? minimalCompressedLength = null)
        {
            CompressionOptions comp = new CompressionOptions { Algorithm = CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel = System.IO.Compression.CompressionLevel.Fastest };
            if (minimalCompressedLength.HasValue)
            {
                comp.MinimalCompressedLength = minimalCompressedLength.Value;
            }

            return new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                JsonProcessor = JsonProcessor.Stream,
                PathsToEncrypt = paths,
                CompressionOptions = comp,
            };
        }

        private class Doc
        {
            public string Id { get; set; }
            public string Pk { get; set; }
            public string SecretStr { get; set; }
            public long SecretNum { get; set; }
            public object SecretObj { get; set; }
            public object[] SecretArr { get; set; }
            public string Plain { get; set; }
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

            string[] paths = new[] { "/SecretStr", "/SecretNum", "/SecretObj", "/SecretArr" };
            ItemResponse<Doc> createResp = await encContainer.CreateItemAsync(doc, new PartitionKey(doc.Pk), new EncryptionItemRequestOptions
            {
                EncryptionOptions = CreateStreamingOptions(paths)
            });
            Assert.AreEqual(System.Net.HttpStatusCode.Created, createResp.StatusCode);

            ItemResponse<Doc> readResp = await encContainer.ReadItemAsync<Doc>(doc.Id, new PartitionKey(doc.Pk), new ItemRequestOptions
            {
                // Streaming decrypt path is automatically chosen by EncryptionContainer when _ei present
            });
            Doc roundtrip = readResp.Resource;
            Assert.AreEqual(doc.SecretStr, roundtrip.SecretStr);
            Assert.AreEqual(doc.SecretNum, roundtrip.SecretNum);
            Assert.AreEqual(doc.Plain, roundtrip.Plain);
        }

        [TestMethod]
        public async Task StreamingEncryption_CompressionThreshold_Behavior()
        {
            string large = new string('x', 1500);
            string small = new string('y', 30);
            var doc = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "p",
                Large = large,
                Small = small,
                Plain = 42
            };
            string[] paths = new[] { "/Large", "/Small" };
            int threshold = 256; // small below, large above
            EncryptionOptions options = CreateStreamingOptions(paths, threshold);
            ItemResponse<dynamic> resp = await encContainer.CreateItemAsync<dynamic>(doc, new PartitionKey("p"), new EncryptionItemRequestOptions { EncryptionOptions = options });
            Assert.AreEqual(System.Net.HttpStatusCode.Created, resp.StatusCode);

            // Raw read to inspect encryption metadata JSON.
            using ResponseMessage raw = await encContainer.ReadItemStreamAsync(doc.id, new PartitionKey("p"));
            raw.EnsureSuccessStatusCode();
            using MemoryStream ms = new MemoryStream();
            await raw.Content.CopyToAsync(ms);
            ms.Position = 0;
            using System.Text.Json.JsonDocument jsonDoc = System.Text.Json.JsonDocument.Parse(ms);
            System.Text.Json.JsonElement root = jsonDoc.RootElement;
            Assert.IsTrue(root.TryGetProperty("_ei", out System.Text.Json.JsonElement eiEl));
            EncryptionProperties props = System.Text.Json.JsonSerializer.Deserialize<EncryptionProperties>(eiEl.GetRawText());
            Assert.IsTrue(props.CompressedEncryptedPaths.ContainsKey("/Large"));
            Assert.IsFalse(props.CompressedEncryptedPaths.ContainsKey("/Small"));

            ItemResponse<dynamic> read = await encContainer.ReadItemAsync<dynamic>(doc.id, new PartitionKey("p"));
            dynamic rt = read.Resource;
            Assert.AreEqual(large.Length, ((string)rt.Large).Length);
            Assert.AreEqual(small, (string)rt.Small);
            Assert.AreEqual(42L, (long)rt.Plain);
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

            public override Task<Custom.DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
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
