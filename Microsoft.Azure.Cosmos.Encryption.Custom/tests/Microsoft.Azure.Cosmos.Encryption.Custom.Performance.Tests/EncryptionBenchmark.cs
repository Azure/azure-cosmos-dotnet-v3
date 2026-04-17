namespace Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests
{
    using System.IO;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Data.Encryption.Cryptography;
#if NET8_0_OR_GREATER
    using Microsoft.IO;
#endif

    using Moq;

    [MemoryDiagnoser]
    public partial class EncryptionBenchmark
    {
        /// <summary>
        /// Concrete key store provider — MDE requires ProviderName, Sign, and Verify
        /// to be implemented. Moq-based setups silently fail inside MDE's internal caching.
        /// </summary>
        private sealed class BenchmarkKeyStoreProvider : EncryptionKeyStoreProvider
        {
            private static readonly byte[] RawKey = Enumerable.Range(0, 32).Select(static i => (byte)(255 - i)).ToArray();

            public override string ProviderName => "benchmark-store";

            public override byte[] UnwrapKey(string encryptionKeyId, KeyEncryptionKeyAlgorithm algorithm, byte[] encryptedKey)
                => RawKey;

            public override byte[] WrapKey(string encryptionKeyId, KeyEncryptionKeyAlgorithm algorithm, byte[] key)
                => key;

            public override byte[] Sign(string encryptionKeyId, bool allowEnclaveComputations)
                => new byte[] { 0x01 };

            public override bool Verify(string encryptionKeyId, bool allowEnclaveComputations, byte[] signature)
                => signature?.Length == 1 && signature[0] == 0x01;
        }

        private static class RequestOptionsOverrideHelper
        {
            public static RequestOptions? Create(JsonProcessor processor)
            {
#if NET8_0_OR_GREATER
                if (processor == JsonProcessor.Newtonsoft)
                {
                    return null;
                }
                return new ItemRequestOptions
                {
                    Properties = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "encryption-json-processor", processor.ToString() }
                    }
                };
#else
                return null;
#endif
            }
        }

        private static readonly BenchmarkKeyStoreProvider KeyStoreProvider = new();
        private static readonly DataEncryptionKeyProperties DekProperties = new(
                "id",
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                Enumerable.Range(0, 32).Select(static i => (byte)i).ToArray(),
                new EncryptionKeyWrapMetadata("name", "value"), DateTime.UtcNow);

#if NET8_0_OR_GREATER
        private readonly RecyclableMemoryStreamManager recyclableMemoryStreamManager = new ();
#endif

        private CosmosEncryptor? encryptor;

        private EncryptionOptions? encryptionOptions;
        private byte[]? encryptedData;
        private byte[]? plaintext;

        [Params(1, 10, 100)]
        public int DocumentSizeInKb { get; set; }

#if NET8_0_OR_GREATER
        [Params("Newtonsoft", "Stream")]
#else
        [Params("Newtonsoft")]
#endif
        public string Processor { get; set; } = "Newtonsoft";

        internal JsonProcessor JsonProcessor => Enum.Parse<JsonProcessor>(this.Processor);

        [GlobalSetup]
        public async Task Setup()
        {
            Mock<DataEncryptionKeyProvider> keyProvider = new();
            keyProvider
                .Setup(x => x.FetchDataEncryptionKeyWithoutRawKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async () => await MdeEncryptionAlgorithm.CreateAsync(DekProperties, EncryptionType.Randomized, KeyStoreProvider, cacheTimeToLive: TimeSpan.MaxValue, false, default));

            this.encryptor = new(keyProvider.Object);
            this.encryptionOptions = this.CreateEncryptionOptions();
            this.plaintext = this.LoadTestDoc();

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                 new MemoryStream(this.plaintext),
                 this.encryptor,
                 this.encryptionOptions,
                 this.JsonProcessor,
                 new CosmosDiagnosticsContext(),
                 CancellationToken.None);

            using MemoryStream memoryStream = new ();
            encryptedStream.CopyTo(memoryStream);
            this.encryptedData = memoryStream.ToArray();
        }

        
        [Benchmark]
        public async Task Encrypt()
        {
            await EncryptionProcessor.EncryptAsync(
                 new MemoryStream(this.plaintext!),
                 this.encryptor,
                 this.encryptionOptions,
                 this.JsonProcessor,
                 new CosmosDiagnosticsContext(),
                 CancellationToken.None);
        }

#if NET8_0_OR_GREATER
        [Benchmark]
        public async Task EncryptToProvidedStream()
        {
            using RecyclableMemoryStream rms = new (this.recyclableMemoryStreamManager);
            await EncryptionProcessor.EncryptAsync(
                new MemoryStream(this.plaintext!),
                rms,
                this.encryptor,
                this.encryptionOptions,
                 this.JsonProcessor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);
        }
#endif

        [Benchmark]
        public async Task Decrypt()
        {
            await EncryptionProcessor.DecryptAsync(
                new MemoryStream(this.encryptedData!),
                this.encryptor,
                new CosmosDiagnosticsContext(),
                RequestOptionsOverrideHelper.Create(this.JsonProcessor),
                CancellationToken.None);
        }

#if NET8_0_OR_GREATER
        [Benchmark]
        public async Task DecryptToProvidedStream()
        {
            using RecyclableMemoryStream rms = new(this.recyclableMemoryStreamManager);
            await EncryptionProcessor.DecryptAsync(
                new MemoryStream(this.encryptedData!),
                rms,
                this.encryptor,
                new CosmosDiagnosticsContext(),
                RequestOptionsOverrideHelper.Create(this.JsonProcessor),
                CancellationToken.None);
        }
#endif

        private EncryptionOptions CreateEncryptionOptions()
        {
            EncryptionOptions options = new()
            {
                DataEncryptionKeyId = "dekId",
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };

            return options;
        }

        private byte[] LoadTestDoc()
        {
            string name = $"Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests.sampledata.testdoc-{this.DocumentSizeInKb}kb.json";
            using Stream resourceStream = typeof(EncryptionBenchmark).Assembly.GetManifestResourceStream(name)!;
            
            byte[] buffer = new byte[resourceStream!.Length];
            resourceStream.Read(buffer, 0, buffer.Length);

            return buffer;
        }
    }
}
