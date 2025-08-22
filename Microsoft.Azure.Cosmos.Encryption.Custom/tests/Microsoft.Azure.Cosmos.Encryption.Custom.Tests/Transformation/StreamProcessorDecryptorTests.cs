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
        public async Task Decrypt_Throws_OnUnknownCompressionAlgorithm()
        {
            // Arrange: produce a payload with compression so CompressedEncryptedPaths is non-empty
            var doc = new { id = "1", LargeStr = new string('x', 400) };
            var paths = new[] { "/LargeStr" };
            EncryptionOptions options = CreateOptions(paths, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            Assert.IsTrue(props.CompressedEncryptedPaths?.Any() == true);

            // Forge properties with an unsupported compression algorithm to trigger the guard
            EncryptionProperties badProps = new (
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
            EncryptionProperties invalid = new EncryptionProperties(999, props.EncryptionAlgorithm, props.DataEncryptionKeyId, null, props.EncryptedPaths, props.CompressionAlgorithm, props.CompressedEncryptedPaths);
            MemoryStream output = new();
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() => new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, invalid, new CosmosDiagnosticsContext(), CancellationToken.None));
        }

        [TestMethod]
        public async Task Decrypt_Throws_OnInvalidBase64Ciphertext()
        {
            var doc = new { id = "1", SensitiveStr = "abc" };
            var paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            string jsonText = Encoding.UTF8.GetString(encrypted.ToArray());
            using JsonDocument jd = JsonDocument.Parse(jsonText);
            string originalCipher = jd.RootElement.GetProperty("SensitiveStr").GetString();
            Assert.IsNotNull(originalCipher);
            string corruptedCipher = "#" + originalCipher.Substring(1); // invalid base64 start
            jsonText = jsonText.Replace("\"SensitiveStr\":\"" + originalCipher + "\"", "\"SensitiveStr\":\"" + corruptedCipher + "\"");
            MemoryStream corruptedStream = new(Encoding.UTF8.GetBytes(jsonText));
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

        [TestMethod]
        public async Task Decrypt_Throws_OnCompressedPayload_DestinationTooSmall()
        {
            // Arrange: valid compressed payload, but forge properties to declare a too-small decompressed size for the path.
            var doc = new { id = "1", LargeStr = new string('x', 400) };
            var paths = new[] { "/LargeStr" };
            EncryptionOptions options = CreateOptions(paths, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            Assert.IsTrue(props.CompressedEncryptedPaths.ContainsKey("/LargeStr"), "Expected LargeStr to be marked as compressed");

            // Forge smaller decompressed size to force DestinationTooSmall from Brotli decoder
            int declared = props.CompressedEncryptedPaths["/LargeStr"];
            IDictionary<string, int> adjusted = new Dictionary<string, int>(props.CompressedEncryptedPaths)
            {
                ["/LargeStr"] = Math.Max(1, declared / 4)
            };
            EncryptionProperties badProps = new (
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
                MemoryStream output = new();
                DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
                output.Position = 0;
                using JsonDocument jd = JsonDocument.Parse(output, new JsonDocumentOptions { AllowTrailingCommas = true });
                JsonElement root = jd.RootElement;
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
            // Cover branch where EncryptionProperties.CompressedEncryptedPaths is null (as opposed to empty dictionary or populated),
            // exercising the null path of the null-conditional operator in: bool containsCompressed = properties.CompressedEncryptedPaths?.Count > 0;
            var doc = new { id = "1", SensitiveStr = "abc" };
            var paths = new[] { "/SensitiveStr" };
            EncryptionOptions options = CreateOptions(paths); // no compression requested
            (MemoryStream encrypted, EncryptionProperties propsWithEmpty) = await EncryptRawAsync(doc, options);

            // Forge properties with CompressedEncryptedPaths = null to reach missing branch
            EncryptionProperties propsNullCompressed = new (
                propsWithEmpty.EncryptionFormatVersion,
                propsWithEmpty.EncryptionAlgorithm,
                propsWithEmpty.DataEncryptionKeyId,
                encryptedData: null,
                propsWithEmpty.EncryptedPaths,
                propsWithEmpty.CompressionAlgorithm, // None
                compressedEncryptedPaths: null);

            encrypted.Position = 0;
            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, propsNullCompressed, new CosmosDiagnosticsContext(), CancellationToken.None);

            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.AreEqual("abc", jd.RootElement.GetProperty("SensitiveStr").GetString());
            Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/SensitiveStr"));
        }

        [TestMethod]
        public async Task Decrypt_UnencryptedArrayAndBooleans()
        {
            // Covers StartArray / EndArray / True / False switch branches where decryptPropertyName == null (no encryption for those tokens).
            var doc = new
            {
                id = "1",
                SensitiveStr = "secret",
                UnencryptedArr = new int[] { 7, 8, 9 },
                UnencryptedBoolTrue = true,
                UnencryptedBoolFalse = false,
            };
            var paths = new[] { "/SensitiveStr" }; // only encrypt the string; others remain plain
            EncryptionOptions options = CreateOptions(paths);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);

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
            // Covers TypeMarker.Null switch branch by forging a ciphertext with first byte = Null marker.
            var doc = new { id = "1", SensitiveStr = "abc" };
            var paths = new[] { "/SensitiveStr" };
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

            // Use custom encryptor that returns empty plaintext for Null marker
            var sp = new StreamProcessor { Encryptor = new NullMarkerMdeEncryptor() };
            MemoryStream output = new();
            DecryptionContext ctx = await sp.DecryptStreamAsync(forged, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            using JsonDocument outDoc = JsonDocument.Parse(output);
            Assert.AreEqual(JsonValueKind.Null, outDoc.RootElement.GetProperty("SensitiveStr").ValueKind);
            Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/SensitiveStr"));
        }

        [TestMethod]
        public async Task Decrypt_CompressedContainsPathButNotCurrentProperty()
        {
            // Scenario: compression enabled, some paths compressed, but the current decryptPropertyName is NOT in CompressedEncryptedPaths.
            // Covers branch: containsCompressed == true && TryGetValue == false so decompression block is skipped.
            var doc = new
            {
                id = "1",
                LargeStr = new string('x', 400), // will be compressed
                OtherStr = new string('y', 50),   // below default MinimalCompressedLength (128) so not compressed
            };
            var paths = new[] { "/LargeStr", "/OtherStr" };
            EncryptionOptions options = CreateOptions(paths, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest);
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            Assert.IsTrue(props.CompressedEncryptedPaths.ContainsKey("/LargeStr"));
            Assert.IsFalse(props.CompressedEncryptedPaths.ContainsKey("/OtherStr"));

            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            JsonElement root = jd.RootElement;
            Assert.AreEqual(400, root.GetProperty("LargeStr").GetString().Length);
            Assert.AreEqual(50, root.GetProperty("OtherStr").GetString().Length);
            // Both should appear in decrypted paths
            Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/LargeStr"));
            Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/OtherStr"));
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

    }
}
#endif
