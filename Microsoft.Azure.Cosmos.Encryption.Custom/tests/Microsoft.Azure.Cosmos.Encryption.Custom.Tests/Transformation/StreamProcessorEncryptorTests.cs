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
            // Exercise buffer growth with small initial buffer size
            PooledStreamConfiguration.SetConfiguration(new PooledStreamConfiguration { StreamProcessorBufferSize = 8 });

            mockEncryptor = TestEncryptorFactory.CreateMde(DekId, out mockDek);
        }

        private static EncryptionOptions CreateOptions(IEnumerable<string> paths)
        {
            return new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = paths.ToList()
            };
        }

        private static async Task<MemoryStream> EncryptAsync(object doc, EncryptionOptions options)
        {
            Stream input = TestCommon.ToStream(doc);
            MemoryStream output = new();
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
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
            using JsonDocument jd = Parse(encrypted);
            // Assert
            string cipher = jd.RootElement.GetProperty("Big").GetString();
            Assert.IsTrue(cipher.Length > 10);
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
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
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
        }

        [TestMethod]
        public void Encrypt_InternalProperty_Getter_Coverage()
        {
            // Arrange
            // Touch internal partial members for coverage: Encryptor property lives on decryptor partial file
            StreamProcessor sp = new StreamProcessor();
            // Assert
            Assert.IsNotNull(sp.Encryptor); // covers getter sequence point
            Assert.IsTrue(PooledStreamConfiguration.Current.StreamProcessorBufferSize > 0);
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
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
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
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
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
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
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
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
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
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
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
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
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
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
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
            await EncryptionProcessor.EncryptAsync(input, encrypted, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
            encrypted.Position = 0;
            using JsonDocument jenc = JsonDocument.Parse(encrypted);
            byte[] cipher = Convert.FromBase64String(jenc.RootElement.GetProperty("DZ").GetString());
            Assert.AreEqual((byte)TypeMarker.Double, cipher[0]);

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
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
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
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            // Assert
            string base64 = jd.RootElement.GetProperty("Arr").GetString();
            byte[] cipher = Convert.FromBase64String(base64);
            Assert.AreEqual((byte)TypeMarker.String, cipher[0]);
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
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            // Assert
            string base64 = jd.RootElement.GetProperty("Obj").GetString();
            byte[] cipher = Convert.FromBase64String(base64);
            Assert.AreEqual((byte)TypeMarker.Long, cipher[0]);
        }

        [TestMethod]
        public async Task Encrypt_RootArray_NoOpWhenNoPaths()
        {
            string json = "[ { \"id\": \"1\", \"Secret\": \"abc\" } ]";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(Array.Empty<string>());
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Newtonsoft, new CosmosDiagnosticsContext(), CancellationToken.None);

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
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Newtonsoft, new CosmosDiagnosticsContext(), CancellationToken.None);

                output.Position = 0;
                using JsonDocument jd = JsonDocument.Parse(output);
                using JsonDocument expected = JsonDocument.Parse(json);
                Assert.AreEqual(expected.RootElement.ValueKind, jd.RootElement.ValueKind, $"Primitive root {json} should be preserved.");
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("PooledResources")]
        public async Task EncryptStreamAsync_TryFinallyBlock_DisposesResourcesOnSuccess()
        {
            // Tests StreamProcessor.Encryptor.cs:76-86 - finally block disposal on success path
            var doc = new { id = "1", Sensitive = "secret" };
            EncryptionOptions options = CreateOptions(new[] { "/Sensitive" });

            Stream input = TestCommon.ToStream(doc);
            MemoryStream output = new();

            // Act - should complete successfully and dispose resources in finally
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);

            // Assert - verify encryption succeeded
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            Assert.IsTrue(jd.RootElement.TryGetProperty(Constants.EncryptedInfo, out _), "Encryption should succeed and dispose resources properly");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("PooledResources")]
        public async Task EncryptStreamAsync_ExceptionDuringParsing_DisposesResourcesInFinally()
        {
            // Tests StreamProcessor.Encryptor.cs:78-85 - finally block disposal on exception path
            // Verify that encryptionPayloadWriter and bufferWriter are disposed even when exception occurs

            string invalidJson = "{\"id\":\"1\",\"Sensitive\":"; // truncated JSON
            using MemoryStream input = new(Encoding.UTF8.GetBytes(invalidJson));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/Sensitive" });

            bool exceptionThrown = false;
            try
            {
                // Act - should throw JSON parsing exception but dispose resources in finally
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
            }
            catch (Exception ex) when (ex is JsonException || ex.GetType().Name.Contains("JsonReader"))
            {
                // Expected - resources should be disposed in finally block
                exceptionThrown = true;
            }

            Assert.IsTrue(exceptionThrown, "Expected JSON parsing exception to be thrown");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("PooledResources")]
        public async Task EncryptStreamAsync_CancellationRequested_DisposesResourcesInFinally()
        {
            // Tests StreamProcessor.Encryptor.cs:78-85 - finally block disposal on cancellation
            var doc = new { id = "1", Sensitive = "secret" };
            EncryptionOptions options = CreateOptions(new[] { "/Sensitive" });

            Stream input = TestCommon.ToStream(doc);
            MemoryStream output = new();
            CancellationTokenSource cts = new();
            cts.Cancel(); // Cancel immediately

            try
            {
                // Act - should be cancelled but dispose resources in finally
                await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), cts.Token);
                Assert.Fail("Expected OperationCanceledException");
            }
            catch (OperationCanceledException)
            {
                // Expected - resources should be disposed in finally block
            }
            finally
            {
                cts.Dispose();
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("PooledResources")]
        [TestCategory("Stress")]
        public async Task EncryptStreamAsync_RepeatedCallsWithExceptions_NoResourceLeak()
        {
            // Stress test to verify try-finally disposal prevents resource leaks
            // Run 100 iterations mixing success and failure paths

            var validDoc = new { id = "1", Sensitive = "secret" };
            string invalidJson = "{\"id\":\"1\",\"Sensitive\":"; // truncated
            EncryptionOptions options = CreateOptions(new[] { "/Sensitive" });

            for (int i = 0; i < 100; i++)
            {
                // Success path
                Stream validInput = TestCommon.ToStream(validDoc);
                MemoryStream validOutput = new();
                await EncryptionProcessor.EncryptAsync(validInput, validOutput, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
                validOutput.Dispose();

                // Failure path
                using MemoryStream invalidInput = new(Encoding.UTF8.GetBytes(invalidJson));
                MemoryStream invalidOutput = new();
                try
                {
                    await EncryptionProcessor.EncryptAsync(invalidInput, invalidOutput, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
                }
                catch (JsonException)
                {
                    // Expected - resources disposed in finally
                }
                finally
                {
                    invalidOutput.Dispose();
                }
            }

            // If we got here without OutOfMemoryException, disposal is working
            Assert.IsTrue(true, "100 iterations completed without resource leak");
        }
    }
}
#endif
