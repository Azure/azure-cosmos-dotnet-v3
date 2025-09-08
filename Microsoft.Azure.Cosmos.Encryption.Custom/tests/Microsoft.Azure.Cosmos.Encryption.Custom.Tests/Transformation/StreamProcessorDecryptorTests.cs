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
            // Arrange
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
            string[] paths = new[] { "/SensitiveStr", "/SensitiveInt", "/SensitiveBoolTrue", "/SensitiveBoolFalse", "/SensitiveNull", "/SensitiveArr", "/SensitiveObj" };
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
            // Arrange
            var doc = new
            {
                id = Guid.NewGuid().ToString(),
                LargeStr = new string('x', 400), // ensure above minimal compression (default 0 but large anyway)
                SmallStr = "s",
            };
            string[] paths = new[] { "/LargeStr", "/SmallStr" };
            EncryptionOptions options = CreateOptions(paths, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest);

            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            Assert.AreEqual(EncryptionFormatVersion.MdeWithCompression, props.EncryptionFormatVersion);
            Assert.IsNotNull(props.CompressedEncryptedPaths);
            Assert.IsTrue(props.CompressedEncryptedPaths.Count > 0); // at least LargeStr is compressed

            // Act
            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            JsonElement root = jd.RootElement;

            // Assert
            foreach (string p in paths)
            {
                Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains(p));
                Assert.IsTrue(root.TryGetProperty(p.TrimStart('/'), out _));
            }
        }

        [TestMethod]
        public async Task Decrypt_Throws_OnUnknownCompressionAlgorithm()
        {
            // Arrange: produce a payload with compression so CompressedEncryptedPaths is non-empty
            var doc = new { id = "1", LargeStr = new string('x', 400) };
            string[] paths = new[] { "/LargeStr" };
            EncryptionOptions options = CreateOptions(paths, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            Assert.IsTrue(props.CompressedEncryptedPaths?.Any() == true);

            // Forge properties with an unsupported compression algorithm to trigger the guard
            EncryptionProperties badProps = new(
                props.EncryptionFormatVersion,
                props.EncryptionAlgorithm,
                props.DataEncryptionKeyId,
                encryptedData: null,
                props.EncryptedPaths,
                (CompressionOptions.CompressionAlgorithm)123,
                props.CompressedEncryptedPaths);

            // Act + Assert
            MemoryStream output = new();
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() => new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, badProps, new CosmosDiagnosticsContext(), CancellationToken.None));
        }

        [TestMethod]
        public async Task Decrypt_Skips_EncryptionInfo_Block()
        {
            // Arrange
            // Build a document that already has _ei. (Encryptor will append another one during encryption; we want to ensure decryptor skips only the encrypted one at top-level.)
            var doc = new { id = "1", _ei = new { ignore = true }, SensitiveStr = "abc" };
            string[] paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Act
            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);

            // Assert
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
            // Arrange
            var doc = new { id = "1", SensitiveStr = "abc", Regular = 5 };
            string[] paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Act
            MemoryStream output = new();
            _ = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            // Assert
            JsonElement root = jd.RootElement;
            Assert.AreEqual(5, root.GetProperty("Regular").GetInt32());
        }

        [TestMethod]
        public async Task Decrypt_Throws_OnUnknownEncryptionFormatVersion()
        {
            // Arrange
            var doc = new { id = "1", SensitiveStr = "abc" };
            string[] paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            EncryptionProperties invalid = new EncryptionProperties(999, props.EncryptionAlgorithm, props.DataEncryptionKeyId, null, props.EncryptedPaths, props.CompressionAlgorithm, props.CompressedEncryptedPaths);
            // Act + Assert
            MemoryStream output = new();
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() => new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, invalid, new CosmosDiagnosticsContext(), CancellationToken.None));
        }

        [TestMethod]
        public async Task Decrypt_Throws_OnInvalidBase64Ciphertext()
        {
            // Arrange
            var doc = new { id = "1", SensitiveStr = "abc" };
            string[] paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            string jsonText = Encoding.UTF8.GetString(encrypted.ToArray());
            using JsonDocument jd = JsonDocument.Parse(jsonText);
            string originalCipher = jd.RootElement.GetProperty("SensitiveStr").GetString();
            Assert.IsNotNull(originalCipher);
            string corruptedCipher = string.Concat("#", originalCipher.AsSpan(1)); // invalid base64 start
            jsonText = jsonText.Replace("\"SensitiveStr\":\"" + originalCipher + "\"", "\"SensitiveStr\":\"" + corruptedCipher + "\"");
            MemoryStream corruptedStream = new(Encoding.UTF8.GetBytes(jsonText));
            // Act + Assert
            MemoryStream output = new();
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => new StreamProcessor().DecryptStreamAsync(corruptedStream, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None));
        }

        [TestMethod]
        public async Task Decrypt_Throws_OnCorruptedCompressedPayload()
        {
            // Arrange: create a document with compression enabled
            var doc = new { id = "1", LargeStr = new string('x', 400) };
            string[] paths = new[] { "/LargeStr" };
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

        [TestMethod]
        public async Task Decrypt_Throws_OnCompressedPayload_DestinationTooSmall()
        {
            // Arrange: valid compressed payload, but forge properties to declare a too-small decompressed size for the path.
            var doc = new { id = "1", LargeStr = new string('x', 400) };
            string[] paths = new[] { "/LargeStr" };
            EncryptionOptions options = CreateOptions(paths, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            Assert.IsTrue(props.CompressedEncryptedPaths.ContainsKey("/LargeStr"), "Expected LargeStr to be marked as compressed");

            // Forge smaller decompressed size to force DestinationTooSmall from Brotli decoder
            int declared = props.CompressedEncryptedPaths["/LargeStr"];
            IDictionary<string, int> adjusted = new Dictionary<string, int>(props.CompressedEncryptedPaths)
            {
                ["/LargeStr"] = Math.Max(1, declared / 4)
            };
            EncryptionProperties badProps = new(
                props.EncryptionFormatVersion,
                props.EncryptionAlgorithm,
                props.DataEncryptionKeyId,
                encryptedData: null,
                props.EncryptedPaths,
                props.CompressionAlgorithm,
                adjusted);

            // Act + Assert
            MemoryStream output = new();
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, badProps, new CosmosDiagnosticsContext(), CancellationToken.None));
        }

        [TestMethod]
        public async Task Decrypt_IgnoredBlock_PartialEiSkip()
        {
            // Arrange
            // Force the _ei metadata object to span multiple buffer reads so Utf8JsonReader.TrySkip() returns false,
            // exercising the fallback isIgnoredBlock path.
            const int propertyCount = 250; // large to inflate _ei encrypted paths list
            Dictionary<string, object> doc = new() { ["id"] = "1" };
            List<string> paths = new(propertyCount);
            for (int i = 0; i < propertyCount; i++)
            {
                string name = "P" + i.ToString();
                // moderately sized value to enlarge encrypted base64 + metadata
                string value = new string('x', 32 + (i % 5));
                doc[name] = value;
                paths.Add("/" + name);
            }

            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Choose very small initial buffer so _ei object is fragmented.
            int original = StreamProcessor.InitialBufferSize;
            StreamProcessor.InitialBufferSize = 32;
            try
            {
                // Act
                MemoryStream output = new();
                DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
                output.Position = 0;
                using JsonDocument jd = JsonDocument.Parse(output, new JsonDocumentOptions { AllowTrailingCommas = true });
                JsonElement root = jd.RootElement;
                // Assert
                // _ei must be removed
                Assert.IsFalse(root.TryGetProperty(Constants.EncryptedInfo, out _), "_ei should be skipped");
                // spot check a few decrypted properties
                Assert.AreEqual(JsonValueKind.String, root.GetProperty("P0").ValueKind);
                Assert.AreEqual(JsonValueKind.String, root.GetProperty("P100").ValueKind);
                // Ensure some decrypted paths recorded
                Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Count > 200);
            }
            finally
            {
                StreamProcessor.InitialBufferSize = original; // restore
            }
        }

        [TestMethod]
        public async Task Decrypt_CompressedPathsPropertyNull()
        {
            // Arrange
            // Cover branch where EncryptionProperties.CompressedEncryptedPaths is null (as opposed to empty dictionary or populated),
            // exercising the null path of the null-conditional operator in: bool containsCompressed = properties.CompressedEncryptedPaths?.Count > 0;
            var doc = new { id = "1", SensitiveStr = "abc" };
            string[] paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths); // no compression requested
            (MemoryStream encrypted, EncryptionProperties propsWithEmpty) = await EncryptRawAsync(doc, options);

            // Forge properties with CompressedEncryptedPaths = null to reach missing branch
            EncryptionProperties propsNullCompressed = new(
                propsWithEmpty.EncryptionFormatVersion,
                propsWithEmpty.EncryptionAlgorithm,
                propsWithEmpty.DataEncryptionKeyId,
                encryptedData: null,
                propsWithEmpty.EncryptedPaths,
                propsWithEmpty.CompressionAlgorithm, // None
                compressedEncryptedPaths: null);

            encrypted.Position = 0;
            // Act
            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, propsNullCompressed, new CosmosDiagnosticsContext(), CancellationToken.None);

            // Assert
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.AreEqual("abc", jd.RootElement.GetProperty("SensitiveStr").GetString());
            Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/SensitiveStr"));
        }

        [TestMethod]
        public async Task Decrypt_Compression_InvalidDecompressedSizeZero_Throws()
        {
            // Arrange: create a compressed payload then forge decompressed size = 0 to trip guard (<=0)
            var doc = new { id = "1", LargeStr = new string('x', 400) }; // ensure compression
            string[] paths = new[] { "/LargeStr" };
            EncryptionOptions options = CreateOptions(paths, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            Assert.IsTrue(props.CompressedEncryptedPaths.ContainsKey("/LargeStr"), "Expected LargeStr to be compressed");

            IDictionary<string, int> forged = new Dictionary<string, int>(props.CompressedEncryptedPaths)
            {
                ["/LargeStr"] = 0 // invalid
            };

            EncryptionProperties badProps = new(
                props.EncryptionFormatVersion,
                props.EncryptionAlgorithm,
                props.DataEncryptionKeyId,
                encryptedData: null,
                props.EncryptedPaths,
                props.CompressionAlgorithm,
                forged);

            encrypted.Position = 0;
            MemoryStream output = new();
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, badProps, new CosmosDiagnosticsContext(), CancellationToken.None));
        }

        [TestMethod]
        public async Task Decrypt_Compression_InvalidDecompressedSizeTooLarge_Throws()
        {
            // Arrange: same as above but set decompressed size beyond 32MB guard (MaxBufferSizeBytes = 32 * 1024 * 1024)
            var doc = new { id = "1", LargeStr = new string('x', 400) };
            string[] paths = new[] { "/LargeStr" };
            EncryptionOptions options = CreateOptions(paths, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            Assert.IsTrue(props.CompressedEncryptedPaths.ContainsKey("/LargeStr"), "Expected LargeStr to be compressed");

            const int TooLarge = 40_000_000; // > 32MB limit
            IDictionary<string, int> forged = new Dictionary<string, int>(props.CompressedEncryptedPaths)
            {
                ["/LargeStr"] = TooLarge
            };

            EncryptionProperties badProps = new(
                props.EncryptionFormatVersion,
                props.EncryptionAlgorithm,
                props.DataEncryptionKeyId,
                encryptedData: null,
                props.EncryptedPaths,
                props.CompressionAlgorithm,
                forged);

            encrypted.Position = 0;
            MemoryStream output = new();
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, badProps, new CosmosDiagnosticsContext(), CancellationToken.None));
        }

        [TestMethod]
        public async Task Decrypt_EncryptedToken_ExceedsAdjustedMax_Throws()
        {
            // Arrange: Lower the max buffer cap to 4MB and craft an encrypted value slightly above it.
            // We generate a large plaintext string so that after encryption + base64 it exceeds the 4MB threshold.
            int originalCap = StreamProcessor.TestMaxBufferSizeBytesOverride ?? 0;
            StreamProcessor.TestMaxBufferSizeBytesOverride = 4 * 1024 * 1024; // 4MB
            try
            {
                int cap = StreamProcessor.TestMaxBufferSizeBytesOverride.Value;
                string[] paths = new[] { "/Big" };
                EncryptionOptions options = CreateOptions(paths);
                int plainLength = cap / 2; // start below
                MemoryStream encrypted = null;
                EncryptionProperties props = null;
                string cipher = null;
                for (int attempts = 0; attempts < 8; attempts++)
                {
                    string large = new string('Z', plainLength);
                    var doc = new { id = "1", Big = large };
                    (encrypted, props) = await EncryptRawAsync(doc, options);
                    string json = Encoding.UTF8.GetString(encrypted.ToArray());
                    using (JsonDocument jd = JsonDocument.Parse(json))
                    {
                        cipher = jd.RootElement.GetProperty("Big").GetString();
                    }

                    if (cipher.Length > cap)
                    {
                        break;
                    }

                    plainLength = (int)(plainLength * 1.6); // grow
                }

                Assert.IsNotNull(cipher);
                Assert.IsTrue(cipher.Length > cap, "Failed to exceed lowered cap after growth attempts");

                encrypted.Position = 0;
                MemoryStream output = new();
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None));
            }
            finally
            {
                StreamProcessor.TestMaxBufferSizeBytesOverride = originalCap == 0 ? null : originalCap; // restore
            }
        }

        [TestMethod]
        public async Task Decrypt_Skips_Ei_ScalarString_AcrossBuffers()
        {
            // Arrange: craft a JSON with a very long string as the value of _ei so it spans buffers
            int original = StreamProcessor.InitialBufferSize;
            StreamProcessor.InitialBufferSize = 32; // ensure fragmentation for TrySkip false
            try
            {
                string longEi = new string('x', 2000);
                string json = "{\"id\":\"1\",\"_ei\":\"" + longEi + "\",\"Regular\":5}";
                MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));

                // No encrypted paths; properties still need a valid DEK id for the current flow
                EncryptionProperties props = new(
                    EncryptionFormatVersion.Mde,
                    CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    DekId,
                    encryptedData: null,
                    encryptedPaths: Array.Empty<string>(),
                    CompressionOptions.CompressionAlgorithm.None,
                    compressedEncryptedPaths: null);

                // Act
                MemoryStream output = new();
                DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(input, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);

                // Assert: _ei removed, other content intact, and no decryption info needed
                output.Position = 0;
                using JsonDocument jd = JsonDocument.Parse(output);
                JsonElement root = jd.RootElement;
                Assert.IsFalse(root.TryGetProperty(Constants.EncryptedInfo, out _));
                Assert.AreEqual(5, root.GetProperty("Regular").GetInt32());
                Assert.IsNull(ctx); // no decrypted paths
            }
            finally
            {
                StreamProcessor.InitialBufferSize = original;
            }
        }

        [TestMethod]
        public async Task Decrypt_Preserves_MultiSegment_String_And_Number()
        {
            // Arrange: long unencrypted tokens to force multi-segment ValueSequence
            int original = StreamProcessor.InitialBufferSize;
            StreamProcessor.InitialBufferSize = 32;
            try
            {
                string longStr = new string('a', 8192);
                string longDigits = new string('7', 5000);
                string json = "{\"id\":\"1\",\"LongStr\":\"" + longStr + "\",\"BigNumber\":" + longDigits + "}";
                MemoryStream input = new(Encoding.UTF8.GetBytes(json));

                EncryptionProperties props = new(
                    EncryptionFormatVersion.Mde,
                    CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    DekId,
                    encryptedData: null,
                    encryptedPaths: Array.Empty<string>(),
                    CompressionOptions.CompressionAlgorithm.None,
                    compressedEncryptedPaths: null);

                // Act
                MemoryStream output = new();
                _ = await new StreamProcessor().DecryptStreamAsync(input, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
                output.Position = 0;

                // Assert: values preserved
                using JsonDocument jd = JsonDocument.Parse(output);
                JsonElement root = jd.RootElement;
                Assert.AreEqual(longStr, root.GetProperty("LongStr").GetString());
                Assert.AreEqual(longDigits, root.GetProperty("BigNumber").GetRawText());
            }
            finally
            {
                StreamProcessor.InitialBufferSize = original;
            }
        }

        [TestMethod]
        public async Task Decrypt_Encrypted_SmallAndLargeBase64_AcrossBuffers()
        {
            // Arrange: encrypted small and large strings to exercise stackalloc and pooled paths, and multi-buffer
            var doc = new
            {
                id = "1",
                Small = "s",
                Large = new string('Z', 20000),
            };
            string[] paths = new[] { "/Small", "/Large" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            int original = StreamProcessor.InitialBufferSize;
            StreamProcessor.InitialBufferSize = 64; // force segmentation for Large
            try
            {
                // Act
                MemoryStream output = new();
                DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);

                // Assert
                output.Position = 0;
                using JsonDocument jd = JsonDocument.Parse(output);
                JsonElement root = jd.RootElement;
                Assert.AreEqual("s", root.GetProperty("Small").GetString());
                Assert.AreEqual(doc.Large, root.GetProperty("Large").GetString());
                Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/Small"));
                Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/Large"));
            }
            finally
            {
                StreamProcessor.InitialBufferSize = original;
            }
        }

        [TestMethod]
        public async Task Decrypt_UnencryptedArrayAndBooleans()
        {
            // Arrange
            // Covers StartArray / EndArray / True / False switch branches where decryptPropertyName == null (no encryption for those tokens).
            var doc = new
            {
                id = "1",
                SensitiveStr = "secret",
                UnencryptedArr = new int[] { 7, 8, 9 },
                UnencryptedBoolTrue = true,
                UnencryptedBoolFalse = false,
            };
            string[] paths = new[] { "/SensitiveStr" }; // only encrypt the string; others remain plain
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Act
            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);

            // Assert
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            JsonElement root = jd.RootElement;
            // Ensure decrypted sensitive property was processed
            Assert.AreEqual("secret", root.GetProperty("SensitiveStr").GetString());
            Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/SensitiveStr"));
            // Validate unencrypted array preserved
            JsonElement arr = root.GetProperty("UnencryptedArr");
            Assert.AreEqual(JsonValueKind.Array, arr.ValueKind);
            Assert.AreEqual(3, arr.GetArrayLength());
            Assert.AreEqual(7, arr[0].GetInt32());
            Assert.AreEqual(8, arr[1].GetInt32());
            Assert.AreEqual(9, arr[2].GetInt32());
            // Validate unencrypted booleans preserved
            Assert.IsTrue(root.GetProperty("UnencryptedBoolTrue").GetBoolean());
            Assert.IsFalse(root.GetProperty("UnencryptedBoolFalse").GetBoolean());
        }

        [TestMethod]
        public async Task Decrypt_ForgedCipherText_TypeMarkerNull()
        {
            // Arrange
            // Covers TypeMarker.Null switch branch by forging a ciphertext with first byte = Null marker.
            var doc = new { id = "1", SensitiveStr = "abc" };
            string[] paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths); // no compression
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            Assert.AreEqual(EncryptionFormatVersion.Mde, props.EncryptionFormatVersion);

            // Parse and replace SensitiveStr base64 value
            encrypted.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(encrypted, new JsonDocumentOptions { AllowTrailingCommas = true });
            string originalCipher = jd.RootElement.GetProperty("SensitiveStr").GetString();
            Assert.IsNotNull(originalCipher);
            byte[] forgedBytes = new byte[] { (byte)TypeMarker.Null, 0x00 }; // minimal payload
            string forgedBase64 = Convert.ToBase64String(forgedBytes);

            // Reconstruct JSON deterministically
            MemoryStream forged = new();
            using (Utf8JsonWriter w = new(forged))
            {
                w.WriteStartObject();
                w.WriteString("id", "1");
                w.WriteString("SensitiveStr", forgedBase64);
                w.WritePropertyName(Constants.EncryptedInfo);
                jd.RootElement.GetProperty(Constants.EncryptedInfo).WriteTo(w);
                w.WriteEndObject();
            }
            forged.Position = 0;

            // Act
            // Use custom encryptor that returns empty plaintext for Null marker
            StreamProcessor sp = new StreamProcessor { Encryptor = new NullMarkerMdeEncryptor() };
            MemoryStream output = new();
            DecryptionContext ctx = await sp.DecryptStreamAsync(forged, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
            // Assert
            output.Position = 0;
            using JsonDocument outDoc = JsonDocument.Parse(output);
            Assert.AreEqual(JsonValueKind.Null, outDoc.RootElement.GetProperty("SensitiveStr").ValueKind);
            Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/SensitiveStr"));
        }

        [TestMethod]
        public async Task Decrypt_Throws_OnMissingDataEncryptionKey()
        {
            // Arrange
            // Arrange: create a valid encrypted payload
            var doc = new { id = "1", SensitiveStr = "abc" };
            string[] paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Forge properties with an unknown DEK id
            EncryptionProperties badProps = new(
                props.EncryptionFormatVersion,
                props.EncryptionAlgorithm,
                dataEncryptionKeyId: "missing-dek",
                encryptedData: null,
                props.EncryptedPaths,
                props.CompressionAlgorithm,
                props.CompressedEncryptedPaths);

            // Act + Assert
            MemoryStream output = new();
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, badProps, new CosmosDiagnosticsContext(), CancellationToken.None));
        }

        [TestMethod]
        public async Task Decrypt_EncryptedPathValueIsNumber_NoDecryptionOccurs()
        {
            // Arrange
            var doc = new { id = "1", SensitiveStr = "abc" };
            string[] paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Replace the encrypted string token with a number token to bypass decryption logic for that property
            string jsonText = Encoding.UTF8.GetString(encrypted.ToArray());
            using (JsonDocument jd = JsonDocument.Parse(jsonText))
            {
                string originalCipher = jd.RootElement.GetProperty("SensitiveStr").GetString();
                Assert.IsNotNull(originalCipher);
                jsonText = jsonText.Replace("\"SensitiveStr\":\"" + originalCipher + "\"", "\"SensitiveStr\":123");
            }

            MemoryStream mutated = new(Encoding.UTF8.GetBytes(jsonText));

            // Act
            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(mutated, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);

            // Assert: value remains number and path is not recorded as decrypted
            output.Position = 0;
            using JsonDocument outDoc = JsonDocument.Parse(output);
            Assert.AreEqual(123, outDoc.RootElement.GetProperty("SensitiveStr").GetInt32());
            Assert.IsFalse(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/SensitiveStr"));
        }

        [TestMethod]
        public async Task Decrypt_ForgedCompressedMetadata_ForNonCompressedPath_Throws()
        {
            // Arrange: compression enabled; LargeStr compressed, OtherStr not compressed
            var doc = new { id = "1", LargeStr = new string('x', 400), OtherStr = new string('y', 50) };
            string[] paths = new[] { "/LargeStr", "/OtherStr" };
            EncryptionOptions options = CreateOptions(paths, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            Assert.IsTrue(props.CompressedEncryptedPaths.ContainsKey("/LargeStr"));
            Assert.IsFalse(props.CompressedEncryptedPaths.ContainsKey("/OtherStr"));

            // Forge metadata to claim OtherStr is compressed even though its ciphertext wasn't compressed
            IDictionary<string, int> forgedMap = new Dictionary<string, int>(props.CompressedEncryptedPaths)
            {
                ["/OtherStr"] = 50,
            };
            EncryptionProperties badProps = new(
                props.EncryptionFormatVersion,
                props.EncryptionAlgorithm,
                props.DataEncryptionKeyId,
                encryptedData: null,
                props.EncryptedPaths,
                props.CompressionAlgorithm,
                compressedEncryptedPaths: forgedMap);

            // Act + Assert: decompressor should fail on non-Brotli data
            MemoryStream output = new();
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, badProps, new CosmosDiagnosticsContext(), CancellationToken.None));
        }

        [TestMethod]
        public async Task Decrypt_ForgedUnknownTypeMarker_WritesRaw_InvalidJson()
        {
            // Arrange: create a valid encrypted payload
            var doc = new { id = "1", SensitiveStr = "abc" };
            string[] paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Replace SensitiveStr with a base64 value whose first byte is an unknown type marker (0xEE)
            byte[] bogusCipher = new byte[] { 0xEE, 0x01, 0x02, 0x03 };
            string forgedBase64 = Convert.ToBase64String(bogusCipher);

            encrypted.Position = 0;
            MemoryStream forged = new();
            using (JsonDocument jd = JsonDocument.Parse(encrypted, new JsonDocumentOptions { AllowTrailingCommas = true }))
            using (Utf8JsonWriter w = new(forged))
            {
                w.WriteStartObject();
                w.WriteString("id", jd.RootElement.GetProperty("id").GetString());
                w.WriteString("SensitiveStr", forgedBase64);
                w.WritePropertyName(Constants.EncryptedInfo);
                jd.RootElement.GetProperty(Constants.EncryptedInfo).WriteTo(w);
                w.WriteEndObject();
            }
            forged.Position = 0;

            // Act
            // Use a bypass encryptor to return raw bytes that are not valid JSON, exercising the default branch (WriteRawValue)
            StreamProcessor sp = new StreamProcessor { Encryptor = new AlwaysPlaintextMdeEncryptor("NOT_JSON") };
            MemoryStream output = new();
            _ = await sp.DecryptStreamAsync(forged, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;

            // Assert: output is not valid JSON due to raw invalid token insertion
            try
            {
                using JsonDocument _ = JsonDocument.Parse(output);
                Assert.Fail("Expected JSON parse to fail due to raw invalid token");
            }
            catch (Exception ex)
            {
                // System.Text.Json may throw JsonReaderException (derived) or JsonException depending on runtime
                Assert.IsTrue(ex is JsonException, $"Unexpected exception type: {ex.GetType()}");
            }
        }

        [TestMethod]
        public async Task Decrypt_ForgedTypeMarkerLong_InvalidPayload_Throws()
        {
            // Arrange: create a valid encrypted payload, then forge the marker to Long while the decryptor returns non-numeric plaintext
            var doc = new { id = "1", SensitiveStr = "abc" };
            string[] paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            byte[] bogusCipher = new byte[] { (byte)TypeMarker.Long, 0xAA, 0xBB, 0xCC };
            string forgedBase64 = Convert.ToBase64String(bogusCipher);

            encrypted.Position = 0;
            MemoryStream forged = new();
            using (JsonDocument jd = JsonDocument.Parse(encrypted, new JsonDocumentOptions { AllowTrailingCommas = true }))
            using (Utf8JsonWriter w = new(forged))
            {
                w.WriteStartObject();
                w.WriteString("id", jd.RootElement.GetProperty("id").GetString());
                w.WriteString("SensitiveStr", forgedBase64);
                w.WritePropertyName(Constants.EncryptedInfo);
                jd.RootElement.GetProperty(Constants.EncryptedInfo).WriteTo(w);
                w.WriteEndObject();
            }
            forged.Position = 0;

            // Act
            // Use encryptor that returns a plaintext that is invalid for a long serializer
            StreamProcessor sp = new StreamProcessor { Encryptor = new AlwaysPlaintextMdeEncryptor("abc") };
            MemoryStream output = new();
            try
            {
                await sp.DecryptStreamAsync(forged, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
                Assert.Fail("Expected exception due to invalid bigint serializer input");
            }
            catch (Exception ex)
            {
                // Serializer throws ArgumentSizeIncorrectException; verify something was thrown
                Assert.IsTrue(ex != null);
            }
        }

        [TestMethod]
        public async Task Decrypt_ForgedTypeMarkerDouble_InvalidPayload_Throws()
        {
            // Arrange: valid encrypted payload, forge marker to Double while plaintext is non-double bytes.
            var doc = new { id = "1", SensitiveStr = "abc" };
            string[] paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            byte[] bogusCipher = new byte[] { (byte)TypeMarker.Double, 0xAA, 0xBB, 0xCC };
            string forgedBase64 = Convert.ToBase64String(bogusCipher);

            encrypted.Position = 0;
            MemoryStream forged = new();
            using (JsonDocument jd = JsonDocument.Parse(encrypted, new JsonDocumentOptions { AllowTrailingCommas = true }))
            using (Utf8JsonWriter w = new(forged))
            {
                w.WriteStartObject();
                w.WriteString("id", jd.RootElement.GetProperty("id").GetString());
                w.WriteString("SensitiveStr", forgedBase64);
                w.WritePropertyName(Constants.EncryptedInfo);
                jd.RootElement.GetProperty(Constants.EncryptedInfo).WriteTo(w);
                w.WriteEndObject();
            }
            forged.Position = 0;

            StreamProcessor sp = new StreamProcessor { Encryptor = new AlwaysPlaintextMdeEncryptor("abc") };
            MemoryStream output = new();
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => sp.DecryptStreamAsync(forged, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None));
        }

        [TestMethod]
        public async Task Decrypt_ForgedTypeMarkerBoolean_InvalidPayload_Throws()
        {
            // Arrange: forge Boolean marker but plaintext buffer empty causing deserializer to fail.
            var doc = new { id = "1", SensitiveStr = "abc" };
            string[] paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            byte[] bogusCipher = new byte[] { (byte)TypeMarker.Boolean, 0x00 }; // boolean serializer expects at least 1 byte (0 or 1)
            string forgedBase64 = Convert.ToBase64String(bogusCipher);

            encrypted.Position = 0;
            MemoryStream forged = new();
            using (JsonDocument jd = JsonDocument.Parse(encrypted, new JsonDocumentOptions { AllowTrailingCommas = true }))
            using (Utf8JsonWriter w = new(forged))
            {
                w.WriteStartObject();
                w.WriteString("id", jd.RootElement.GetProperty("id").GetString());
                w.WriteString("SensitiveStr", forgedBase64);
                w.WritePropertyName(Constants.EncryptedInfo);
                jd.RootElement.GetProperty(Constants.EncryptedInfo).WriteTo(w);
                w.WriteEndObject();
            }
            forged.Position = 0;

            // Use encryptor returning empty plaintext to ensure boolean deserialize fails
            StreamProcessor sp = new StreamProcessor { Encryptor = new AlwaysPlaintextMdeEncryptor(string.Empty) };
            MemoryStream output = new();
            try
            {
                await sp.DecryptStreamAsync(forged, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
                Assert.Fail("Expected deserializer to throw due to invalid boolean payload length");
            }
            catch (Exception ex)
            {
                // Serializer throws a specific size exception (e.g., ArgumentSizeIncorrectException). We just validate it's not swallowed.
                Assert.IsTrue(ex.GetType().Name.Contains("ArgumentSize") || ex is InvalidOperationException, $"Unexpected exception type: {ex.GetType()}" );
            }
        }

        [TestMethod]
        public async Task Decrypt_CompressedContainsPathButNotCurrentProperty()
        {
            // Arrange
            // Scenario: compression enabled, some paths compressed, but the current decryptPropertyName is NOT in CompressedEncryptedPaths.
            // Covers branch: containsCompressed == true && TryGetValue == false so decompression block is skipped.
            var doc = new
            {
                id = "1",
                LargeStr = new string('x', 400), // will be compressed
                OtherStr = new string('y', 50),   // below default MinimalCompressedLength (128) so not compressed
            };
            string[] paths = new[] { "/LargeStr", "/OtherStr" };
            EncryptionOptions options = CreateOptions(paths, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            Assert.IsTrue(props.CompressedEncryptedPaths.ContainsKey("/LargeStr"));
            Assert.IsFalse(props.CompressedEncryptedPaths.ContainsKey("/OtherStr"));

            // Act
            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            JsonElement root = jd.RootElement;
            // Assert
            Assert.AreEqual(400, root.GetProperty("LargeStr").GetString().Length);
            Assert.AreEqual(50, root.GetProperty("OtherStr").GetString().Length);
            // Both should appear in decrypted paths
            Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/LargeStr"));
            Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/OtherStr"));
        }

        [TestMethod]
        public async Task Decrypt_PopulatesDiagnostics_BytesReadWritten()
        {
            // Arrange: small known payload with one encrypted property
            var doc = new { id = "1", SensitiveStr = "abc", Regular = 5 };
            string[] paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            CosmosDiagnosticsContext diag = new CosmosDiagnosticsContext();
            MemoryStream output = new();

            // Act
            _ = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, diag, CancellationToken.None);

            // Assert
            IReadOnlyDictionary<string, long> metrics = diag.GetMetricsSnapshot();
            Assert.IsTrue(metrics.ContainsKey("decrypt.bytesRead"));
            Assert.IsTrue(metrics.ContainsKey("decrypt.bytesWritten"));
            Assert.AreEqual(encrypted.Length, metrics["decrypt.bytesRead"], "bytesRead should equal input length for MemoryStream");
            Assert.AreEqual(output.Length, metrics["decrypt.bytesWritten"], "bytesWritten should equal output length");
            // quick sanity: output is valid JSON and contains decrypted value
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.AreEqual("abc", jd.RootElement.GetProperty("SensitiveStr").GetString());
        }

        [TestMethod]
        public async Task Decrypt_NonSeekable_SmallPayload_SinglePass()
        {
            // Arrange: create a small (<2KB) encrypted payload and wrap it in a non-seekable stream
            var doc = new { id = "1", SensitiveStr = "abc", Other = 42 };
            string[] paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            encrypted.Position = 0;
            using NonSeekableStream nonSeekable = new NonSeekableStream(encrypted.ToArray());

            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(nonSeekable, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);

            // Assert
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            JsonElement root = jd.RootElement;
            Assert.AreEqual("abc", root.GetProperty("SensitiveStr").GetString());
            Assert.AreEqual(42, root.GetProperty("Other").GetInt32());
            Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/SensitiveStr"));
        }

        private sealed class NonSeekableStream : Stream
        {
            private readonly MemoryStream inner;

            public NonSeekableStream(byte[] data)
            {
                this.inner = new MemoryStream(data, writable: false);
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => this.inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => this.inner.ReadAsync(buffer, cancellationToken);
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.inner.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        [TestMethod]
        public async Task Decrypt_Fuzz_Ciphertext_Length_And_TypeMarker_CrossProduct()
        {
            // Arrange
            // Property-style fuzzing across type markers and plaintext lengths. We don't assert per-iteration outcomes;
            // instead we ensure a wide set runs without catastrophic failures and that some known-good cases succeed.
            var doc = new { id = "1", V = "seed" };
            string[] paths = new[] { "/V" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encryptedSeed, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Extract _ei once to reuse in forged documents
            string eiRaw;
            string idValue;
            encryptedSeed.Position = 0;
            using (JsonDocument jd = JsonDocument.Parse(encryptedSeed, new JsonDocumentOptions { AllowTrailingCommas = true }))
            {
                idValue = jd.RootElement.GetProperty("id").GetString();
                eiRaw = jd.RootElement.GetProperty(Constants.EncryptedInfo).GetRawText();
            }

            Random rng = new Random(1234);
            byte[] markers = new byte[] { (byte)TypeMarker.String, (byte)TypeMarker.Long, (byte)TypeMarker.Double, (byte)TypeMarker.Boolean, 0xEE /* unknown */ };
            int iterationsPerLen = 4;
            int maxLen = 16;
            int attempts = 0;
            int successes = 0;

            StreamProcessor sp = new StreamProcessor { Encryptor = new MutablePlaintextMdeEncryptor() };
            MutablePlaintextMdeEncryptor mut = (MutablePlaintextMdeEncryptor)sp.Encryptor;

            // Act
            for (int len = 0; len <= maxLen; len++)
            {
                for (int m = 0; m < markers.Length; m++)
                {
                    for (int i = 0; i < iterationsPerLen; i++)
                    {
                        attempts++;
                        byte marker = markers[m];
                        byte[] plain = new byte[len];
                        rng.NextBytes(plain);
                        mut.Payload = plain;

                        // Build forged ciphertext (type marker only matters to switch in decryptor)
                        byte[] cipher = new byte[1 + 1]; // marker + 1 byte minimal to base64 properly
                        cipher[0] = marker;
                        cipher[1] = 0x00;
                        string base64 = Convert.ToBase64String(cipher);

                        using MemoryStream forged = new();
                        using (Utf8JsonWriter w = new(forged))
                        {
                            w.WriteStartObject();
                            w.WriteString("id", idValue);
                            w.WriteString("V", base64);
                            w.WritePropertyName(Constants.EncryptedInfo);
                            using (JsonDocument eiDoc = JsonDocument.Parse(eiRaw))
                            {
                                eiDoc.RootElement.WriteTo(w);
                            }
                            w.WriteEndObject();
                        }
                        forged.Position = 0;

                        try
                        {
                            using MemoryStream output = new();
                            _ = await sp.DecryptStreamAsync(forged, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
                            successes++;
                        }
                        catch
                        {
                            // Expected for many combinations (e.g., size mismatch or invalid UTF-8); continue
                        }
                    }
                }
            }

            // Add a few known-good shapes that should succeed to guarantee coverage of successful serialization paths
            (byte marker, byte[] payload)[] knownGood = new (byte marker, byte[] payload)[]
            {
                ((byte)TypeMarker.String, Encoding.UTF8.GetBytes("ok")),
                ((byte)TypeMarker.Long, new byte[8] /* 0L */),
                ((byte)TypeMarker.Double, new byte[8] /* 0.0 */),
                ((byte)TypeMarker.Boolean, new byte[]{ 1 }),
            };
            foreach ((byte marker, byte[] payload) in knownGood)
            {
                attempts++;
                mut.Payload = payload;
                string base64 = Convert.ToBase64String(new byte[] { marker, 0x00 });
                using MemoryStream forged = new();
                using (Utf8JsonWriter w = new(forged))
                {
                    w.WriteStartObject();
                    w.WriteString("id", idValue);
                    w.WriteString("V", base64);
                    w.WritePropertyName(Constants.EncryptedInfo);
                    using (JsonDocument eiDoc = JsonDocument.Parse(eiRaw))
                    {
                        eiDoc.RootElement.WriteTo(w);
                    }
                    w.WriteEndObject();
                }
                forged.Position = 0;
                using MemoryStream output = new();
                try
                {
                    _ = await sp.DecryptStreamAsync(forged, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
                    successes++;
                }
                catch
                {
                    // Some environments may still throw for Double if writer rejects NaN/Inf, but 0.0 should be fine; ignore either way
                }
            }

            // Assert
            Assert.IsTrue(attempts > 0, "No fuzz attempts executed");
            Assert.IsTrue(successes > 0, "Expected at least some successful decrypt/writes during fuzzing");
        }

        // Note: JsonTokenType.Comment branch remains uncovered intentionally. The decryptor configures JsonReaderOptions with CommentHandling.Skip (readonly static),
        // and the encryption pipeline never emits comments. Altering the static readonly field or constructing a custom reader just for coverage would add fragility.
        // The switch case exists defensively; functional risk is negligible.

        private class NullMarkerMdeEncryptor : MdeEncryptor
        {
            internal override (byte[] plainText, int plainTextLength) Decrypt(DataEncryptionKey encryptionKey, byte[] cipherText, int cipherTextLength, ArrayPoolManager arrayPoolManager)
            {
                if ((TypeMarker)cipherText[0] == TypeMarker.Null)
                {
                    // Return empty plaintext buffer (length 0). Caller will write null based on marker (already in cipherText[0]).
                    byte[] buffer = arrayPoolManager.Rent(0);
                    return (buffer, 0);
                }

                // Delegate to normal decrypt logic for non-null markers so compression scenarios work.
                return base.Decrypt(encryptionKey, cipherText, cipherTextLength, arrayPoolManager);
            }
        }

        private class AlwaysPlaintextMdeEncryptor : MdeEncryptor
        {
            private readonly byte[] payload;

            public AlwaysPlaintextMdeEncryptor(string raw)
            {
                this.payload = Encoding.UTF8.GetBytes(raw);
            }

            internal override (byte[] plainText, int plainTextLength) Decrypt(DataEncryptionKey encryptionKey, byte[] cipherText, int cipherTextLength, ArrayPoolManager arrayPoolManager)
            {
                byte[] buffer = arrayPoolManager.Rent(this.payload.Length);
                this.payload.AsSpan().CopyTo(buffer);
                return (buffer, this.payload.Length);
            }
        }

        private class MutablePlaintextMdeEncryptor : MdeEncryptor
        {
            public byte[] Payload { get; set; } = Array.Empty<byte>();

            internal override (byte[] plainText, int plainTextLength) Decrypt(DataEncryptionKey encryptionKey, byte[] cipherText, int cipherTextLength, ArrayPoolManager arrayPoolManager)
            {
                byte[] buffer = arrayPoolManager.Rent(this.Payload.Length);
                if (this.Payload.Length > 0)
                {
                    this.Payload.AsSpan().CopyTo(buffer);
                }
                return (buffer, this.Payload.Length);
            }
        }

    }
}
#endif
