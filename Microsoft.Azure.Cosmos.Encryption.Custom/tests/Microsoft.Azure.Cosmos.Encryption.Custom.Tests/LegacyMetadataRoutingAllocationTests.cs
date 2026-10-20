//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
#if NET8_0_OR_GREATER
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
#endif

    [TestClass]
    public class LegacyMetadataRoutingAllocationTests
    {
#pragma warning disable CS0618
        private const string Legacy = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized;
#pragma warning restore CS0618
        private const string KeyId = "routing-key";

        public TestContext TestContext { get; set; }

        private static string Metadata(string data)
            => "{\"_ef\":2,\"_ea\":\"" + Legacy + "\",\"_en\":\"" + KeyId + "\",\"_ed\":" + data + ",\"_ep\":[\"/secret\"]}";

        private static string Document(string data) => "{\"id\":\"i\",\"_ei\":" + Metadata(data) + "}";

        public static IEnumerable<object[]> MalformedCipherCases()
        {
            foreach (JsonProcessor processor in Processors())
            {
                foreach (string data in new[] { "\"%%%\"", "\"A\"", "\"A===\"", "\"\\uD800\"", "\"INVALID_UTF8\"" })
                {
                    yield return new object[] { (int)processor, data };
                }
            }
        }

        private static IEnumerable<JsonProcessor> Processors()
        {
            yield return JsonProcessor.Newtonsoft;
#if NET8_0_OR_GREATER
            yield return JsonProcessor.Stream;
#endif
        }

        private static byte[] Payload(string data)
        {
            byte[] payload = Encoding.UTF8.GetBytes(Document(data));
            if (data == "\"INVALID_UTF8\"")
            {
                int offset = Document(data).IndexOf("INVALID_UTF8", StringComparison.Ordinal);
                payload[offset] = 0xFF;
            }

            return payload;
        }

        [DataTestMethod]
        [DynamicData(nameof(MalformedCipherCases), DynamicDataSourceType.Method)]
        public async Task MalformedLegacyCipher_RejectsBeforeCryptoAndPreservesInput(int processor, string data)
        {
            byte[] payload = Payload(data);
            Mock<Encryptor> encryptor = new (MockBehavior.Strict);
            using MemoryStream input = new (payload);
            Exception failure = null;
            try
            {
                await EncryptionProcessor.DecryptAsync(
                    input, encryptor.Object, new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            Assert.IsNotNull(failure, "Malformed base64 must not become plaintext or reach crypto.");
            this.TestContext.WriteLine("OPT_PROOF " + Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                processor,
                data,
                error = failure.GetType().FullName,
                message = failure.Message,
                innerError = failure.InnerException?.GetType().FullName,
                inputPosition = input.Position,
            }));
            // Legacy conversion errors come from the Newtonsoft fallback.
            Assert.IsTrue(
                failure is FormatException || failure is Newtonsoft.Json.JsonSerializationException ||
                failure is InvalidOperationException, failure.ToString());
            Assert.IsTrue(input.CanRead);
            Assert.AreEqual(0, input.Position);
            CollectionAssert.AreEqual(payload, input.ToArray());
            encryptor.VerifyNoOtherCalls();
        }

#if NET8_0_OR_GREATER
        [DataTestMethod]
        [DataRow("\"AA==\"")]
        [DataRow("\"\\u0041A==\"")]
        [DataRow("\"%%%\"")]
        [DataRow("\"A\"")]
        [DataRow("\"A===\"")]
        [DataRow("\"\\uD800\"")]
        [DataRow("\"INVALID_UTF8\"")]
        public async Task ReadResult_LegacyDoesNotDecodeUnusedCipherOrMaterializeProperties(string data)
        {
            using MemoryStream input = new (Payload(data));
            EncryptionPropertiesStreamReader.EncryptionMetadataReadResult result =
                await EncryptionPropertiesStreamReader.ReadResultAsync(
                    input, PooledJsonSerializer.SerializerOptions, CancellationToken.None);
            Assert.AreEqual(EncryptionMetadataDisposition.Legacy, result.Disposition);
            Assert.IsNull(result.Properties);
            Assert.AreEqual(0, input.Position);
            Assert.IsTrue(input.CanRead);
        }

        [DataTestMethod]
        [DataRow("\"AA==\"")]
        [DataRow("\"\\u0041A==\"")]
        public async Task ReadAsync_LegacyStillMaterializesKeyAndDecodedCipher(string data)
        {
            using MemoryStream input = new (Payload(data));
            EncryptionProperties properties = await EncryptionPropertiesStreamReader.ReadAsync(
                input, PooledJsonSerializer.SerializerOptions, CancellationToken.None);
            Assert.AreEqual(KeyId, properties.DataEncryptionKeyId);
            Assert.AreEqual(Legacy, properties.EncryptionAlgorithm);
            CollectionAssert.AreEqual(new byte[] { 0 }, properties.EncryptedData);
            Assert.AreEqual(0, input.Position);
        }

        [DataTestMethod]
        [DataRow("\"%%%\"")]
        [DataRow("\"A\"")]
        [DataRow("\"A===\"")]
        public async Task ReadAsync_LegacyRawInspectionStillRejectsInvalidBase64(string data)
        {
            using MemoryStream input = new (Payload(data));
            InvalidOperationException failure = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await EncryptionPropertiesStreamReader.ReadAsync(
                    input, PooledJsonSerializer.SerializerOptions, CancellationToken.None));
            Assert.IsInstanceOfType(failure.InnerException, typeof(System.Text.Json.JsonException));
            Assert.AreEqual(0, input.Position);
            Assert.IsTrue(input.CanRead);
        }

        [DataTestMethod]
        [DataRow("\"\"")]
        [DataRow("null")]
        [DataRow("0")]
        [DataRow("true")]
        [DataRow("[]")]
        [DataRow("{}")]
        public async Task ReadResult_EmptyOrWrongCipherTypeRemainsInvalid(string data)
        {
            using MemoryStream input = new (Payload(data));
            EncryptionPropertiesStreamReader.EncryptionMetadataReadResult result =
                await EncryptionPropertiesStreamReader.ReadResultAsync(
                    input, PooledJsonSerializer.SerializerOptions, CancellationToken.None);
            Assert.AreEqual(EncryptionMetadataDisposition.Invalid, result.Disposition);
            Assert.IsNull(result.Properties);
            Assert.AreEqual(0, input.Position);
        }

        [DataTestMethod]
        [DataRow(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized, false)]
        [DataRow("unknown-algorithm", true)]
        public async Task ReadResult_NonLegacyStillMaterializesProperties(string algorithm, bool unsupported)
        {
            string metadata = "{\"_ef\":3,\"_ea\":\"" + algorithm + "\",\"_en\":\"" + KeyId + "\",\"_ep\":[]}";
            using MemoryStream input = new (Encoding.UTF8.GetBytes("{\"_ei\":" + metadata + "}"));
            EncryptionPropertiesStreamReader.EncryptionMetadataReadResult result =
                await EncryptionPropertiesStreamReader.ReadResultAsync(
                    input, PooledJsonSerializer.SerializerOptions, CancellationToken.None);
            Assert.AreEqual(unsupported ? EncryptionMetadataDisposition.Unsupported : EncryptionMetadataDisposition.Mde, result.Disposition);
            Assert.IsNotNull(result.Properties);
            Assert.AreEqual(algorithm, result.Properties.EncryptionAlgorithm);
            if (unsupported)
            {
                SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
                NotSupportedException failure = await Assert.ThrowsExceptionAsync<NotSupportedException>(
                    async () => await adapter.DecryptAsync(
                        input, new Mock<Encryptor>(MockBehavior.Strict).Object,
                        new CosmosDiagnosticsContext(), CancellationToken.None));
                StringAssert.Contains(failure.Message, algorithm);
                Assert.AreEqual(0, input.Position);
            }
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task ReadResult_DuplicateMetadataUsesLastValueAcrossChunks(bool lastNull)
        {
            string first = lastNull ? Metadata("\"\\uD800\"") :
                "{\"_ef\":3,\"_ea\":\"" + CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized + "\",\"_en\":\"mde\",\"_ep\":[]}";
            string json = "{\"_ei\":" + first + ",\"filler\":\"" + new string('x', 20000) +
                "\",\"_ei\":" + (lastNull ? "null" : Metadata("\"\\u0041A==\"")) + "}";
            using MemoryStream input = new (Encoding.UTF8.GetBytes(json));
            EncryptionPropertiesStreamReader.EncryptionMetadataReadResult result =
                await EncryptionPropertiesStreamReader.ReadResultAsync(
                    input, PooledJsonSerializer.SerializerOptions, CancellationToken.None);
            Assert.AreEqual(lastNull ? EncryptionMetadataDisposition.Plaintext : EncryptionMetadataDisposition.Legacy, result.Disposition);
            Assert.IsNull(result.Properties);
            Assert.AreEqual(0, input.Position);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task ReadResult_MetadataStrictParsingRemainsDeferredUntilLastValue(bool replaced)
        {
            string metadata = Metadata("\"AA==\"").Replace("\"_ef\":2", "\"_ef\":/*comment*/2");
            string json = "{\"_ei\":" + metadata + (replaced ? ",\"_ei\":null}" : "}");
            using MemoryStream input = new (Encoding.UTF8.GetBytes(json));
            if (replaced)
            {
                EncryptionPropertiesStreamReader.EncryptionMetadataReadResult result =
                    await EncryptionPropertiesStreamReader.ReadResultAsync(
                        input, PooledJsonSerializer.SerializerOptions, CancellationToken.None);
                Assert.AreEqual(EncryptionMetadataDisposition.Plaintext, result.Disposition);
            }
            else
            {
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    async () => await EncryptionPropertiesStreamReader.ReadResultAsync(
                        input, PooledJsonSerializer.SerializerOptions, CancellationToken.None));
            }

            Assert.AreEqual(0, input.Position);
        }

        [TestMethod]
        public async Task ReadResult_ReplacingLargeMetadataAndReusingInputRetainsOnlyLastSlice()
        {
            string json = "{\"_ei\":" + Metadata("\"" + new string('A', 20000) + "\"") +
                ",\"_ei\":" + Metadata("\"\\u0041A==\"") + "}";
            using MemoryStream input = new (Encoding.UTF8.GetBytes(json));
            for (int attempt = 0; attempt < 3; attempt++)
            {
                EncryptionPropertiesStreamReader.EncryptionMetadataReadResult result =
                    await EncryptionPropertiesStreamReader.ReadResultAsync(
                        input, PooledJsonSerializer.SerializerOptions, CancellationToken.None);
                Assert.AreEqual(EncryptionMetadataDisposition.Legacy, result.Disposition);
                Assert.IsNull(result.Properties);
                EncryptionProperties properties = await EncryptionPropertiesStreamReader.ReadAsync(
                    input, PooledJsonSerializer.SerializerOptions, CancellationToken.None);
                CollectionAssert.AreEqual(new byte[] { 0 }, properties.EncryptedData);
                Assert.AreEqual(KeyId, properties.DataEncryptionKeyId);
                Assert.AreEqual(0, input.Position);
            }
        }

        [TestMethod]
        public async Task LazyLegacyFailure_StillReportsRawKeyAndCiphertext()
        {
            string json = Document("\"\\u0041A==\"");
            InvalidOperationException sentinel = new ("encryptor failure");
            Mock<Encryptor> encryptor = new (MockBehavior.Strict);
            encryptor.Setup(e => e.DecryptAsync(
                It.IsAny<byte[]>(), KeyId, Legacy, It.IsAny<CancellationToken>())).ThrowsAsync(sentinel);
            await using StreamDecryptableItem item = new (
                new MemoryStream(Encoding.UTF8.GetBytes(json)), encryptor.Object,
                new Mock<CosmosSerializer>(MockBehavior.Strict).Object);
            EncryptionException failure = await Assert.ThrowsExceptionAsync<EncryptionException>(
                async () => await item.GetItemAsync<JObject>());
            Assert.AreSame(sentinel, failure.InnerException);
            Assert.AreEqual(KeyId, failure.DataEncryptionKeyId);
            Assert.IsTrue(JToken.DeepEquals(JObject.Parse(json), JObject.Parse(failure.EncryptedContent)));
        }

        [TestMethod]
        public async Task ReadResult_CancellationAndNonSeekableInputRemainRejected()
        {
            using MemoryStream input = new (Payload("\"AA==\""));
            using CancellationTokenSource cancellation = new ();
            cancellation.Cancel();
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(
                async () => await EncryptionPropertiesStreamReader.ReadResultAsync(
                    input, PooledJsonSerializer.SerializerOptions, cancellation.Token));
            Assert.AreEqual(0, input.Position);
            Assert.IsTrue(input.CanRead);
            using NonSeekableStream nonSeekable = new (input.ToArray());
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                async () => await EncryptionPropertiesStreamReader.ReadResultAsync(
                    nonSeekable, PooledJsonSerializer.SerializerOptions, CancellationToken.None));
            Assert.IsTrue(nonSeekable.CanRead);
        }

        private sealed class NonSeekableStream : MemoryStream
        {
            public NonSeekableStream(byte[] bytes) : base(bytes) { }

            public override bool CanSeek => false;
        }
#endif
    }
}
