//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Stream that returns data in very small chunks to force Utf8JsonReader to see HasValueSequence=true.
    /// </summary>
    internal class TinyChunkStream : Stream
    {
        private readonly byte[] data;
        private int position;
        private readonly int maxChunkSize;

        public TinyChunkStream(byte[] data, int maxChunkSize = 4)
        {
            this.data = data;
            this.maxChunkSize = maxChunkSize;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => this.data.Length;
        public override long Position { get => this.position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int available = this.data.Length - this.position;
            if (available == 0)
            {
                return 0;
            }
            
            int toRead = Math.Min(Math.Min(count, this.maxChunkSize), available);
            Array.Copy(this.data, this.position, buffer, offset, toRead);
            this.position += toRead;
            return toRead;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.Read(buffer, offset, count));
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    [TestClass]
    public class StreamProcessorEncryptorTests
    {
        private const string DekId = "dekId";
        private static Mock<Encryptor> mockEncryptor;
        private static Mock<DataEncryptionKey> mockDek;

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            _ = ctx;
            StreamProcessor.InitialBufferSize = 8; // exercise buffer growth

            mockEncryptor = TestEncryptorFactory.CreateMde(DekId, out mockDek);
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

        private static async Task<MemoryStream> EncryptAsync(object doc, EncryptionOptions options)
        {
            Stream input = TestCommon.ToStream(doc);
            MemoryStream output = new();
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            return output;
        }

        private static JsonDocument Parse(Stream s)
        {
            s.Position = 0;
            return JsonDocument.Parse(s);
        }

        [TestMethod]
        public async Task Encrypt_AllPrimitiveTypesAndContainers()
        {
            // Arrange
            var doc = new
            {
                id = Guid.NewGuid().ToString(),
                SensitiveStr = "abc",
                SensitiveInt = 123L, // long branch
                SensitiveDouble = 3.14159,
                SensitiveBoolTrue = true,
                SensitiveBoolFalse = false,
                SensitiveNull = (string)null, // will not be encrypted
                SensitiveArr = new object[] { 1, 2, 3 },
                SensitiveObj = new { a = 5, b = "text" },
                NonSensitive = 42
            };
            string[] paths = new[] { "/SensitiveStr", "/SensitiveInt", "/SensitiveDouble", "/SensitiveBoolTrue", "/SensitiveBoolFalse", "/SensitiveNull", "/SensitiveArr", "/SensitiveObj" };
            EncryptionOptions options = CreateOptions(paths);

            // Act (encrypt)
            MemoryStream encrypted = await EncryptAsync(doc, options);
            string rawJson = Encoding.UTF8.GetString(encrypted.ToArray());
            using JsonDocument jd = Parse(encrypted);
            JsonElement root = jd.RootElement;

            // Assert (encryption)
            // Encryption properties appended
            Assert.IsTrue(root.TryGetProperty(Constants.EncryptedInfo, out JsonElement ei));
            EncryptionProperties props = System.Text.Json.JsonSerializer.Deserialize<EncryptionProperties>(ei.GetRawText());
            Assert.AreEqual(EncryptionFormatVersion.Mde, props.EncryptionFormatVersion);

            // Null path should be excluded
            Assert.IsFalse(props.EncryptedPaths.Contains("/SensitiveNull"));

            foreach (string path in paths.Where(p => p != "/SensitiveNull"))
            {
                string name = path.TrimStart('/');
                string base64 = root.GetProperty(name).GetString();
                Assert.IsNotNull(base64);
                byte[] cipherBytes = Convert.FromBase64String(base64);
                // first byte is type marker
                switch (name)
                {
                    case "SensitiveStr": Assert.AreEqual((byte)TypeMarker.String, cipherBytes[0]); break;
                    case "SensitiveInt": Assert.AreEqual((byte)TypeMarker.Long, cipherBytes[0]); break;
                    case "SensitiveDouble": Assert.AreEqual((byte)TypeMarker.Double, cipherBytes[0]); break;
                    case "SensitiveBoolTrue":
                    case "SensitiveBoolFalse": Assert.AreEqual((byte)TypeMarker.Boolean, cipherBytes[0]); break;
                    case "SensitiveArr": Assert.AreEqual((byte)TypeMarker.Array, cipherBytes[0]); break;
                    case "SensitiveObj": Assert.AreEqual((byte)TypeMarker.Object, cipherBytes[0]); break;
                }
            }

            // Strong verification: ensure raw JSON does not contain plaintext for encrypted values (string/number markers)
            EncryptionVerificationTestHelper.AssertEncryptedDocument(
                rawJson,
                encryptedProperties: new Dictionary<string, object>
                {
                    { "SensitiveStr", doc.SensitiveStr },
                    { "SensitiveInt", doc.SensitiveInt },
                    { "SensitiveDouble", doc.SensitiveDouble },
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

            // Act (decrypt)
            encrypted.Position = 0;
            (Stream decrypted, DecryptionContext ctx) = await EncryptionProcessor.DecryptStreamAsync(encrypted, mockEncryptor.Object, new CosmosDiagnosticsContext(), CancellationToken.None);
            // Assert (roundtrip)
            using JsonDocument d2 = Parse(decrypted);
            JsonElement r2 = d2.RootElement;
            Assert.AreEqual(doc.SensitiveStr, r2.GetProperty("SensitiveStr").GetString());
            Assert.AreEqual((long)doc.SensitiveInt, r2.GetProperty("SensitiveInt").GetInt64());
            Assert.AreEqual(doc.SensitiveDouble, r2.GetProperty("SensitiveDouble").GetDouble(), 0.00001);
            Assert.AreEqual(doc.SensitiveBoolTrue, r2.GetProperty("SensitiveBoolTrue").GetBoolean());
            Assert.AreEqual(doc.SensitiveBoolFalse, r2.GetProperty("SensitiveBoolFalse").GetBoolean());
            Assert.AreEqual(System.Text.Json.JsonValueKind.Null, r2.GetProperty("SensitiveNull").ValueKind);
            Assert.AreEqual(3, r2.GetProperty("SensitiveArr").GetArrayLength());
            Assert.AreEqual("text", r2.GetProperty("SensitiveObj").GetProperty("b").GetString());
            Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/SensitiveStr"));
        }

    // Compression behavior test removed as compression support was dropped.

        [TestMethod]
        public async Task Encrypt_NestedObjectAndArray()
        {
            // Arrange
            var doc = new
            {
                id = "1",
                Nested = new { a = 5, b = new { c = 10 } },
                Arr = new object[] { new { x = 1 }, new { x = 2 } },
                Plain = 7
            };
            string[] paths = new[] { "/Nested", "/Arr" };
            // Act (encrypt)
            MemoryStream encrypted = await EncryptAsync(doc, CreateOptions(paths));
            using JsonDocument jd = Parse(encrypted);
            JsonElement root = jd.RootElement;
            // Assert (encryption)
            Assert.AreEqual(JsonValueKind.String, root.GetProperty("Nested").ValueKind);
            Assert.AreEqual(JsonValueKind.String, root.GetProperty("Arr").ValueKind);

            // Act (decrypt)
            encrypted.Position = 0;
            (Stream decrypted, _) = await EncryptionProcessor.DecryptStreamAsync(encrypted, mockEncryptor.Object, new CosmosDiagnosticsContext(), CancellationToken.None);
            // Assert (roundtrip)
            using JsonDocument d2 = Parse(decrypted);
            JsonElement r2 = d2.RootElement;
            Assert.AreEqual(5, r2.GetProperty("Nested").GetProperty("a").GetInt32());
            Assert.AreEqual(2, r2.GetProperty("Arr").GetArrayLength());
        }

        [TestMethod]
        public async Task Encrypt_BufferGrowthLargeString()
        {
            // Arrange
            var doc = new { id = "1", Big = new string('a', 5000) };
            string[] paths = new[] { "/Big" };
            // Act
            MemoryStream encrypted = await EncryptAsync(doc, CreateOptions(paths));
            string rawJson = Encoding.UTF8.GetString(encrypted.ToArray());
            using JsonDocument jd = Parse(encrypted);
            // Assert
            string cipher = jd.RootElement.GetProperty("Big").GetString();
            Assert.IsTrue(cipher.Length > 10);
            EncryptionVerificationTestHelper.AssertEncryptedDocument(rawJson, new Dictionary<string, object> { { "Big", doc.Big } });
        }

        [TestMethod]
        public async Task Encrypt_SkipsNullProperty()
        {
            // Arrange
            var doc = new { id = "1", Maybe = (string)null };
            string[] paths = new[] { "/Maybe" };
            // Act
            MemoryStream encrypted = await EncryptAsync(doc, CreateOptions(paths));
            using JsonDocument jd = Parse(encrypted);
            JsonElement root = jd.RootElement;
            // Assert
            EncryptionProperties props = System.Text.Json.JsonSerializer.Deserialize<EncryptionProperties>(root.GetProperty(Constants.EncryptedInfo).GetRawText());
            Assert.IsFalse(props.EncryptedPaths.Contains("/Maybe"));
            Assert.AreEqual(JsonValueKind.Null, root.GetProperty("Maybe").ValueKind);
        }

        [TestMethod]
        public async Task Encrypt_NullThenPlain_RemainsPlain()
        {
            // Arrange
            // Regression: if encryption state isn't cleared after a null, the next property might be incorrectly encrypted
            string json = "{\"Maybe\":null,\"Plain\":42,\"id\":\"1\"}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/Maybe" });
            // Act
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
            string rawJson = Encoding.UTF8.GetString(output.ToArray());
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            JsonElement root = jd.RootElement;
            // Assert
            // Maybe remains null and is not listed in encrypted paths
            Assert.AreEqual(JsonValueKind.Null, root.GetProperty("Maybe").ValueKind);
            EncryptionProperties props = System.Text.Json.JsonSerializer.Deserialize<EncryptionProperties>(root.GetProperty(Constants.EncryptedInfo).GetRawText());
            Assert.IsFalse(props.EncryptedPaths.Contains("/Maybe"));
            // Plain must remain a number (not a base64 string)
            Assert.AreEqual(JsonValueKind.Number, root.GetProperty("Plain").ValueKind);
            Assert.AreEqual(42, root.GetProperty("Plain").GetInt32());
            EncryptionVerificationTestHelper.AssertEncryptedDocument(
                rawJson,
                encryptedProperties: new Dictionary<string, object>(),
                plainProperties: new Dictionary<string, object> { { "Plain", 42 } });
        }

        [TestMethod]
        public void Encrypt_InternalProperty_Getter_Coverage()
        {
            // Arrange
            // Touch internal partial members for coverage: Encryptor property lives on decryptor partial file
            StreamProcessor sp = new StreamProcessor();
            // Assert
            Assert.IsNotNull(sp.Encryptor); // covers getter sequence point
            Assert.IsTrue(StreamProcessor.InitialBufferSize > 0);
        }

        [TestMethod]
        public async Task Encrypt_NumberParsing_IsCultureInvariant()
        {
            // Arrange
            // Force a culture that expects comma as decimal separator so parsing a dot-formatted invariant number fails
            CultureInfo original = CultureInfo.CurrentCulture;
            CultureInfo originalUi = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo commaCulture = new CultureInfo("fr-FR");
                CultureInfo.CurrentCulture = commaCulture;
                CultureInfo.CurrentUICulture = commaCulture;

                // Anonymous object with a decimal value will be serialized using invariant culture ("1.23") by Json.NET
                var doc = new { id = "1", Weird = 1.23m };
                string[] paths = new[] { "/Weird" };
                EncryptionOptions options = CreateOptions(paths);

                // Act
                // Should succeed regardless of current culture and round-trip the value as a double
                MemoryStream encrypted = await EncryptAsync(doc, options);
                encrypted.Position = 0;
                (Stream decrypted, _) = await EncryptionProcessor.DecryptStreamAsync(encrypted, mockEncryptor.Object, new CosmosDiagnosticsContext(), CancellationToken.None);
                // Assert
                using JsonDocument d2 = JsonDocument.Parse(decrypted);
                double value = d2.RootElement.GetProperty("Weird").GetDouble();
                Assert.AreEqual(1.23, value, 1e-12);
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
                CultureInfo.CurrentUICulture = originalUi;
            }
        }

        [TestMethod]
        public async Task Encrypt_InputWithComments_IgnoresComments()
        {
            // Arrange
            // Although StreamProcessor has a comment case, JsonReaderOptions uses CommentHandling=Skip, so comments are not surfaced as tokens.
            // This test documents that behavior: comments are silently dropped and encryption still succeeds.
            string json = "{\n  // comment before sensitive\n  \"SensitiveStr\": \"abc\",\n  // trailing comment\n  \"id\": \"1\"\n}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/SensitiveStr" });
            // Act
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
            string rawJson = Encoding.UTF8.GetString(output.ToArray());
            output.Position = 0;
            using JsonDocument jd = Parse(output);
            JsonElement root = jd.RootElement;
            // Assert
            // SensitiveStr should be encrypted (string -> base64), id should remain plain, no comments present.
            string cipher = root.GetProperty("SensitiveStr").GetString();
            Assert.IsNotNull(cipher);
            Assert.AreEqual("1", root.GetProperty("id").GetString());
            // Ensure encrypted info present
            Assert.IsTrue(root.TryGetProperty(Constants.EncryptedInfo, out _));
            EncryptionVerificationTestHelper.AssertEncryptedDocument(
                rawJson,
                encryptedProperties: new Dictionary<string, object> { { "SensitiveStr", "abc" } },
                plainProperties: new Dictionary<string, object> { { "id", "1" } });
        }

        [TestMethod]
        public async Task Encrypt_NonObjectRoot_Array_RemainsUnchanged()
        {
            // Arrange
            // Attacker supplies a non-object root. Encrypt path always appends _ei into an object context, so this must fail.
            string json = "[1,2,3]";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(Array.Empty<string>());
            // Act
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            // Assert
            Assert.AreEqual(JsonValueKind.Array, jd.RootElement.ValueKind);
        }

        [TestMethod]
        public async Task Encrypt_NonObjectRoot_Primitive_RemainsUnchanged()
        {
            // Arrange
            foreach (string json in new[] { "123", "\"str\"", "true", "false", "null" })
            {
                using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
                MemoryStream output = new();
                EncryptionOptions options = CreateOptions(Array.Empty<string>());
                // Act
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
                output.Position = 0;
                using JsonDocument jd = JsonDocument.Parse(output);
                // Assert
                // Ensure the root kind matches the primitive provided
                JsonValueKind kind = jd.RootElement.ValueKind;
                switch (json)
                {
                    case "123": Assert.AreEqual(JsonValueKind.Number, kind); break;
                    case "\"str\"": Assert.AreEqual(JsonValueKind.String, kind); break;
                    case "true": Assert.AreEqual(JsonValueKind.True, kind); break;
                    case "false": Assert.AreEqual(JsonValueKind.False, kind); break;
                    case "null": Assert.AreEqual(JsonValueKind.Null, kind); break;
                }
            }
        }

        [TestMethod]
        public async Task Encrypt_Fails_OnTruncatedJson()
        {
            // Arrange
            // Missing closing quote and brace
            string json = "{\"id\":\"1\",\"SensitiveStr\":\"abc"; // truncated
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/SensitiveStr" });
            try
            {
                // Act
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
                // Assert
                Assert.Fail("Expected exception for truncated JSON");
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentOutOfRangeException || ex is InvalidOperationException)
            {
                // acceptable failure modes
            }
        }

        [TestMethod]
        public async Task Encrypt_Fails_OnDoubleInfinity()
        {
            // Arrange
            // Extremely large exponent overflows to Infinity in double parsing; serializer should not accept it.
            string json = "{\"id\":\"1\",\"SensitiveDouble\":1e309}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/SensitiveDouble" });
            try
            {
                // Act
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
                // Assert
                Assert.Fail("Expected exception for Infinity double serialization");
            }
            catch (Exception ex)
            {
                // Different serializer layers may throw different exception types; verify it's about out-of-range/infinity
                StringAssert.Contains(ex.ToString(), "Infinity");
            }
        }

        [TestMethod]
        public async Task Encrypt_Fails_OnInvalidUtf8InString()
        {
            // Arrange
            // Construct bytes for: {"id":"1","SensitiveStr":"<invalid utf8>"}
            // Invalid sequence C3 28
            byte[] bytes = new byte[] {
                0x7B, // {
                0x22, (byte)'i', (byte)'d', 0x22, 0x3A, 0x22, (byte)'1', 0x22, 0x2C,
                0x22, (byte)'S', (byte)'e', (byte)'n', (byte)'s', (byte)'i', (byte)'t', (byte)'i', (byte)'v', (byte)'e', (byte)'S', (byte)'t', (byte)'r', 0x22, 0x3A, 0x22,
                0xC3, 0x28, // invalid UTF-8 sequence
                0x22,
                0x7D // }
            };
            using MemoryStream input = new MemoryStream(bytes);
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/SensitiveStr" });
            try
            {
                // Act
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
                // Assert
                Assert.Fail("Expected parsing failure for invalid UTF-8");
            }
            catch (Exception ex) when (ex.GetType().Name.Contains("Json") || ex is InvalidOperationException)
            {
                // Accept JsonException/JsonReaderException/InvalidOperationException("Cannot transcode invalid UTF-8...")
            }
        }

        [TestMethod]
        public async Task Encrypt_Fails_OnNaN_Literal()
        {
            // Arrange
            // NaN is not valid JSON literal; parsing should fail
            string json = "{\"id\":\"1\",\"SensitiveDouble\":NaN}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/SensitiveDouble" });
            try
            {
                // Act
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
                // Assert
                Assert.Fail("Expected parsing failure for NaN literal");
            }
            catch (Exception ex) when (ex.GetType().Name.Contains("Json"))
            {
                // Accept JsonException/JsonReaderException
            }
        }

        [TestMethod]
        public async Task Encrypt_NegativeZero_Double_RoundtripsAsZero()
        {
            // Arrange
            string json = "{\"id\":\"1\",\"DZ\":-0.0}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream encrypted = new();
            EncryptionOptions options = CreateOptions(new[] { "/DZ" });
            // Act (encrypt)
            await EncryptionProcessor.EncryptAsync(input, encrypted, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
            string rawJson = Encoding.UTF8.GetString(encrypted.ToArray());
            encrypted.Position = 0;
            using JsonDocument jenc = JsonDocument.Parse(encrypted);
            byte[] cipher = Convert.FromBase64String(jenc.RootElement.GetProperty("DZ").GetString());
            Assert.AreEqual((byte)TypeMarker.Double, cipher[0]);
            EncryptionVerificationTestHelper.AssertEncryptedDocument(
                rawJson,
                encryptedProperties: new Dictionary<string, object> { { "DZ", -0.0 } });

            // Act (decrypt)
            encrypted.Position = 0;
            (Stream decrypted, _) = await EncryptionProcessor.DecryptStreamAsync(encrypted, mockEncryptor.Object, new CosmosDiagnosticsContext(), CancellationToken.None);
            // Assert
            using JsonDocument jdec = JsonDocument.Parse(decrypted);
            Assert.AreEqual(0.0, jdec.RootElement.GetProperty("DZ").GetDouble(), 0.0);
        }

        [TestMethod]
        public async Task Encrypt_DeepNesting_ExceedsDepth_Fails()
        {
            // Arrange
            // Build deeply nested object under property "Obj"
            int depth = 200;
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"Obj\":");
            for (int i = 0; i < depth; i++) sb.Append("{");
            sb.Append("\"x\":1");
            for (int i = 0; i < depth; i++) sb.Append("}");
            sb.Append("}");
            string json = sb.ToString();
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/Obj" });
            try
            {
                // Act
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
                // Assert
                Assert.Fail("Expected parsing failure for deep nesting");
            }
            catch (Exception ex) when (ex.GetType().Name.Contains("Json"))
            {
                // Accept JsonException/JsonReaderException
            }
        }

        [TestMethod]
        public async Task Encrypt_PathToArray_ButValueIsString_EncryptsAsString()
        {
            // Arrange
            string json = "{\"Arr\":\"not an array\"}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/Arr" });
            // Act
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
            string rawJson = Encoding.UTF8.GetString(output.ToArray());
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            // Assert
            string base64 = jd.RootElement.GetProperty("Arr").GetString();
            byte[] cipher = Convert.FromBase64String(base64);
            Assert.AreEqual((byte)TypeMarker.String, cipher[0]);
            EncryptionVerificationTestHelper.AssertEncryptedDocument(
                rawJson,
                encryptedProperties: new Dictionary<string, object> { { "Arr", "not an array" } });
        }

        [TestMethod]
        public async Task Encrypt_PathToObject_ButValueIsNumber_EncryptsAsNumber()
        {
            // Arrange
            string json = "{\"Obj\":42}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/Obj" });
            // Act
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
            string rawJson = Encoding.UTF8.GetString(output.ToArray());
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            // Assert
            string base64 = jd.RootElement.GetProperty("Obj").GetString();
            byte[] cipher = Convert.FromBase64String(base64);
            Assert.AreEqual((byte)TypeMarker.Long, cipher[0]);
            EncryptionVerificationTestHelper.AssertEncryptedDocument(
                rawJson,
                encryptedProperties: new Dictionary<string, object> { { "Obj", 42 } });
        }

        [TestMethod]
        public async Task EncryptStreamAsync_WithNonReadableInputStream_ThrowsArgumentException()
        {
            // Arrange
            Mock<Stream> nonReadableStream = new Mock<Stream>();
            nonReadableStream.Setup(s => s.CanRead).Returns(false);
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/test" });

            // Act & Assert
            ArgumentException ex = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => new StreamProcessor().EncryptStreamAsync(
                    nonReadableStream.Object,
                    output,
                    mockEncryptor.Object,
                    options,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None));

            Assert.IsTrue(ex.Message.Contains("Input stream must be readable"));
            Assert.AreEqual("inputStream", ex.ParamName);
        }

        [TestMethod]
        public async Task EncryptStreamAsync_WithNonWritableOutputStream_ThrowsArgumentException()
        {
            // Arrange
            string json = "{\"test\":42}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            Mock<Stream> nonWritableStream = new Mock<Stream>();
            nonWritableStream.Setup(s => s.CanWrite).Returns(false);
            EncryptionOptions options = CreateOptions(new[] { "/test" });

            // Act & Assert
            ArgumentException ex = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => new StreamProcessor().EncryptStreamAsync(
                    input,
                    nonWritableStream.Object,
                    mockEncryptor.Object,
                    options,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None));

            Assert.IsTrue(ex.Message.Contains("Output stream must be writable"));
            Assert.AreEqual("outputStream", ex.ParamName);
        }

        [TestMethod]
        public async Task EncryptStreamAsync_ExceedsMaxBufferSize_ThrowsInvalidOperationException()
        {
            // Arrange - create a JSON token that exceeds max buffer size
            // Set a small max buffer size for testing
            int originalInitialBufferSize = StreamProcessor.InitialBufferSize;
            int? originalMaxBufferSize = StreamProcessor.TestMaxBufferSizeBytesOverride;
            
            try
            {
                StreamProcessor.InitialBufferSize = 8;
                StreamProcessor.TestMaxBufferSizeBytesOverride = 64; // very small limit

                // Create a JSON string property value that's larger than the max buffer
                string hugeValue = new string('x', 100); // > 64 bytes
                string json = $"{{\"huge\":\"{hugeValue}\"}}";
                using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
                MemoryStream output = new();
                EncryptionOptions options = CreateOptions(new[] { "/huge" });

                // Act & Assert
                InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => new StreamProcessor().EncryptStreamAsync(
                        input,
                        output,
                        mockEncryptor.Object,
                        options,
                        new CosmosDiagnosticsContext(),
                        CancellationToken.None));

                Assert.IsTrue(ex.Message.Contains("JSON token exceeds maximum supported size"));
                Assert.IsTrue(ex.Message.Contains("64 bytes"));
            }
            finally
            {
                // Restore original values
                StreamProcessor.InitialBufferSize = originalInitialBufferSize;
                StreamProcessor.TestMaxBufferSizeBytesOverride = originalMaxBufferSize;
            }
        }

        [TestMethod]
        public async Task Encrypt_MultiSegmentPropertyName_HandlesValueSequence()
        {
            // Arrange - create a JSON with a property name that spans multiple buffer segments
            // InitialBufferSize is 8, so a property name longer than that will cause ValueSequence
            string longPropertyName = "VeryLongPropertyNameThatWillSpanMultipleBuffers";
            string json = $"{{\"{longPropertyName}\":\"value\",\"id\":\"123\"}}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { $"/{longPropertyName}" });

            // Act
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);

            // Assert - verify the document was encrypted correctly
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.IsTrue(jd.RootElement.TryGetProperty(longPropertyName, out JsonElement encrypted));
            Assert.AreEqual(JsonValueKind.String, encrypted.ValueKind);
            
            // Verify it's actually encrypted (base64 format)
            string encryptedValue = encrypted.GetString();
            Assert.IsTrue(encryptedValue.Length > 0);
            
            // Verify the id property is still there and not encrypted
            Assert.IsTrue(jd.RootElement.TryGetProperty("id", out JsonElement idElement));
            Assert.AreEqual("123", idElement.GetString());
        }

        [TestMethod]
        public async Task Encrypt_NestedArrays_EncryptsCorrectly()
        {
            // Arrange - test nested arrays to exercise buffering logic for arrays within arrays
            var doc = new
            {
                id = "123",
                nestedArr = new object[] { new object[] { 1, 2 }, new object[] { 3, 4 }, "text" }
            };
            EncryptionOptions options = CreateOptions(new[] { "/nestedArr" });

            // Act
            MemoryStream encrypted = await EncryptAsync(doc, options);

            // Assert - verify nested array was encrypted
            encrypted.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(encrypted);
            string base64 = jd.RootElement.GetProperty("nestedArr").GetString();
            byte[] cipher = Convert.FromBase64String(base64);
            Assert.AreEqual((byte)TypeMarker.Array, cipher[0]);
        }

        [TestMethod]
        public async Task Encrypt_LongUnencryptedString_HandlesValueSequence()
        {
            // Arrange - test ValueSequence handling in pass-through (non-encrypted) path
            // With InitialBufferSize=8, a long string will span multiple segments
            string longValue = new string('x', 200);
            var doc = new
            {
                id = "123",
                encrypted = "short",
                notEncrypted = longValue
            };
            EncryptionOptions options = CreateOptions(new[] { "/encrypted" });

            // Act
            MemoryStream encrypted = await EncryptAsync(doc, options);

            // Assert - verify long unencrypted string preserved
            encrypted.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(encrypted);
            Assert.IsTrue(jd.RootElement.TryGetProperty("notEncrypted", out JsonElement notEncryptedElem));
            Assert.AreEqual(longValue, notEncryptedElem.GetString());
            
            // Verify encrypted property is actually encrypted
            Assert.IsTrue(jd.RootElement.TryGetProperty("encrypted", out JsonElement encryptedElem));
            Assert.AreNotEqual("short", encryptedElem.GetString()); // Should be base64
        }

        [TestMethod]
        public async Task Encrypt_ObjectWithLongPropertyNames_HandlesValueSequence()
        {
            // Arrange - test ValueSequence for property names within an encrypted object
            // The object itself is encrypted, and has properties with long names
            string longPropName1 = "VeryLongPropertyNameInside" + new string('A', 100);
            string longPropName2 = "AnotherLongPropertyName" + new string('B', 100);
            
            Dictionary<string, object> doc = new Dictionary<string, object>
            {
                { "id", "123" },
                { "encryptedObject", new Dictionary<string, object>
                    {
                        { longPropName1, "value1" },
                        { longPropName2, "value2" },
                        { "normal", "value3" }
                    }
                }
            };
            EncryptionOptions options = CreateOptions(new[] { "/encryptedObject" });

            // Act
            MemoryStream encrypted = await EncryptAsync(doc, options);

            // Assert - verify the object was encrypted (property names handled)
            encrypted.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(encrypted);
            string base64 = jd.RootElement.GetProperty("encryptedObject").GetString();
            byte[] cipher = Convert.FromBase64String(base64);
            Assert.AreEqual((byte)TypeMarker.Object, cipher[0]);
        }

        [TestMethod]
        public async Task Encrypt_PassThrough_EscapedString_HandlesCorrectly()
        {
            // Arrange - test escaped string handling in pass-through (non-encrypted) path
            // JSON with escaped characters that are NOT encrypted
            string jsonWithEscaped = "{\"id\":\"123\",\"encrypted\":\"simple\",\"notEncrypted\":\"Line1\\nLine2\\tTabbed\"}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(jsonWithEscaped));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/encrypted" });

            // Act
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);

            // Assert - verify escaped string preserved in pass-through
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            string notEncrypted = jd.RootElement.GetProperty("notEncrypted").GetString();
            Assert.AreEqual("Line1\nLine2\tTabbed", notEncrypted); // Properly unescaped
        }

        [TestMethod]
        public async Task Encrypt_PassThrough_LongNumber_HandlesValueSequence()
        {
            // Arrange - test number spanning multiple segments in pass-through (non-encrypted)
            // Need a very long number representation that spans buffers
            string longNumber = "123456789012345678901234567890.123456789012345678901234567890";
            string json = $"{{\"id\":\"123\",\"encrypted\":\"test\",\"longNum\":{longNumber}}}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/encrypted" });

            // Act
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);

            // Assert
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            string preserved = jd.RootElement.GetProperty("longNum").GetRawText();
            Assert.AreEqual(longNumber, preserved);
        }

        [TestMethod]
        public async Task Encrypt_BufferedObject_VeryLongPropertyName_HandlesValueSequence()
        {
            // Arrange - test property name that spans segments when buffering encrypted object
            // This tests lines 404-411 where property names in buffered objects are ValueSequence
            int originalBufferSize = StreamProcessor.InitialBufferSize;
            try
            {
                StreamProcessor.InitialBufferSize = 8; // Force multi-segment for property names
                string veryLongPropName = new string('A', 200); // 200 character property name
                string json = $"{{\"id\":\"123\",\"obj\":{{\"{veryLongPropName}\":\"value\"}}}}";
                using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
                MemoryStream output = new();
                EncryptionOptions options = CreateOptions(new[] { "/obj" });

                // Act - the entire /obj will be encrypted, including the long property name
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);

                // Assert - the encrypted object should have been processed
                output.Position = 0;
                using JsonDocument jd = JsonDocument.Parse(output);
                Assert.IsTrue(jd.RootElement.TryGetProperty("obj", out JsonElement objElem));
                Assert.AreEqual(JsonValueKind.String, objElem.ValueKind); // Encrypted as base64 string
            }
            finally
            {
                StreamProcessor.InitialBufferSize = originalBufferSize;
            }
        }

        [TestMethod]
        public async Task Encrypt_FragmentedStream_PropertyNameValueSequence_HandlesCorrectly()
        {
            // Arrange - force property names to span buffer chunks triggering HasValueSequence
            int originalBufferSize = StreamProcessor.InitialBufferSize;
            try
            {
                StreamProcessor.InitialBufferSize = 16; // Small but not too small
                string longPropName = new string('X', 30); // Long property name
                string json = $"{{\"obj\":{{\"{longPropName}\":\"value\"}}}}";
                
                // TinyChunkStream returns 4 bytes at a time, forcing property name to span chunks
                using TinyChunkStream input = new TinyChunkStream(Encoding.UTF8.GetBytes(json), maxChunkSize: 4);
                MemoryStream output = new();
                EncryptionOptions options = CreateOptions(new[] { "/obj" });

                // Act - should handle property names that are ValueSequence (lines 404-411)
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);

                // Assert
                output.Position = 0;
                using JsonDocument jd = JsonDocument.Parse(output);
                Assert.IsTrue(jd.RootElement.TryGetProperty("obj", out JsonElement objElem));
                Assert.AreEqual(JsonValueKind.String, objElem.ValueKind); // Encrypted
            }
            finally
            {
                StreamProcessor.InitialBufferSize = originalBufferSize;
            }
        }

        [TestMethod]
        public async Task Encrypt_FragmentedStream_NumberValueSequenceEncrypted_HandlesCorrectly()
        {
            // Arrange - force number to span chunks when encrypting (lines 455-460)
            int originalBufferSize = StreamProcessor.InitialBufferSize;
            try
            {
                StreamProcessor.InitialBufferSize = 16;
                string json = "{\"num\":123456.789012}"; // Number to encrypt
                
                using TinyChunkStream input = new TinyChunkStream(Encoding.UTF8.GetBytes(json), maxChunkSize: 3);
                MemoryStream output = new();
                EncryptionOptions options = CreateOptions(new[] { "/num" });

                // Act - should handle numbers with ValueSequence when encrypting (calls CopySequenceToScratch)
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);

                // Assert
                output.Position = 0;
                using JsonDocument jd = JsonDocument.Parse(output);
                Assert.IsTrue(jd.RootElement.TryGetProperty("num", out JsonElement numElem));
                Assert.AreEqual(JsonValueKind.String, numElem.ValueKind); // Encrypted as base64
            }
            finally
            {
                StreamProcessor.InitialBufferSize = originalBufferSize;
            }
        }

        [TestMethod]
        public async Task Encrypt_FragmentedStream_NumberValueSequencePassThrough_HandlesCorrectly()
        {
            // Arrange - force number to span chunks in pass-through mode (lines 477-482)
            int originalBufferSize = StreamProcessor.InitialBufferSize;
            try
            {
                StreamProcessor.InitialBufferSize = 16;
                string json = "{\"encrypted\":\"test\",\"num\":987654.321098}"; // num NOT encrypted
                
                using TinyChunkStream input = new TinyChunkStream(Encoding.UTF8.GetBytes(json), maxChunkSize: 3);
                MemoryStream output = new();
                EncryptionOptions options = CreateOptions(new[] { "/encrypted" });

                // Act - number should pass through with ValueSequence handling
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);

                // Assert
                output.Position = 0;
                using JsonDocument jd = JsonDocument.Parse(output);
                Assert.AreEqual(987654.321098, jd.RootElement.GetProperty("num").GetDouble(), 0.000001);
            }
            finally
            {
                StreamProcessor.InitialBufferSize = originalBufferSize;
            }
        }

        [TestMethod]
        public void Debug_VerifyHasValueSequenceBehavior()
        {
            // This test verifies whether Utf8JsonReader constructed from span can have HasValueSequence=true
            byte[] json = Encoding.UTF8.GetBytes("{\"veryLongPropertyNameThatMightSpanBuffers\":12345.67890}");
            
            // Create reader from span (like StreamProcessor does)
            Utf8JsonReader reader = new Utf8JsonReader(json, isFinalBlock: true, default);
            bool hasSeenValueSequence = false;
            
            while (reader.Read())
            {
                if (reader.HasValueSequence)
                {
                    hasSeenValueSequence = true;
                    System.Diagnostics.Debug.WriteLine($"HasValueSequence=true for {reader.TokenType}");
                }
            }
            
            // This test documents that span-based readers NEVER have HasValueSequence=true
            Assert.IsFalse(hasSeenValueSequence, "Span-based Utf8JsonReader should never have HasValueSequence=true");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task EncryptAsync_NonReadableInputStream_ThrowsArgumentException()
        {
            // Arrange - create a write-only stream
            using MemoryStream writeOnlyStream = new WriteOnlyMemoryStream();
            using MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/test" });

            // Act - should throw ArgumentException for non-readable input
            await EncryptionProcessor.EncryptAsync(writeOnlyStream, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task EncryptAsync_NonWritableOutputStream_ThrowsArgumentException()
        {
            // Arrange - create a read-only stream
            using MemoryStream input = new(Encoding.UTF8.GetBytes("{\"id\":\"1\"}"));
            using MemoryStream readOnlyStream = new ReadOnlyMemoryStream(new byte[100]);
            EncryptionOptions options = CreateOptions(new[] { "/test" });

            // Act - should throw ArgumentException for non-writable output
            await EncryptionProcessor.EncryptAsync(input, readOnlyStream, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task EncryptAsync_NullInputStream_ThrowsArgumentNullException()
        {
            // Arrange
            using MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/test" });

            // Act - should throw ArgumentNullException
            await EncryptionProcessor.EncryptAsync(null, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task EncryptAsync_NullOutputStream_ThrowsArgumentNullException()
        {
            // Arrange
            using MemoryStream input = new(Encoding.UTF8.GetBytes("{\"id\":\"1\"}"));
            EncryptionOptions options = CreateOptions(new[] { "/test" });

            // Act - should throw ArgumentNullException
            await EncryptionProcessor.EncryptAsync(input, null, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task EncryptAsync_NullEncryptor_ThrowsArgumentNullException()
        {
            // Arrange
            using MemoryStream input = new(Encoding.UTF8.GetBytes("{\"id\":\"1\"}"));
            using MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/test" });

            // Act - should throw ArgumentNullException
            await EncryptionProcessor.EncryptAsync(input, output, null, options, new CosmosDiagnosticsContext(), CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task EncryptAsync_NullOptions_ThrowsArgumentNullException()
        {
            // Arrange
            using MemoryStream input = new(Encoding.UTF8.GetBytes("{\"id\":\"1\"}"));
            using MemoryStream output = new();

            // Act - should throw ArgumentNullException
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, null, new CosmosDiagnosticsContext(), CancellationToken.None);
        }

        /// <summary>
        /// Helper stream that is write-only (CanRead=false) for testing validation.
        /// </summary>
        private class WriteOnlyMemoryStream : MemoryStream
        {
            public override bool CanRead => false;
        }

        /// <summary>
        /// Helper stream that is read-only (CanWrite=false) for testing validation.
        /// </summary>
        private class ReadOnlyMemoryStream : MemoryStream
        {
            public ReadOnlyMemoryStream(byte[] buffer) : base(buffer)
            {
            }

            public override bool CanWrite => false;
        }

        [TestMethod]
        public async Task Encrypt_RootArray_NoOpWhenNoPaths()
        {
            string json = "[ { \"id\": \"1\", \"Secret\": \"abc\" } ]";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(Array.Empty<string>());
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);

            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            Assert.AreEqual(JsonValueKind.Array, jd.RootElement.ValueKind);
            string roundTripped = jd.RootElement.GetRawText();
            using JsonDocument expected = JsonDocument.Parse(json);
            Assert.AreEqual(expected.RootElement.GetRawText(), roundTripped, "Root array should be preserved when no paths are encrypted.");
        }

        [TestMethod]
        public async Task Encrypt_PrimitiveRoot_NoOpWhenNoPaths()
        {
            foreach (string json in new[] { "123", "\"str\"", "true", "false", "null" })
            {
                using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
                MemoryStream output = new();
                EncryptionOptions options = CreateOptions(Array.Empty<string>());
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);

                output.Position = 0;
                using JsonDocument jd = JsonDocument.Parse(output);
                using JsonDocument expected = JsonDocument.Parse(json);
                Assert.AreEqual(expected.RootElement.ValueKind, jd.RootElement.ValueKind, $"Primitive root {json} should be preserved.");
            }
        }
    }
}
#endif
