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

        private TestDoc? testDoc;
        private CosmosEncryptor? encryptor;
        private Custom.DataEncryptionKey? dek;

        private EncryptionOptions? encryptionOptions;
        private byte[]? encryptedData;
        private byte[]? plaintext;

        [Params(1, 10, 100)]
        public int DocumentSizeInKb { get; set; }

        [GlobalSetup]
        public async Task Setup()
        {
            StoreProvider
                .Setup(x => x.UnwrapKey(It.IsAny<string>(), It.IsAny<KeyEncryptionKeyAlgorithm>(), It.IsAny<byte[]>()))
                .Returns(DekData);

            this.dek = this.CreateMdeDek();

            Mock<DataEncryptionKeyProvider> keyProvider = new();
            keyProvider
                .Setup(x => x.FetchDataEncryptionKeyWithoutRawKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => this.dek);

            this.encryptor = new(keyProvider.Object);
            this.testDoc = TestDoc.Create(approximateSize: this.DocumentSizeInKb * 1024);

            this.encryptionOptions = CreateEncryptionOptions();

            this.plaintext = EncryptionProcessor.BaseSerializer.ToStream(this.testDoc).ToArray();

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                 new MemoryStream(this.plaintext),
                 this.encryptor,
                 this.encryptionOptions,
                 new CosmosDiagnosticsContext(),
                 CancellationToken.None);

            using MemoryStream memoryStream = new MemoryStream();
            
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
                CancellationToken.None);
        }

        private Custom.DataEncryptionKey CreateMdeDek()
        {
            return new MdeEncryptionAlgorithm(DekProperties, EncryptionType.Deterministic, StoreProvider.Object, cacheTimeToLive: TimeSpan.MaxValue);
        }

        private static EncryptionOptions CreateEncryptionOptions()
        {
            EncryptionOptions options = new()
            {
                DataEncryptionKeyId = "dekId",
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt
            };

            return options;
        }
    }
}