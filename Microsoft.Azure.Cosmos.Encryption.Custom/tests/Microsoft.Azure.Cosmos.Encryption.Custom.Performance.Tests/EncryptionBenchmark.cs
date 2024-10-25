namespace Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests
{
    using System.IO;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Data.Encryption.Cryptography;
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
    using Microsoft.IO;
#endif
    using Moq;

    [RPlotExporter]
    public partial class EncryptionBenchmark
    {
        private static readonly byte[] DekData = Enumerable.Repeat((byte)0, 32).ToArray();
        private static readonly DataEncryptionKeyProperties DekProperties = new(
                "id",
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                DekData,
                new EncryptionKeyWrapMetadata("name", "value"), DateTime.UtcNow);
        private static readonly Mock<EncryptionKeyStoreProvider> StoreProvider = new();

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        private readonly RecyclableMemoryStreamManager recyclableMemoryStreamManager = new ();
#endif

        private CosmosEncryptor? encryptor;

        private EncryptionOptions? encryptionOptions;
        private byte[]? encryptedData;
        private byte[]? plaintext;

        [Params(1, 10, 100)]
        public int DocumentSizeInKb { get; set; }

        [Params(CompressionOptions.CompressionAlgorithm.None, CompressionOptions.CompressionAlgorithm.Brotli)]
        public CompressionOptions.CompressionAlgorithm CompressionAlgorithm { get; set; }

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        [Params(JsonProcessor.Newtonsoft, JsonProcessor.Stream)]
#else
        [Params(JsonProcessor.Newtonsoft)]
#endif
        public JsonProcessor JsonProcessor { get; set; }

        [GlobalSetup]
        public async Task Setup()
        {
            StoreProvider
                .Setup(x => x.UnwrapKey(It.IsAny<string>(), It.IsAny<KeyEncryptionKeyAlgorithm>(), It.IsAny<byte[]>()))
                .Returns(DekData);

            Mock<DataEncryptionKeyProvider> keyProvider = new();
            keyProvider
                .Setup(x => x.FetchDataEncryptionKeyWithoutRawKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MdeEncryptionAlgorithm(DekProperties, EncryptionType.Randomized, StoreProvider.Object, cacheTimeToLive: TimeSpan.MaxValue));

            this.encryptor = new(keyProvider.Object);
            this.encryptionOptions = this.CreateEncryptionOptions();
            this.plaintext = this.LoadTestDoc();

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                 new MemoryStream(this.plaintext),
                 this.encryptor,
                 this.encryptionOptions,
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
                 new CosmosDiagnosticsContext(),
                 CancellationToken.None);
        }

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        [Benchmark]
        public async Task EncryptToProvidedStream()
        {
            using RecyclableMemoryStream rms = new (this.recyclableMemoryStreamManager);
            await EncryptionProcessor.EncryptAsync(
                new MemoryStream(this.plaintext!),
                rms,
                this.encryptor,
                this.encryptionOptions,
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
                this.JsonProcessor,
                CancellationToken.None);
        }

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        [Benchmark]
        public async Task DecryptToProvidedStream()
        {
            using RecyclableMemoryStream rms = new(this.recyclableMemoryStreamManager);
            await EncryptionProcessor.DecryptAsync(
                new MemoryStream(this.encryptedData!),
                rms,
                this.encryptor,
                new CosmosDiagnosticsContext(),
                this.JsonProcessor,
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
                CompressionOptions = new()
                {
                    Algorithm = this.CompressionAlgorithm
                },
                JsonProcessor = this.JsonProcessor,
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