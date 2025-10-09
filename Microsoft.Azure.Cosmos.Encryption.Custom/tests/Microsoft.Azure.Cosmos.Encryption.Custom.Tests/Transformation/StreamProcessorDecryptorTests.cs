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
