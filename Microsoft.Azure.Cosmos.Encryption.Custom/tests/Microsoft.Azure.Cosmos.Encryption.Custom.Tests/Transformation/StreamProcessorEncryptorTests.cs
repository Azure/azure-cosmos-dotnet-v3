//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
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
            StreamProcessor.InitialBufferSize = 8; // exercise buffer growth

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

        private static EncryptionOptions CreateOptions(IEnumerable<string> paths, CompressionOptions.CompressionAlgorithm algorithm = CompressionOptions.CompressionAlgorithm.None, CompressionLevel compressionLevel = CompressionLevel.NoCompression, int? minCompressedLength = null)
        {
            CompressionOptions comp = new CompressionOptions { Algorithm = algorithm, CompressionLevel = compressionLevel };
            if (minCompressedLength.HasValue)
            {
                comp.MinimalCompressedLength = minCompressedLength.Value;
            }
            return new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                JsonProcessor = JsonProcessor.Stream,
                PathsToEncrypt = paths.ToList(),
                CompressionOptions = comp
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

            MemoryStream encrypted = await EncryptAsync(doc, options);
            using JsonDocument jd = Parse(encrypted);
            JsonElement root = jd.RootElement;

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

            // Decrypt roundtrip to verify values
            encrypted.Position = 0;
            (Stream decrypted, DecryptionContext ctx) = await EncryptionProcessor.DecryptStreamAsync(encrypted, mockEncryptor.Object, new CosmosDiagnosticsContext(), CancellationToken.None);
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
        public async Task Encrypt_CompressionBehavior()
        {
            var doc = new
            {
                id = "1",
                LargeStr = new string('x', 400),
                SmallStr = new string('y', 10)
            };
            var paths = new[] { "/LargeStr", "/SmallStr" };
            EncryptionOptions options = CreateOptions(paths, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest, minCompressedLength: 64);
            MemoryStream encrypted = await EncryptAsync(doc, options);
            using JsonDocument jd = Parse(encrypted);
            JsonElement propsJson = jd.RootElement.GetProperty(Constants.EncryptedInfo);
            EncryptionProperties props = System.Text.Json.JsonSerializer.Deserialize<EncryptionProperties>(propsJson.GetRawText());
            Assert.AreEqual(EncryptionFormatVersion.MdeWithCompression, props.EncryptionFormatVersion);
            Assert.IsTrue(props.CompressedEncryptedPaths.ContainsKey("/LargeStr"));
            Assert.IsFalse(props.CompressedEncryptedPaths.ContainsKey("/SmallStr"));
        }

        [TestMethod]
        public async Task Encrypt_NestedObjectAndArray()
        {
            var doc = new
            {
                id = "1",
                Nested = new { a = 5, b = new { c = 10 } },
                Arr = new object[] { new { x = 1 }, new { x = 2 } },
                Plain = 7
            };
            var paths = new[] { "/Nested", "/Arr" };
            MemoryStream encrypted = await EncryptAsync(doc, CreateOptions(paths));
            using JsonDocument jd = Parse(encrypted);
            JsonElement root = jd.RootElement;
            Assert.AreEqual(JsonValueKind.String, root.GetProperty("Nested").ValueKind);
            Assert.AreEqual(JsonValueKind.String, root.GetProperty("Arr").ValueKind);

            // Roundtrip
            encrypted.Position = 0;
            (Stream decrypted, _) = await EncryptionProcessor.DecryptStreamAsync(encrypted, mockEncryptor.Object, new CosmosDiagnosticsContext(), CancellationToken.None);
            using JsonDocument d2 = Parse(decrypted);
            JsonElement r2 = d2.RootElement;
            Assert.AreEqual(5, r2.GetProperty("Nested").GetProperty("a").GetInt32());
            Assert.AreEqual(2, r2.GetProperty("Arr").GetArrayLength());
        }

        [TestMethod]
        public async Task Encrypt_BufferGrowthLargeString()
        {
            var doc = new { id = "1", Big = new string('a', 5000) };
            var paths = new[] { "/Big" };
            MemoryStream encrypted = await EncryptAsync(doc, CreateOptions(paths));
            using JsonDocument jd = Parse(encrypted);
            string cipher = jd.RootElement.GetProperty("Big").GetString();
            Assert.IsTrue(cipher.Length > 10);
        }

        [TestMethod]
        public async Task Encrypt_SkipsNullProperty()
        {
            var doc = new { id = "1", Maybe = (string)null };
            var paths = new[] { "/Maybe" };
            MemoryStream encrypted = await EncryptAsync(doc, CreateOptions(paths));
            using JsonDocument jd = Parse(encrypted);
            JsonElement root = jd.RootElement;
            EncryptionProperties props = System.Text.Json.JsonSerializer.Deserialize<EncryptionProperties>(root.GetProperty(Constants.EncryptedInfo).GetRawText());
            Assert.IsFalse(props.EncryptedPaths.Contains("/Maybe"));
            Assert.AreEqual(JsonValueKind.Null, root.GetProperty("Maybe").ValueKind);
        }

        [TestMethod]
        public void Encrypt_InternalProperty_Getter_Coverage()
        {
            // Touch internal partial members for coverage: Encryptor property lives on decryptor partial file
            var sp = new StreamProcessor();
            Assert.IsNotNull(sp.Encryptor); // covers getter sequence point
            Assert.IsTrue(StreamProcessor.InitialBufferSize > 0);
        }

        [TestMethod]
        public async Task Encrypt_UnsupportedNumberFormat_Throws()
        {
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
                var paths = new[] { "/Weird" };
                EncryptionOptions options = CreateOptions(paths);

                InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await EncryptAsync(doc, options));
                StringAssert.Contains(ex.Message, "Unsupported Number type");
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
            // Although StreamProcessor has a comment case, JsonReaderOptions uses CommentHandling=Skip, so comments are not surfaced as tokens.
            // This test documents that behavior: comments are silently dropped and encryption still succeeds.
            string json = "{\n  // comment before sensitive\n  \"SensitiveStr\": \"abc\",\n  // trailing comment\n  \"id\": \"1\"\n}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            var options = CreateOptions(new[] { "/SensitiveStr" });
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            using JsonDocument jd = Parse(output);
            JsonElement root = jd.RootElement;
            // SensitiveStr should be encrypted (string -> base64), id should remain plain, no comments present.
            string cipher = root.GetProperty("SensitiveStr").GetString();
            Assert.IsNotNull(cipher);
            Assert.AreEqual("1", root.GetProperty("id").GetString());
            // Ensure encrypted info present
            Assert.IsTrue(root.TryGetProperty(Constants.EncryptedInfo, out _));
        }
    }
}
#endif
