namespace Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests
{
    using System.IO;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Data.Encryption.Cryptography;
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

        private CosmosEncryptor? encryptor;

        private EncryptionOptions? encryptionOptions;
        private byte[]? encryptedData;
        private byte[]? plaintext;

        [Params(1, 10, 100)]
        public int DocumentSizeInKb { get; set; }

        [Params(JsonProcessor.Newtonsoft, JsonProcessor.SystemTextJson)]
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

        private EncryptionOptions CreateEncryptionOptions()
        {
            EncryptionOptions options = new()
            {
                DataEncryptionKeyId = "dekId",
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt,
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