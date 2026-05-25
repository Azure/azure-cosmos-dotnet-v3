//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Moq;

    /// <summary>
    /// Measures the allocation profile of the new <see cref="LegacyAlgorithmDetector"/>-based
    /// Stream-opt-in legacy detection path against the try/catch baseline introduced in PR #5902.
    ///
    /// Two scenarios per algorithm so we can see both the hot-path (MDE) overhead and the cold-path
    /// (legacy) savings of swapping exception-driven control flow for a focused Utf8JsonReader scan:
    /// <list type="bullet">
    /// <item><c>Legacy_*</c>: the document is AE-AES legacy. PR #5902's baseline calls into
    /// <see cref="MdeEncryptionProcessor.DecryptAsync"/>, which fully deserializes the MDE wrapper
    /// (including base64-decoding the entire ciphertext blob) before throwing
    /// <see cref="NotSupportedException"/>; only then does it rewind and run the JObject path.
    /// The new detector skips that wasted parse + decode entirely.</item>
    /// <item><c>Mde_*</c>: the document is MDE. The detector adds a tiny up-front
    /// <see cref="System.Text.Json.Utf8JsonReader"/> peek before forwarding to the same
    /// MDE processor. We expect a small but non-negative cost here, which the legacy
    /// savings should dwarf in any mixed workload.</item>
    /// </list>
    /// </summary>
    [MemoryDiagnoser]
    public class LegacyDetectionBenchmark
    {
        private const string DekId = "dek-bench";

        private static readonly MdeEncryptionProcessor MdeProcessor = new();

        private Mock<Encryptor> mockEncryptor = null!;
        private byte[] mdeEncryptedBytes = null!;
        private byte[] legacyEncryptedBytes = null!;
        private ItemRequestOptions streamOptIn = null!;

        [Params(1, 10, 100)]
        public int DocumentSizeInKb { get; set; }

        [GlobalSetup]
        public async Task Setup()
        {
            this.mockEncryptor = CreateMockEncryptor();
            this.streamOptIn = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream },
                },
            };

            EncryptionBenchmark.TestDoc doc = EncryptionBenchmark.TestDoc.Create(this.DocumentSizeInKb * 1024);
            byte[] plaintext = SerializeDoc(doc);

            this.mdeEncryptedBytes = await EncryptToBytesAsync(plaintext, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);
#pragma warning disable CS0618 // legacy algorithm intentionally used to produce a legacy-shaped payload
            this.legacyEncryptedBytes = await EncryptToBytesAsync(plaintext, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized);
