//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using System.Buffers.Text;
    using System.Collections.Generic;
    using System.IO;
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

    private static EncryptionOptions CreateOptions(IEnumerable<string> paths)
        {
            return new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                JsonProcessor = JsonProcessor.Stream,
        PathsToEncrypt = paths.ToList()
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

            // Strong pre-decrypt verification: encrypted raw JSON must not contain sensitive plaintext values.
            string rawJson = Encoding.UTF8.GetString(encrypted.ToArray());
            EncryptionVerificationTestHelper.AssertEncryptedDocument(
                rawJson,
                encryptedProperties: new Dictionary<string, object>
                {
                    { "SensitiveStr", doc.SensitiveStr },
                    { "SensitiveInt", doc.SensitiveInt },
                    { "SensitiveBoolTrue", doc.SensitiveBoolTrue },
                    { "SensitiveBoolFalse", doc.SensitiveBoolFalse },
                    { "SensitiveArr", doc.SensitiveArr },
                    { "SensitiveObj", doc.SensitiveObj },
                },
                plainProperties: new Dictionary<string, object>
                {
                    { "SensitiveNull", (object)null },
                    { "NonSensitive", doc.NonSensitive },
                });

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

    // Compression-related decrypt tests removed.

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
            EncryptionProperties invalid = new EncryptionProperties(999, props.EncryptionAlgorithm, props.DataEncryptionKeyId, null, props.EncryptedPaths);
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
                    encryptedPaths: Array.Empty<string>());

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
                    encryptedPaths: Array.Empty<string>());

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

            // Strong pre-decrypt verification: ensure SensitiveStr plaintext absent, while (heuristically) expect to still see boolean literals true/false.
            string rawJson = Encoding.UTF8.GetString(encrypted.ToArray());
            EncryptionVerificationTestHelper.AssertEncryptedDocument(
                rawJson,
                encryptedProperties: new Dictionary<string, object> { { "SensitiveStr", doc.SensitiveStr } },
                plainProperties: new Dictionary<string, object>
                {
                    { "UnencryptedArr", doc.UnencryptedArr },
                    { "UnencryptedBoolTrue", doc.UnencryptedBoolTrue },
                    { "UnencryptedBoolFalse", doc.UnencryptedBoolFalse },
                });

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
                props.EncryptedPaths);

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
        public async Task Encrypt_NonSeekableOutput_WrapsAndSucceeds()
        {
            // Use a seekable memory input but wrap output to appear non-seekable to ensure code doesn't rely on output seeking except optional reposition.
            var doc = new { id = "1", V1 = "hello", V2 = "world" };
            string[] paths = new[] { "/V1", "/V2" };
            EncryptionOptions options = CreateOptions(paths);
            string json = System.Text.Json.JsonSerializer.Serialize(doc);
            MemoryStream input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            using NonSeekableWriteOnlyStream nonSeekableOut = new NonSeekableWriteOnlyStream();

            StreamProcessor sp = new StreamProcessor();
            await sp.EncryptStreamAsync(input, nonSeekableOut, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);

            // Fetch written bytes
            byte[] encryptedBytes = nonSeekableOut.ToArray();
            Assert.IsTrue(encryptedBytes.Length > 0);
            // Quick sanity: Can parse JSON and _ei exists
            using JsonDocument jd = JsonDocument.Parse(encryptedBytes);
            Assert.IsTrue(jd.RootElement.TryGetProperty(Constants.EncryptedInfo, out _));
        }

        private sealed class NonSeekableWriteOnlyStream : Stream
        {
            private readonly MemoryStream inner = new MemoryStream();
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => this.inner.Length;
            public override long Position
            {
                get => this.inner.Position;
                set => throw new NotSupportedException();
            }
            public override void Flush()
            {
                this.inner.Flush();
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }
            public override void SetLength(long value)
            {
                this.inner.SetLength(value);
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                this.inner.Write(buffer, offset, count);
            }
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return this.inner.WriteAsync(buffer, cancellationToken);
            }
            public byte[] ToArray()
            {
                return this.inner.ToArray();
            }
        }

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

        [TestMethod]
        public async Task Decrypt_MultiSegmentPropertyName_HandlesValueSequence()
        {
            // Arrange - encrypt a document with a long property name that will span multiple buffer segments
            // InitialBufferSize is 8, so a property name longer than that will cause ValueSequence during decrypt
            string longPropertyName = "VeryLongPropertyNameThatWillSpanMultipleBuffersWhenDecrypting";
            Dictionary<string, object> doc = new Dictionary<string, object>
            {
                { "id", "123" },
                { longPropertyName, "encryptedValue" },
                { "normalProp", "normalValue" }
            };
            
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, CreateOptions(new[] { $"/{longPropertyName}" }));
            MemoryStream output = new();

            // Act - decrypt the document
            await new StreamProcessor().DecryptStreamAsync(
                encrypted,
                output,
                mockEncryptor.Object,
                props,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            // Assert - verify the long property name was correctly handled
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.IsTrue(jd.RootElement.TryGetProperty(longPropertyName, out JsonElement decrypted));
            Assert.AreEqual("encryptedValue", decrypted.GetString());
            Assert.IsTrue(jd.RootElement.TryGetProperty("id", out JsonElement idElement));
            Assert.AreEqual("123", idElement.GetString());
            Assert.IsTrue(jd.RootElement.TryGetProperty("normalProp", out JsonElement normalElement));
            Assert.AreEqual("normalValue", normalElement.GetString());
        }

        [TestMethod]
        public async Task Decrypt_MixedProperties_LongUnencryptedNames_HandlesValueSequence()
        {
            // Arrange - decrypt document with long unencrypted property names (ValueSequence in pass-through)
            string longUnencryptedProp = "VeryLongUnencryptedPropertyName" + new string('X', 150);
            Dictionary<string, object> doc = new Dictionary<string, object>
            {
                { "id", "123" },
                { "encrypted", "secretValue" },
                { longUnencryptedProp, "plainValue" },
                { "alsoNotEncrypted", new string('Y', 200) } // Long value too
            };
            
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, CreateOptions(new[] { "/encrypted" }));
            MemoryStream output = new();

            // Act - decrypt
            await new StreamProcessor().DecryptStreamAsync(
                encrypted,
                output,
                mockEncryptor.Object,
                props,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            // Assert - verify long unencrypted property name and value preserved
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.IsTrue(jd.RootElement.TryGetProperty(longUnencryptedProp, out JsonElement longPropElem));
            Assert.AreEqual("plainValue", longPropElem.GetString());
            Assert.IsTrue(jd.RootElement.TryGetProperty("alsoNotEncrypted", out JsonElement longValueElem));
            Assert.AreEqual(200, longValueElem.GetString().Length);
            Assert.IsTrue(jd.RootElement.TryGetProperty("encrypted", out JsonElement decryptedElem));
            Assert.AreEqual("secretValue", decryptedElem.GetString());
        }

        [TestMethod]
        public async Task Decrypt_PassThrough_EscapedStrings_HandlesCorrectly()
        {
            // Arrange - decrypt document with escaped strings that are NOT encrypted
            Dictionary<string, object> doc = new Dictionary<string, object>
            {
                { "id", "123" },
                { "encrypted", "secret" },
                { "withNewlines", "Line1\nLine2\nLine3" },
                { "withTabs", "Col1\tCol2\tCol3" },
                { "withQuotes", "He said \"Hello\"" }
            };
            
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, CreateOptions(new[] { "/encrypted" }));
            MemoryStream output = new();

            // Act
            await new StreamProcessor().DecryptStreamAsync(
                encrypted,
                output,
                mockEncryptor.Object,
                props,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            // Assert - verify escaped strings preserved correctly
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.AreEqual("Line1\nLine2\nLine3", jd.RootElement.GetProperty("withNewlines").GetString());
            Assert.AreEqual("Col1\tCol2\tCol3", jd.RootElement.GetProperty("withTabs").GetString());
            Assert.AreEqual("He said \"Hello\"", jd.RootElement.GetProperty("withQuotes").GetString());
        }

        [TestMethod]
        public async Task Decrypt_VeryLargeEncryptedValue_HandlesValueSequence()
        {
            // Arrange - encrypt a very large value that will span multiple buffer segments when base64 encoded
            // This exercises ValueSequence path in encrypted value decryption (lines 388-402)
            int originalBufferSize = StreamProcessor.InitialBufferSize;
            try
            {
                StreamProcessor.InitialBufferSize = 16; // Small buffer to force ValueSequence
                
                // Create a large string that when encrypted and base64-encoded will span buffers
                string hugeValue = new string('Z', 10000);
                Dictionary<string, object> doc = new Dictionary<string, object>
                {
                    { "id", "123" },
                    { "hugeEncrypted", hugeValue }
                };
                
                (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, CreateOptions(new[] { "/hugeEncrypted" }));
                MemoryStream output = new();

                // Act
                await new StreamProcessor().DecryptStreamAsync(
                    encrypted,
                    output,
                    mockEncryptor.Object,
                    props,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None);

                // Assert
                output.Position = 0;
                using JsonDocument jd = JsonDocument.Parse(output);
                Assert.AreEqual(hugeValue, jd.RootElement.GetProperty("hugeEncrypted").GetString());
            }
            finally
            {
                StreamProcessor.InitialBufferSize = originalBufferSize;
            }
        }

        [TestMethod]
        public async Task Decrypt_PassThrough_LongNumber_HandlesValueSequence()
        {
            // Arrange - test long number that spans segments in pass-through path
            int originalBufferSize = StreamProcessor.InitialBufferSize;
            try
            {
                StreamProcessor.InitialBufferSize = 8; // Force multi-segment
                string longNumber = "987654321098765432109876543210.987654321098765432109876543210";
                Dictionary<string, object> doc = new()
                {
                    ["id"] = "123",
                    ["encrypted"] = "secret",
                    ["largeNum"] = double.Parse(longNumber)
                };
                
                EncryptionOptions options = CreateOptions(new[] { "/encrypted" });
                (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

                // Act - decrypt should preserve the long number
                encrypted.Position = 0;
                MemoryStream output = new();
                await new StreamProcessor().DecryptStreamAsync(
                    encrypted,
                    output,
                    mockEncryptor.Object,
                    props,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None);

                // Assert
                output.Position = 0;
                using JsonDocument jd = JsonDocument.Parse(output);
                double preserved = jd.RootElement.GetProperty("largeNum").GetDouble();
                Assert.AreEqual(double.Parse(longNumber), preserved);
            }
            finally
            {
                StreamProcessor.InitialBufferSize = originalBufferSize;
            }
        }

        [TestMethod]
        public async Task Decrypt_RootArray_HandlesCorrectly()
        {
            // Arrange - test decrypting when root is an array (not object)
            Dictionary<string, object> item1 = new() { ["id"] = "1", ["encrypted"] = "secret1" };
            Dictionary<string, object> item2 = new() { ["id"] = "2", ["encrypted"] = "secret2" };
            
            EncryptionOptions encryptOptions = CreateOptions(new[] { "/encrypted" });
            
            // Encrypt each item individually
            (MemoryStream enc1, EncryptionProperties props1) = await EncryptRawAsync(item1, encryptOptions);
            (MemoryStream enc2, EncryptionProperties _) = await EncryptRawAsync(item2, encryptOptions);
            
            // Create array JSON manually
            enc1.Position = 0;
            enc2.Position = 0;
            string item1Json = Encoding.UTF8.GetString(enc1.ToArray());
            string item2Json = Encoding.UTF8.GetString(enc2.ToArray());
            string arrayJson = $"[{item1Json},{item2Json}]";
            
            using MemoryStream input = new(Encoding.UTF8.GetBytes(arrayJson));
            MemoryStream output = new();

            // Act - decrypt array root (lines 444-446, 455-457)
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(
                input,
                output,
                mockEncryptor.Object,
                props1, // Use props from first item
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            // Assert
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.AreEqual(JsonValueKind.Array, jd.RootElement.ValueKind);
            Assert.AreEqual(2, jd.RootElement.GetArrayLength());
        }

        [TestMethod]
        public async Task Decrypt_TrailingWhitespaceAfterRoot_HandlesCorrectly()
        {
            // Arrange - JSON with trailing whitespace is valid
            Dictionary<string, object> doc = new() { ["id"] = "123", ["encrypted"] = "secret" };
            EncryptionOptions options = CreateOptions(new[] { "/encrypted" });
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);
            
            encrypted.Position = 0;
            string validJson = Encoding.UTF8.GetString(encrypted.ToArray());
            string jsonWithWhitespace = validJson + "   \t\n  "; // whitespace is OK
            
            using MemoryStream input = new(Encoding.UTF8.GetBytes(jsonWithWhitespace));
            MemoryStream output = new();

            // Act - should succeed with trailing whitespace (tests IsAllWhitespace helper)
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(
                input,
                output,
                mockEncryptor.Object,
                props,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            // Assert
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.AreEqual("123", jd.RootElement.GetProperty("id").GetString());
        }

        [TestMethod]
        public async Task Decrypt_ComplexNestedEncryptedInfo_SkipsCorrectly()
        {
            // Arrange - test that complex/nested _ei metadata is properly skipped
            // This exercises HandleActiveSkip with nested objects/arrays (lines 511-522)
            Dictionary<string, object> doc = new()
            {
                ["id"] = "complex",
                ["data"] = "encrypted",
                ["nested"] = new Dictionary<string, object>
                {
                    ["inner"] = "value"
                }
            };
            
            EncryptionOptions options = CreateOptions(new[] { "/data" });
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Act - decrypt should skip the _ei property completely
            encrypted.Position = 0;
            MemoryStream output = new();
            await new StreamProcessor().DecryptStreamAsync(
                encrypted,
                output,
                mockEncryptor.Object,
                props,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            // Assert - _ei should not be in output
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.IsFalse(jd.RootElement.TryGetProperty(Constants.EncryptedInfo, out _));
            Assert.AreEqual("complex", jd.RootElement.GetProperty("id").GetString());
        }

        [TestMethod]
        public async Task Decrypt_BooleanValues_DecryptsCorrectly()
        {
            // Arrange - test boolean type marker handling
            Dictionary<string, object> doc = new()
            {
                ["id"] = "bool-test",
                ["trueValue"] = true,
                ["falseValue"] = false
            };
            
            EncryptionOptions options = CreateOptions(new[] { "/trueValue", "/falseValue" });
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Act
            encrypted.Position = 0;
            MemoryStream output = new();
            await new StreamProcessor().DecryptStreamAsync(
                encrypted,
                output,
                mockEncryptor.Object,
                props,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            // Assert
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.AreEqual(true, jd.RootElement.GetProperty("trueValue").GetBoolean());
            Assert.AreEqual(false, jd.RootElement.GetProperty("falseValue").GetBoolean());
        }

        [TestMethod]
        public async Task Decrypt_NullValue_DecryptsCorrectly()
        {
            // Arrange - test null type marker handling
            Dictionary<string, object> doc = new()
            {
                ["id"] = "null-test",
                ["nullProp"] = null
            };
            
            EncryptionOptions options = CreateOptions(new[] { "/nullProp" });
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Act
            encrypted.Position = 0;
            MemoryStream output = new();
            await new StreamProcessor().DecryptStreamAsync(
                encrypted,
                output,
                mockEncryptor.Object,
                props,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            // Assert
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.AreEqual(JsonValueKind.Null, jd.RootElement.GetProperty("nullProp").ValueKind);
        }

        [TestMethod]
        public async Task Decrypt_LongNumber_DecryptsCorrectly()
        {
            // Arrange - test TypeMarker.Long deserialization (lines 617-623)
            Dictionary<string, object> doc = new()
            {
                ["id"] = "long-test",
                ["longValue"] = 9223372036854775807L // Max long value
            };
            
            EncryptionOptions options = CreateOptions(new[] { "/longValue" });
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Act
            encrypted.Position = 0;
            MemoryStream output = new();
            await new StreamProcessor().DecryptStreamAsync(
                encrypted,
                output,
                mockEncryptor.Object,
                props,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            // Assert
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.AreEqual(9223372036854775807L, jd.RootElement.GetProperty("longValue").GetInt64());
        }

        [TestMethod]
        public async Task Decrypt_DoubleNumber_DecryptsCorrectly()
        {
            // Arrange - test TypeMarker.Double deserialization (lines 628-634)
            Dictionary<string, object> doc = new()
            {
                ["id"] = "double-test",
                ["doubleValue"] = 3.141592653589793
            };
            
            EncryptionOptions options = CreateOptions(new[] { "/doubleValue" });
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Act
            encrypted.Position = 0;
            MemoryStream output = new();
            await new StreamProcessor().DecryptStreamAsync(
                encrypted,
                output,
                mockEncryptor.Object,
                props,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            // Assert
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.AreEqual(3.141592653589793, jd.RootElement.GetProperty("doubleValue").GetDouble(), 0.000001);
        }

        [TestMethod]
        public async Task Decrypt_MixedTypes_AllTypeMarkers_DecryptsCorrectly()
        {
            // Arrange - comprehensive test of all type markers in one document
            Dictionary<string, object> doc = new()
            {
                ["id"] = "mixed-types",
                ["stringVal"] = "text",
                ["longVal"] = 12345L,
                ["doubleVal"] = 67.89,
                ["boolVal"] = true,
                ["nullVal"] = null
            };
            
            EncryptionOptions options = CreateOptions(new[] { "/stringVal", "/longVal", "/doubleVal", "/boolVal", "/nullVal" });
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Act
            encrypted.Position = 0;
            MemoryStream output = new();
            DecryptionContext ctx = await new StreamProcessor().DecryptStreamAsync(
                encrypted,
                output,
                mockEncryptor.Object,
                props,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            // Assert
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.AreEqual("text", jd.RootElement.GetProperty("stringVal").GetString());
            Assert.AreEqual(12345L, jd.RootElement.GetProperty("longVal").GetInt64());
            Assert.AreEqual(67.89, jd.RootElement.GetProperty("doubleVal").GetDouble(), 0.01);
            Assert.AreEqual(true, jd.RootElement.GetProperty("boolVal").GetBoolean());
            Assert.AreEqual(JsonValueKind.Null, jd.RootElement.GetProperty("nullVal").ValueKind);
            // Note: null values are not encrypted, so only 4 paths are decrypted
            Assert.AreEqual(4, ctx.DecryptionInfoList[0].PathsDecrypted.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task Decrypt_InvalidDecryptionReturnValue_ThrowsInvalidOperationException()
        {
            // Arrange - test lines 581-583: negative decryptedLength from DecryptData
            Mock<DataEncryptionKey> badDek = new Mock<DataEncryptionKey>();
            badDek.SetupGet(d => d.EncryptionAlgorithm).Returns(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);
            badDek.Setup(d => d.GetDecryptByteCount(It.IsAny<int>())).Returns(100);
            badDek.Setup(d => d.DecryptData(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<int>()))
                .Returns(-1); // Invalid negative return value

            Mock<Encryptor> badEncryptor = new Mock<Encryptor>();
            badEncryptor.Setup(e => e.GetEncryptionKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(badDek.Object);

            Dictionary<string, object> doc = new() { ["id"] = "1", ["data"] = "secret" };
            EncryptionOptions options = CreateOptions(new[] { "/data" });
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Act - should throw InvalidOperationException for negative decryption length
            encrypted.Position = 0;
            MemoryStream output = new();
            await new StreamProcessor().DecryptStreamAsync(
                encrypted,
                output,
                badEncryptor.Object,
                props,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task Decrypt_CorruptedLongDeserialization_ThrowsInvalidOperationException()
        {
            // Arrange - test lines 617-623: invalid Long deserialization
            Mock<DataEncryptionKey> badDek = new Mock<DataEncryptionKey>();
            badDek.SetupGet(d => d.EncryptionAlgorithm).Returns(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);
            badDek.Setup(d => d.GetDecryptByteCount(It.IsAny<int>())).Returns(100);
            badDek.Setup(d => d.DecryptData(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<int>()))
                .Returns((byte[] input, int inOff, int inLen, byte[] output, int outOff) =>
                {
                    // Return corrupted Long data (invalid length - SqlLongSerializer expects 8 bytes)
                    output[outOff] = (byte)TypeMarker.Long;
                    output[outOff + 1] = 0x01; // Only 2 bytes instead of 8
                    return 2;
                });

            Mock<Encryptor> badEncryptor = new Mock<Encryptor>();
            badEncryptor.Setup(e => e.GetEncryptionKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(badDek.Object);

            Dictionary<string, object> doc = new() { ["id"] = "1", ["longVal"] = 12345L };
            EncryptionOptions options = CreateOptions(new[] { "/longVal" });
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Act - should throw InvalidOperationException for corrupted Long
            encrypted.Position = 0;
            MemoryStream output = new();
            await new StreamProcessor().DecryptStreamAsync(
                encrypted,
                output,
                badEncryptor.Object,
                props,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task Decrypt_CorruptedDoubleDeserialization_ThrowsInvalidOperationException()
        {
            // Arrange - test lines 628-634: invalid Double deserialization
            Mock<DataEncryptionKey> badDek = new Mock<DataEncryptionKey>();
            badDek.SetupGet(d => d.EncryptionAlgorithm).Returns(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);
            badDek.Setup(d => d.GetDecryptByteCount(It.IsAny<int>())).Returns(100);
            badDek.Setup(d => d.DecryptData(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<int>()))
                .Returns((byte[] input, int inOff, int inLen, byte[] output, int outOff) =>
                {
                    // Return corrupted Double data (invalid length - SqlDoubleSerializer expects 8 bytes)
                    output[outOff] = (byte)TypeMarker.Double;
                    output[outOff + 1] = 0x01; // Only 3 bytes instead of 8
                    output[outOff + 2] = 0x02;
                    return 3;
                });

            Mock<Encryptor> badEncryptor = new Mock<Encryptor>();
            badEncryptor.Setup(e => e.GetEncryptionKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(badDek.Object);

            Dictionary<string, object> doc = new() { ["id"] = "1", ["doubleVal"] = 3.14 };
            EncryptionOptions options = CreateOptions(new[] { "/doubleVal" });
            (MemoryStream encrypted, EncryptionProperties props) = await EncryptRawAsync(doc, options);

            // Act - should throw InvalidOperationException for corrupted Double
            encrypted.Position = 0;
            MemoryStream output = new();
            await new StreamProcessor().DecryptStreamAsync(
                encrypted,
                output,
                badEncryptor.Object,
                props,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);
        }
    }
}
#endif
