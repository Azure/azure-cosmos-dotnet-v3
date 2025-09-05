#if ENCRYPTION_CUSTOM_PREVIEW
using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Azure.Cosmos.Encryption.Custom;
using Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests;
using Microsoft.Data.Encryption.Cryptography;
using Moq;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Performance.StreamBenchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 1, iterationCount: 5)]
    public class StreamProcessorPathMatchBenchmark
    {
        private static readonly byte[] DekData = Enumerable.Repeat((byte)0, 32).ToArray();
        private static readonly DataEncryptionKeyProperties DekProperties = new(
            "id",
            CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
            DekData,
            new EncryptionKeyWrapMetadata("name", "value"),
            DateTime.UtcNow);

        private CosmosEncryptor? encryptor;
        private EncryptionOptions? options;
        private byte[]? plaintext;
        private byte[]? ciphertext;

        [Params(50, 200)]
        public int TopLevelProperties { get; set; }

        [Params(0, 1, 5, 10)]
        public int EncryptedTopLevelCount { get; set; }

        [GlobalSetup]
        public async Task Setup()
        {
            // Key provider returns MDE algorithm
            Mock<EncryptionKeyStoreProvider> storeProvider = new();
            storeProvider
                .Setup(x => x.UnwrapKey(It.IsAny<string>(), It.IsAny<KeyEncryptionKeyAlgorithm>(), It.IsAny<byte[]>()))
                .Returns(DekData);

            Mock<DataEncryptionKeyProvider> keyProvider = new();
            keyProvider
                .Setup(x => x.FetchDataEncryptionKeyWithoutRawKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MdeEncryptionAlgorithm(DekProperties, Microsoft.Data.Encryption.Cryptography.EncryptionType.Randomized, storeProvider.Object, cacheTimeToLive: TimeSpan.MaxValue));

            this.encryptor = new(keyProvider.Object);

            // Generate a JSON document with many top-level properties
            this.plaintext = GenerateDocument(this.TopLevelProperties);

            // Select first N properties to encrypt: /P1, /P2, ...
            HashSet<string> paths = new();
            for (int i = 1; i <= Math.Min(this.TopLevelProperties, this.EncryptedTopLevelCount); i++)
            {
                paths.Add($"/P{i}");
            }

            this.options = new EncryptionOptions
            {
                DataEncryptionKeyId = "dekId",
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                JsonProcessor = JsonProcessor.Stream,
                PathsToEncrypt = paths,
            };

            // Pre-encrypt once for decrypt benchmark
            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                new MemoryStream(this.plaintext),
                this.encryptor,
                this.options,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            using MemoryStream ms = new();
            await encryptedStream.CopyToAsync(ms);
            this.ciphertext = ms.ToArray();
        }

        [Benchmark(Description = "Encrypt(Stream)")]
        public async Task Encrypt()
        {
            await EncryptionProcessor.EncryptAsync(
                new MemoryStream(this.plaintext!),
                this.encryptor!,
                this.options!,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);
        }

        [Benchmark(Description = "Decrypt(Stream)")]
        public async Task Decrypt()
        {
            await EncryptionProcessor.DecryptAsync(
                new MemoryStream(this.ciphertext!),
                this.encryptor!,
                new CosmosDiagnosticsContext(),
                JsonProcessor.Stream,
                CancellationToken.None);
        }

        private static byte[] GenerateDocument(int props)
        {
            // { "P1": "v...", "P2": 123, ... } repeated simple values to focus on property name matching cost
            // Build with StringBuilder then UTF8 encode
            StringBuilder sb = new();
            sb.Append('{');
            for (int i = 1; i <= props; i++)
            {
                if (i > 1) sb.Append(',');
                sb.Append('\"').Append("P").Append(i).Append('\"').Append(':');
                if ((i % 3) == 0)
                {
                    sb.Append(i); // number
                }
                else
                {
                    sb.Append('\"').Append("value").Append(i).Append('\"'); // short string
                }
            }
            sb.Append('}');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}
#endif