#pragma warning restore CS0618
        }

        [Benchmark(Description = "Legacy doc: new Utf8JsonReader detector (this PR)")]
        public async Task<int> Legacy_NewDetector()
        {
            (Stream output, _) = await EncryptionProcessor.DecryptAsync(
                new MemoryStream(this.legacyEncryptedBytes),
                this.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                this.streamOptIn,
                CancellationToken.None);
            await output.DisposeAsync();
            return 0;
        }

        [Benchmark(Baseline = true, Description = "Legacy doc: PR #5902 try { MDE } catch (NSE) baseline")]
        public async Task<int> Legacy_TryCatchBaseline()
        {
            (Stream output, _) = await DecryptViaTryCatchAsync(this.legacyEncryptedBytes);
            await output.DisposeAsync();
            return 0;
        }

        [Benchmark(Description = "MDE doc: new Utf8JsonReader detector (this PR)")]
        public async Task<int> Mde_NewDetector()
        {
            (Stream output, _) = await EncryptionProcessor.DecryptAsync(
                new MemoryStream(this.mdeEncryptedBytes),
                this.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                this.streamOptIn,
                CancellationToken.None);
            await output.DisposeAsync();
            return 0;
        }

        [Benchmark(Description = "MDE doc: PR #5902 try { MDE } catch (NSE) baseline (no throw)")]
        public async Task<int> Mde_TryCatchBaseline()
        {
            (Stream output, _) = await DecryptViaTryCatchAsync(this.mdeEncryptedBytes);
            await output.DisposeAsync();
            return 0;
        }

        /// <summary>
        /// Mirrors PR #5902's exact dispatch: optimistically run the Stream-mode MDE processor and,
        /// on <see cref="NotSupportedException"/>, rewind and re-run via the Newtonsoft JObject path.
        /// </summary>
        private async Task<(Stream, DecryptionContext)> DecryptViaTryCatchAsync(byte[] payload)
        {
            MemoryStream input = new(payload);
            try
            {
                return await MdeProcessor.DecryptAsync(input, this.mockEncryptor.Object, new CosmosDiagnosticsContext(), this.streamOptIn, CancellationToken.None);
            }
            catch (NotSupportedException)
            {
                input.Position = 0;
                return await EncryptionProcessor.DecryptAsync(input, this.mockEncryptor.Object, new CosmosDiagnosticsContext(), requestOptions: null, CancellationToken.None);
            }
        }

        private async Task<byte[]> EncryptToBytesAsync(byte[] plaintext, string algorithm)
        {
            EncryptionOptions opts = new()
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = algorithm,
                PathsToEncrypt = new List<string>(EncryptionBenchmark.TestDoc.PathsToEncrypt),
            };

            Stream encrypted = await EncryptionProcessor.EncryptAsync(
                new MemoryStream(plaintext),
                this.mockEncryptor.Object,
                opts,
                JsonProcessor.Newtonsoft,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            using MemoryStream ms = new();
            encrypted.Position = 0;
            await encrypted.CopyToAsync(ms);
            return ms.ToArray();
        }

        private static byte[] SerializeDoc(EncryptionBenchmark.TestDoc doc)
        {
            using MemoryStream ms = new();
            using (StreamWriter sw = new(ms, leaveOpen: true))
            {
                Newtonsoft.Json.JsonSerializer.Create().Serialize(sw, doc);
            }
            return ms.ToArray();
        }

        /// <summary>
        /// Identity encrypt/decrypt mock (byte+1 / byte-1) that satisfies both the MDE and legacy
        /// surfaces; matches the contract of <c>TestEncryptorFactory.CreateMde</c> in the test
        /// project but is inlined here so the perf project stays self-contained.
        /// </summary>
        private static Mock<Encryptor> CreateMockEncryptor()
        {
            Mock<DataEncryptionKey> dek = new();
            dek.SetupGet(d => d.EncryptionAlgorithm).Returns(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);
            dek.Setup(d => d.GetEncryptByteCount(It.IsAny<int>())).Returns<int>(i => i);
            dek.Setup(d => d.GetDecryptByteCount(It.IsAny<int>())).Returns<int>(i => i);
            dek.Setup(d => d.EncryptData(It.IsAny<byte[]>())).Returns<byte[]>(b => Plus1(b));
            dek.Setup(d => d.EncryptData(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<int>()))
                .Returns((byte[] inp, int off, int len, byte[] outp, int outOff) => Plus1Into(inp, off, len, outp, outOff));
            dek.Setup(d => d.DecryptData(It.IsAny<byte[]>())).Returns<byte[]>(b => Minus1(b));
            dek.Setup(d => d.DecryptData(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<int>()))
                .Returns((byte[] inp, int off, int len, byte[] outp, int outOff) => Minus1Into(inp, off, len, outp, outOff));

            Mock<Encryptor> encryptor = new();
            encryptor.Setup(e => e.GetEncryptionKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, string algo, CancellationToken _) => id == DekId ? dek.Object : throw new InvalidOperationException("DEK not found"));
            encryptor.Setup(e => e.EncryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] plain, string id, string _, CancellationToken __) => id == DekId ? Plus1(plain) : throw new InvalidOperationException("DEK not found"));
            encryptor.Setup(e => e.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] cipher, string id, string _, CancellationToken __) => id == DekId ? Minus1(cipher) : throw new InvalidOperationException("DEK not found"));
            return encryptor;
        }

        private static byte[] Plus1(byte[] input)
        {
            byte[] outp = new byte[input.Length];
            for (int i = 0; i < input.Length; i++) { outp[i] = (byte)(input[i] + 1); }
            return outp;
        }

        private static int Plus1Into(byte[] input, int offset, int length, byte[] outp, int outOffset)
        {
            for (int i = 0; i < length; i++) { outp[outOffset + i] = (byte)(input[offset + i] + 1); }
            return length;
        }

        private static byte[] Minus1(byte[] input)
        {
            byte[] outp = new byte[input.Length];
            for (int i = 0; i < input.Length; i++) { outp[i] = (byte)(input[i] - 1); }
            return outp;
        }

        private static int Minus1Into(byte[] input, int offset, int length, byte[] outp, int outOffset)
        {
            for (int i = 0; i < length; i++) { outp[outOffset + i] = (byte)(input[offset + i] - 1); }
            return length;
        }
    }
}
#endif
