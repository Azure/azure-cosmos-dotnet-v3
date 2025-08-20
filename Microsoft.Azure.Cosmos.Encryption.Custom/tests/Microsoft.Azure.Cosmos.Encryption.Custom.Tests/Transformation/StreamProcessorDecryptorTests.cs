//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using System.Buffers.Text;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Focused tests for StreamProcessor.DecryptStreamAsync logic (string/number/bool/null/object/array branches,
    /// buffer growth/leftOver logic, compression handling, skipping of _ei, invalid versions and algorithms etc).
    /// We intentionally mock only Encryptor + DataEncryptionKey and use real MdeEncryptor to avoid reflection.
    /// </summary>
    [TestClass]
    public class StreamProcessorDecryptorTests
    {
        private const string DekId = "dekId";
        private static Mock<Encryptor> mockEncryptor;
        private static Mock<DataEncryptionKey> mockDek;

        private static readonly JsonSerializerOptions SystemTextOptions = new()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            _ = ctx;
            StreamProcessor.InitialBufferSize = 8; // force multiple resizes / leftover path

            mockDek = new Mock<DataEncryptionKey>();
            mockDek.SetupGet(d => d.EncryptionAlgorithm).Returns(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);
            mockDek.Setup(d => d.GetEncryptByteCount(It.IsAny<int>())).Returns<int>(i => i);
            mockDek.Setup(d => d.GetDecryptByteCount(It.IsAny<int>())).Returns<int>(i => i);
            mockDek.Setup(d => d.EncryptData(It.IsAny<byte[]>())).Returns<byte[]>(b => TestCommon.EncryptData(b));
            mockDek.Setup(d => d.EncryptData(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<int>()))
                .Returns((byte[] input, int offset, int length, byte[] output, int outputOffset) => TestCommon.EncryptData(input, offset, length, output, outputOffset));
            mockDek.Setup(d => d.DecryptData(It.IsAny<byte[]>())).Returns<byte[]>(b => TestCommon.DecryptData(b));
            mockDek.Setup(d => d.DecryptData(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<int>()))
                .Returns((byte[] input, int offset, int length, byte[] output, int outputOffset) => TestCommon.DecryptData(input, offset, length, output, outputOffset));

            mockEncryptor = new Mock<Encryptor>();
            mockEncryptor.Setup(e => e.GetEncryptionKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string dekId, string algo, CancellationToken t) => dekId == DekId ? mockDek.Object : throw new InvalidOperationException("DEK not found"));
            mockEncryptor.Setup(e => e.EncryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] plain, string dekId, string algo, CancellationToken t) => dekId == DekId ? TestCommon.EncryptData(plain) : throw new InvalidOperationException("DEK not found"));
            mockEncryptor.Setup(e => e.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] cipher, string dekId, string algo, CancellationToken t) => dekId == DekId ? TestCommon.DecryptData(cipher) : throw new InvalidOperationException("DEK not found"));
        }

        private static EncryptionOptions CreateOptions(IEnumerable<string> paths, CompressionOptions.CompressionAlgorithm algorithm = CompressionOptions.CompressionAlgorithm.None, CompressionLevel compressionLevel = CompressionLevel.NoCompression)
        {
            return new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                JsonProcessor = JsonProcessor.Stream,
                PathsToEncrypt = paths.ToList(),
                CompressionOptions = new CompressionOptions { Algorithm = algorithm, CompressionLevel = compressionLevel }
            };
        }

        private static async Task<(MemoryStream encrypted, EncryptionProperties properties)> EncryptRawAsync(object doc, EncryptionOptions options)
        {
            Stream input = TestCommon.ToStream(doc);
            MemoryStream encryptedStream = new();
            await EncryptionProcessor.EncryptAsync(input, encryptedStream, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
            encryptedStream.Position = 0;

            // get properties via System.Text.Json to assert later
            using JsonDocument jd = JsonDocument.Parse(encryptedStream, new JsonDocumentOptions { AllowTrailingCommas = true });
            JsonElement root = jd.RootElement;
            JsonElement ei = root.GetProperty(Constants.EncryptedInfo);
            EncryptionProperties props = JsonSerializer.Deserialize<EncryptionProperties>(ei.GetRawText(), SystemTextOptions);
            encryptedStream.Position = 0;
            return ((MemoryStream)encryptedStream, props);
        }

        [TestMethod]
        public async Task Decrypt_AllPrimitiveTypesAndContainers()
        {
            var doc = new
            {
                id = Guid.NewGuid().ToString(),
                SensitiveStr = "abc",
                SensitiveInt = 123,
                SensitiveBoolTrue = true,
                SensitiveBoolFalse = false,
                SensitiveNull = (string)null,
                SensitiveArr = new object[] { 1, 2, 3 },
                SensitiveObj = new { a = 5, b = "text" },
                NonSensitive = 999
            };
            var paths = new[] { "/SensitiveStr", "/SensitiveInt", "/SensitiveBoolTrue", "/SensitiveBoolFalse", "/SensitiveNull", "/SensitiveArr", "/SensitiveObj" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Act
            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);

            // Assert
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            JsonElement root = jd.RootElement;
            foreach (string p in paths)
            {
                string name = p.TrimStart('/');
                Assert.IsTrue(root.TryGetProperty(name, out JsonElement _));
                // Null values are not encrypted -> not present in decrypted paths list.
                if (p == "/SensitiveNull")
                {
                    Assert.IsFalse(ctx.DecryptionInfoList[0].PathsDecrypted.Contains(p));
                }
                else
                {
                    Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains(p));
                }
            }
        }

        [TestMethod]
        public async Task Decrypt_CompressedPayloads()
        {
            var doc = new
            {
                id = Guid.NewGuid().ToString(),
                LargeStr = new string('x', 400), // ensure above minimal compression (default 0 but large anyway)
                SmallStr = "s",
            };
            var paths = new[] { "/LargeStr", "/SmallStr" };
            EncryptionOptions options = CreateOptions(paths, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest);

            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            Assert.AreEqual(EncryptionFormatVersion.MdeWithCompression, props.EncryptionFormatVersion);
            Assert.IsNotNull(props.CompressedEncryptedPaths);
            Assert.IsTrue(props.CompressedEncryptedPaths.Count > 0); // at least LargeStr is compressed

            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            JsonElement root = jd.RootElement;

            foreach (string p in paths)
            {
                Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains(p));
                Assert.IsTrue(root.TryGetProperty(p.TrimStart('/'), out _));
            }
        }

        [TestMethod]
        public async Task Decrypt_Skips_EncryptionInfo_Block()
        {
            // Build a document that already has _ei. (Encryptor will append another one during encryption; we want to ensure decryptor skips only the encrypted one at top-level.)
            var doc = new { id = "1", _ei = new { ignore = true }, SensitiveStr = "abc" };
            var paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);

            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            JsonElement root = jd.RootElement;
            Assert.IsFalse(root.TryGetProperty(Constants.EncryptedInfo, out _));
            Assert.AreEqual("abc", root.GetProperty("SensitiveStr").GetString());
            Assert.AreEqual(1, ctx.DecryptionInfoList.Count);
        }

        [TestMethod]
        public async Task Decrypt_IgnoresUnknownPropertyTypesAndMaintainsJson()
        {
            var doc = new { id = "1", SensitiveStr = "abc", Regular = 5 };
            var paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            MemoryStream output = new();
            _ = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            JsonElement root = jd.RootElement;
            Assert.AreEqual(5, root.GetProperty("Regular").GetInt32());
        }

        [TestMethod]
        public async Task Decrypt_Throws_OnUnknownEncryptionFormatVersion()
        {
            var doc = new { id = "1", SensitiveStr = "abc" };
            var paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            // Force invalid version (not 3 or 4)
            EncryptionProperties invalid = new EncryptionProperties(999, props.EncryptionAlgorithm, props.DataEncryptionKeyId, null, props.EncryptedPaths, props.CompressionAlgorithm, props.CompressedEncryptedPaths);
            MemoryStream output = new();
            encrypted.Position = 0;
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() => new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, invalid, new CosmosDiagnosticsContext(), CancellationToken.None));
        }

        [TestMethod]
        public async Task Decrypt_Throws_OnUnknownCompressionAlgorithm()
        {
            var doc = new { id = "1", SensitiveStr = new string('x', 400) }; // ensure compression triggers
            var paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            // simulate mismatch: mark algorithm INVALID but leave compressed paths
            EncryptionProperties invalid = new EncryptionProperties(props.EncryptionFormatVersion, props.EncryptionAlgorithm, props.DataEncryptionKeyId, null, props.EncryptedPaths, (CompressionOptions.CompressionAlgorithm)123, props.CompressedEncryptedPaths);
            MemoryStream output = new();
            encrypted.Position = 0;
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() => new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, invalid, new CosmosDiagnosticsContext(), CancellationToken.None));
        }

        [TestMethod]
        public async Task Decrypt_BufferGrowthAndPartialReads()
        {
            // Create a doc large enough to require multiple buffer fills.
            var doc = new { id = "1", SensitiveStr = new string('a', 200) };
            var paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
            Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/SensitiveStr"));
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.AreEqual(200, jd.RootElement.GetProperty("SensitiveStr").GetString().Length);
        }

        [TestMethod]
        public async Task Decrypt_HandlesNullValuesUnencrypted()
        {
            var doc = new { id = "1", SensitiveStr = (string)null };
            var paths = new[] { "/SensitiveStr" }; // null -> not encrypted (Encryptor writes null as-is)
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Remove the path from the EncryptedPaths list to mimic how encryption skips null but options might request it
            var adjustedPaths = props.EncryptedPaths.Where(p => p != "/SensitiveStr");
            EncryptionProperties adjustedProps = new EncryptionProperties(props.EncryptionFormatVersion, props.EncryptionAlgorithm, props.DataEncryptionKeyId, null, adjustedPaths, props.CompressionAlgorithm, props.CompressedEncryptedPaths);

            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, adjustedProps, new CosmosDiagnosticsContext(), CancellationToken.None);
            Assert.IsFalse(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/SensitiveStr"));
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.IsTrue(jd.RootElement.TryGetProperty("SensitiveStr", out JsonElement prop));
            Assert.AreEqual(JsonValueKind.Null, prop.ValueKind);
        }

        [TestMethod]
        public async Task Decrypt_Throws_OnBase64DecodeFailure()
        {
            // Arrange: create a simple encrypted document
            var doc = new { id = "1", SensitiveStr = "abc" };
            var paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Corrupt the base64 for SensitiveStr (introduce an illegal base64 character '#')
            string jsonText = Encoding.UTF8.GetString(encrypted.ToArray());
            using JsonDocument jd = JsonDocument.Parse(jsonText);
            string originalCipher = jd.RootElement.GetProperty("SensitiveStr").GetString();
            Assert.IsNotNull(originalCipher);
            // Replace first character with '#', which is not valid in Base64 alphabet
            string corruptedCipher = "#" + originalCipher.Substring(1);
            string originalFragment = "\"SensitiveStr\":\"" + originalCipher + "\"";
            string corruptedFragment = "\"SensitiveStr\":\"" + corruptedCipher + "\"";
            Assert.IsTrue(jsonText.Contains(originalFragment), "Original cipher fragment not found in JSON.");
            jsonText = jsonText.Replace(originalFragment, corruptedFragment);
            MemoryStream corruptedStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonText));

            // Act + Assert: Base64 decode failure should throw InvalidOperationException
            MemoryStream output = new();
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => new StreamProcessor().DecryptStreamAsync(corruptedStream, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None));
        }

        [TestMethod]
        public async Task Decrypt_Throws_OnCorruptedCompressedPayload()
        {
            // Arrange: create a document with compression enabled
            var doc = new { id = "1", LargeStr = new string('x', 400) };
            var paths = new[] { "/LargeStr" };
            EncryptionOptions options = CreateOptions(paths, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            Assert.IsTrue(props.CompressedEncryptedPaths.ContainsKey("/LargeStr"), "Payload not compressed as expected");

            string jsonText = Encoding.UTF8.GetString(encrypted.ToArray());
            using JsonDocument jd = JsonDocument.Parse(jsonText);
            string cipher = jd.RootElement.GetProperty("LargeStr").GetString();
            Assert.IsNotNull(cipher);
            byte[] cipherBytes = Convert.FromBase64String(cipher);
            Assert.IsTrue(cipherBytes.Length > 20);
            // Flip a middle byte (avoid first byte which is type marker to keep code path the same)
            int corruptIndex = cipherBytes.Length / 2;
            cipherBytes[corruptIndex] ^= 0xFF;
            string corruptedCipher = Convert.ToBase64String(cipherBytes);
            string originalFragment = "\"LargeStr\":\"" + cipher + "\"";
            string corruptedFragment = "\"LargeStr\":\"" + corruptedCipher + "\"";
            jsonText = jsonText.Replace(originalFragment, corruptedFragment);
            MemoryStream corruptedStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonText));

            // Act + Assert: decompression failure should surface as InvalidOperationException (error originates in BrotliCompressor)
            MemoryStream output = new();
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => new StreamProcessor().DecryptStreamAsync(corruptedStream, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None));
        }

    }
}
#endif
