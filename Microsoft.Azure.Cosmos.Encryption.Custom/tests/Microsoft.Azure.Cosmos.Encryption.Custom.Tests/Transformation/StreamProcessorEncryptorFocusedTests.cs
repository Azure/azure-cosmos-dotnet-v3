//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
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
    /// Focused additional coverage for StreamProcessor encryption per request:
    ///  - Encrypted containers with nested content; metadata emission ordering
    ///  - Multi-segment (ValueSequence) tokens for strings and numbers
    ///  - Nulls inside encrypted containers round-trip (not individually marked)
    /// </summary>
    [TestClass]
    public class StreamProcessorEncryptorFocusedTests
    {
        private const string DekId = "dekId";
        private static Mock<Encryptor> mockEncryptor;
        private static Mock<DataEncryptionKey> mockDek;

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            _ = ctx;
            // Small buffer to force multi-segment ValueSequence for long tokens.
            StreamProcessor.InitialBufferSize = 8;

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

        private static EncryptionOptions CreateOptions(string[] paths)
            => new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                JsonProcessor = JsonProcessor.Stream,
                PathsToEncrypt = paths,
            };

        private static async Task<MemoryStream> EncryptAsync(string json, EncryptionOptions options)
        {
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            return output;
        }

        [TestMethod]
        public async Task Encrypt_EncryptedContainer_MetadataAppearsAfterOtherProperties()
        {
            string json = "{\"EncryptedObj\":{\"a\":1,\"b\":[{\"c\":2},{\"d\":3}]},\"tail\":5}";
            MemoryStream encrypted = await EncryptAsync(json, CreateOptions(new[] { "/EncryptedObj" }));
            string text = Encoding.UTF8.GetString(encrypted.ToArray());
            // Ensure tail property serialized before _ei and encrypted object value is a string (base64).
            int tailIdx = text.IndexOf("\"tail\":5", StringComparison.Ordinal);
            int eiIdx = text.IndexOf("\"_ei\"", StringComparison.Ordinal);
            Assert.IsTrue(tailIdx >= 0 && eiIdx > tailIdx, "_ei must appear after tail property at root end");
            using JsonDocument jd = JsonDocument.Parse(encrypted);
            JsonElement root = jd.RootElement;
            Assert.AreEqual(JsonValueKind.String, root.GetProperty("EncryptedObj").ValueKind);
            // Decrypt roundtrip
            encrypted.Position = 0;
            (Stream dec, _) = await EncryptionProcessor.DecryptStreamAsync(encrypted, mockEncryptor.Object, new CosmosDiagnosticsContext(), CancellationToken.None);
            using JsonDocument d2 = JsonDocument.Parse(dec);
            JsonElement r2 = d2.RootElement;
            Assert.AreEqual(1, r2.GetProperty("EncryptedObj").GetProperty("a").GetInt32());
            Assert.AreEqual(2, r2.GetProperty("EncryptedObj").GetProperty("b").GetArrayLength());
            Assert.AreEqual(5, r2.GetProperty("tail").GetInt32());
        }

        [TestMethod]
        public async Task Encrypt_MultiSegment_String_And_Number()
        {
            // Long string and a long numeric literal (19 digits fits Int64) both larger than initial buffer => ValueSequence
            string longStr = new string('x', 200);
            string longNum = "922337203685477580"; // 18 digits within long range, length > buffer size
            string json = $"{{\"BigStr\":\"{longStr}\",\"BigNum\":{longNum}}}";
            MemoryStream encrypted = await EncryptAsync(json, CreateOptions(new[] { "/BigStr", "/BigNum" }));
            using JsonDocument jd = JsonDocument.Parse(encrypted);
            JsonElement root = jd.RootElement;
            string strCipher = root.GetProperty("BigStr").GetString();
            string numCipher = root.GetProperty("BigNum").GetString();
            Assert.IsNotNull(strCipher);
            Assert.IsNotNull(numCipher);
            byte[] strBytes = Convert.FromBase64String(strCipher);
            byte[] numBytes = Convert.FromBase64String(numCipher);
            Assert.AreEqual((byte)TypeMarker.String, strBytes[0], "Marker for string");
            // Depending on parsing, number could be Long or Double; ensure one of those markers.
            Assert.IsTrue(numBytes[0] == (byte)TypeMarker.Long || numBytes[0] == (byte)TypeMarker.Double, "Marker must be numeric");
            // Roundtrip to verify full correctness
            encrypted.Position = 0;
            (Stream dec, _) = await EncryptionProcessor.DecryptStreamAsync(encrypted, mockEncryptor.Object, new CosmosDiagnosticsContext(), CancellationToken.None);
            using JsonDocument d2 = JsonDocument.Parse(dec);
            Assert.AreEqual(longStr, d2.RootElement.GetProperty("BigStr").GetString());
            Assert.AreEqual(longNum, d2.RootElement.GetProperty("BigNum").GetRawText());
        }

        [TestMethod]
        public async Task Encrypt_NullInsideEncryptedContainer_Roundtrips()
        {
            string json = "{\"EncObj\":{\"a\":null,\"b\":5},\"id\":\"1\"}";
            MemoryStream encrypted = await EncryptAsync(json, CreateOptions(new[] { "/EncObj" }));
            using JsonDocument jd = JsonDocument.Parse(encrypted);
            Assert.AreEqual(JsonValueKind.String, jd.RootElement.GetProperty("EncObj").ValueKind);
            encrypted.Position = 0;
            (Stream dec, _) = await EncryptionProcessor.DecryptStreamAsync(encrypted, mockEncryptor.Object, new CosmosDiagnosticsContext(), CancellationToken.None);
            using JsonDocument d2 = JsonDocument.Parse(dec);
            JsonElement obj = d2.RootElement.GetProperty("EncObj");
            Assert.AreEqual(JsonValueKind.Null, obj.GetProperty("a").ValueKind);
            Assert.AreEqual(5, obj.GetProperty("b").GetInt32());
        }

        [TestMethod]
        public async Task Encrypt_LargeNestedContainers_BufferingAndMetrics()
        {
            // Build a large nested object and array to exercise full-container buffering logic.
            StringBuilder bigObj = new();
            bigObj.Append("{\"a\":1");
            for (int i = 0; i < 50; i++)
            {
                bigObj.Append(",\"p").Append(i).Append("\":").Append(i);
            }
            bigObj.Append(",\"inner\":{\"x\":123,\"y\":\"text\"}}");

            // Large array of small objects
            StringBuilder bigArr = new();
            bigArr.Append("[");
            for (int i = 0; i < 60; i++)
            {
                if (i > 0)
                {
                    bigArr.Append(',');
                }
                bigArr.Append("{\"i\":").Append(i).Append("}");
            }
            bigArr.Append("]");

            string json = $"{{\"BigObj\":{bigObj},\"BigArr\":{bigArr},\"id\":\"1\"}}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new();
            EncryptionOptions options = CreateOptions(new[] { "/BigObj", "/BigArr" });
            CosmosDiagnosticsContext diag = new CosmosDiagnosticsContext();
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, diag, CancellationToken.None);
            output.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(output);
            JsonElement root = jd.RootElement;
            Assert.AreEqual(JsonValueKind.String, root.GetProperty("BigObj").ValueKind, "Encrypted object should serialize as base64 string value");
            Assert.AreEqual(JsonValueKind.String, root.GetProperty("BigArr").ValueKind, "Encrypted array should serialize as base64 string value");
            System.Collections.Generic.IReadOnlyDictionary<string, long> metrics = diag.GetMetricsSnapshot();
            Assert.AreEqual(2L, metrics["encrypt.propertiesEncrypted"], "Two encrypted container properties expected");
            // Decrypt to validate roundtrip
            output.Position = 0;
            (Stream dec, _) = await EncryptionProcessor.DecryptStreamAsync(output, mockEncryptor.Object, new CosmosDiagnosticsContext(), CancellationToken.None);
            using JsonDocument d2 = JsonDocument.Parse(dec);
            Assert.AreEqual(52, d2.RootElement.GetProperty("BigObj").EnumerateObject().Count(), "Property count inside BigObj matches (a + p0..p49 + inner)");
            Assert.AreEqual(60, d2.RootElement.GetProperty("BigArr").GetArrayLength());
        }
    }
}
#endif