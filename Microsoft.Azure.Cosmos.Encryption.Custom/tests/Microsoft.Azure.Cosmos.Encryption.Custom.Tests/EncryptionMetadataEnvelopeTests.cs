//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
#if NET8_0_OR_GREATER
    using System.Text.Json;
#endif

    [TestClass]
    public class EncryptionMetadataEnvelopeTests
    {
        private const string DekId = "dekId";
#pragma warning disable CS0618
        private const string LegacyAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized;
#pragma warning restore CS0618

        // Produced by published Microsoft.Azure.Cosmos.Encryption.Custom 1.0.0-preview07:
        // nupkg SHA256 121AA0ED2A518D1F791992AC4E6A90B8E3A16A9BEDE4CB719F6156CF384398F8;
        // assembly SHA256 064FE92B0CC610B3F6CB5E290DA3DFA231643FADB7DC974D01FFDE8D9AEBA3AC.
        // Test key is bytes 1..32. Ciphertext is 81 bytes, SHA256
        // DCC9C5832D0BFA9C99A35D7594363BFFE1708222D1C758E2FF2DB458F5BDF532.
        internal const string Preview07LegacyFixtureBase64 =
            "eyJpZCI6Im1peGVkLTEiLCJQSyI6Im1peGVkLXBrIiwiSGlnaFByZWNpc2lvbiI6OTAwNzE5OTI1NDc0MDk5MywiVHJhaWxpbmdaZXJvIjoxMjMuNDUsIkV4cG9uZW50Ijo2LjAyRSsyMywiX2VpIjp7Il9lZiI6MiwiX2VuIjoibWl4ZWRGaXh0dXJlRGVrIiwiX2VhIjoiQUVBZXMyNTZDYmNIbWFjU2hhMjU2UmFuZG9taXplZCIsIl9lZCI6IkFhOEVtMEljOTRXeC9ZQUdMMngxNmZTRitUTFV0SzJTY2tqbHlkMjhuckQ5ZGJlaDFUUmlXeStmNVVsMWtkdmwzdEZsUUx2T1J2VTdNZFF2TnI5L0svOWRndWpnRk9UaTJUaXBFdE53bkEwSiIsIl9lcCI6WyIvU2Vuc2l0aXZlIl19fQ==";

        [TestMethod]
        [DynamicData(nameof(InvalidLegacyEncryptedDataMetadataCases))]
        public async Task Decrypt_InvalidLegacyEncryptedData_FailsBeforeKeyOrCryptoAndPreservesStreams(
            string scenario,
            string encryptionMetadata,
            int jsonProcessorValue)
        {
            _ = scenario;
            await AssertInvalidMetadataRejectedBeforeCryptoAsync(encryptionMetadata, jsonProcessorValue);
        }

        [TestMethod]
        [DynamicData(nameof(InvalidRecognizedMetadataCases))]
        public async Task Decrypt_InvalidRecognizedEnvelope_FailsBeforeKeyOrCryptoAndPreservesStreams(
            string scenario,
            string encryptionMetadata,
            int jsonProcessorValue)
        {
            _ = scenario;
            await AssertInvalidMetadataRejectedBeforeCryptoAsync(encryptionMetadata, jsonProcessorValue);
        }

        [TestMethod]
        [DynamicData(nameof(SupportedJsonProcessors))]
        public async Task Decrypt_PinnedPreview07LegacyFixture_Succeeds(int jsonProcessorValue)
        {
            byte[] fixture = Convert.FromBase64String(Preview07LegacyFixtureBase64);
            JObject document = JObject.Parse(Encoding.UTF8.GetString(fixture));
            byte[] cipherText = Convert.FromBase64String(document["_ei"]["_ed"].Value<string>());
            using (SHA256 hash = SHA256.Create())
            {
                Assert.AreEqual(81, cipherText.Length);
                Assert.AreEqual(
                    "DCC9C5832D0BFA9C99A35D7594363BFFE1708222D1C758E2FF2DB458F5BDF532",
                    BitConverter.ToString(hash.ComputeHash(cipherText)).Replace("-", string.Empty));
            }

            AeadAes256CbcHmac256EncryptionKey fixtureKey = new(
                Enumerable.Range(1, 32).Select(value => (byte)value).ToArray(),
                LegacyAlgorithm);
            DataEncryptionKey key = new AeadAes256CbcHmac256Algorithm(fixtureKey, EncryptionType.Randomized, algorithmVersion: 1);
            Mock<Encryptor> encryptor = new(MockBehavior.Strict);
            encryptor.Setup(instance => instance.DecryptAsync(
                It.IsAny<byte[]>(), "mixedFixtureDek", LegacyAlgorithm, It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] cipher, string id, string algorithm, CancellationToken token) => key.DecryptData(cipher));
            using MemoryStream input = new(fixture);
            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(
                input, encryptor.Object, new CosmosDiagnosticsContext(),
                RequestOptionsOverrideHelper.Create((JsonProcessor)jsonProcessorValue), CancellationToken.None);
            using (decrypted)
            {
                JObject actual = TestCommon.FromStream<JObject>(decrypted);
                Assert.AreEqual("exact secret", actual["Sensitive"].Value<string>());
                Assert.AreEqual("mixed-1", actual["id"].Value<string>());
                Assert.AreEqual("mixed-pk", actual["PK"].Value<string>());
                Assert.IsNull(actual["_ei"]);
                Assert.IsNotNull(context);
                Assert.AreEqual("mixedFixtureDek", context.DecryptionInfoList.Single().DataEncryptionKeyId);
                CollectionAssert.AreEqual(new[] { "/Sensitive" }, context.DecryptionInfoList.Single().PathsDecrypted.ToArray());
            }
        }

        [TestMethod]
        [DynamicData(nameof(SupportedJsonProcessors))]
        public async Task Decrypt_OpaqueCustomEncryptor_PublicContainerRoundtrip(int jsonProcessorValue)
        {
            byte[] opaqueCipher = { 0xF7, 0x01, 0x02 };
            byte[] savedPlaintext = null;
            byte[] storedDocument = null;
            Mock<Encryptor> encryptor = new(MockBehavior.Strict);
            encryptor.Setup(instance => instance.EncryptAsync(
                It.IsAny<byte[]>(), DekId, LegacyAlgorithm, It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] plain, string id, string algorithm, CancellationToken token) =>
                {
                    savedPlaintext = plain.ToArray();
                    return opaqueCipher.ToArray();
                });
            encryptor.Setup(instance => instance.DecryptAsync(
                It.IsAny<byte[]>(), DekId, LegacyAlgorithm, It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] cipher, string id, string algorithm, CancellationToken token) =>
                {
                    CollectionAssert.AreEqual(opaqueCipher, cipher);
                    return savedPlaintext.ToArray();
                });

            Mock<Container> inner = new(MockBehavior.Strict);
            Mock<CosmosClient> client = new();
            client.SetupGet(instance => instance.ClientOptions).Returns(new CosmosClientOptions());
            Mock<Database> database = new();
            database.SetupGet(instance => instance.Client).Returns(client.Object);
            inner.SetupGet(instance => instance.Database).Returns(database.Object);
            inner.Setup(instance => instance.CreateItemStreamAsync(
                It.IsAny<Stream>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, PartitionKey pk, ItemRequestOptions options, CancellationToken token) =>
                {
                    using MemoryStream stored = new();
                    stream.CopyTo(stored);
                    storedDocument = stored.ToArray();
                    return new ResponseMessage(HttpStatusCode.Created) { Content = new MemoryStream(storedDocument) };
                });
            inner.Setup(instance => instance.ReadItemStreamAsync(
                "opaque-1", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResponseMessage(HttpStatusCode.OK) { Content = new MemoryStream(storedDocument) });
            Container container = inner.Object.WithEncryptor(encryptor.Object);
            using Stream plaintext = TestCommon.ToStream(new { id = "opaque-1", PK = "pk", Sensitive = "opaque secret" });
            EncryptionItemRequestOptions writeOptions = RequestOptionsOverrideHelper.Create(new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = LegacyAlgorithm,
                PathsToEncrypt = new[] { "/Sensitive" },
            }, JsonProcessor.Newtonsoft);
            using ResponseMessage created = await container.CreateItemStreamAsync(plaintext, new PartitionKey("pk"), writeOptions);
            Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
            JObject storedEnvelope = JObject.Parse(Encoding.UTF8.GetString(storedDocument));
            CollectionAssert.AreEqual(opaqueCipher, Convert.FromBase64String(storedEnvelope["_ei"]["_ed"].Value<string>()));
            using ResponseMessage read = await container.ReadItemStreamAsync(
                "opaque-1", new PartitionKey("pk"),
                (ItemRequestOptions)RequestOptionsOverrideHelper.Create((JsonProcessor)jsonProcessorValue));
            JObject actual = TestCommon.FromStream<JObject>(read.Content);
            Assert.AreEqual("opaque secret", actual["Sensitive"].Value<string>());
            Assert.AreEqual("opaque-1", actual["id"].Value<string>());
            Assert.AreEqual("pk", actual["PK"].Value<string>());
            Assert.IsNull(actual["_ei"]);
            encryptor.Verify(instance => instance.EncryptAsync(
                It.IsAny<byte[]>(), DekId, LegacyAlgorithm, It.IsAny<CancellationToken>()), Times.Once);
            encryptor.Verify(instance => instance.DecryptAsync(
                It.IsAny<byte[]>(), DekId, LegacyAlgorithm, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [TestMethod]
        [DynamicData(nameof(ValidLegacyEnvelopeCases))]
        public void Classify_LegacyOptionalPathsAndVersionRepresentations(string metadata)
        {
            Assert.AreEqual(EncryptionMetadataDisposition.Legacy, EncryptionMetadataClassifier.Classify(JObject.Parse(metadata)));
#if NET8_0_OR_GREATER
            using JsonDocument document = JsonDocument.Parse(metadata);
            Assert.AreEqual(EncryptionMetadataDisposition.Legacy, EncryptionMetadataClassifier.Classify(document.RootElement));
#endif
        }

        [TestMethod]
        [DynamicData(nameof(ValidMdeEncryptedDataCases))]
        public async Task Decrypt_ValidMdeMissingOrNullEncryptedData_Succeeds(bool omitEncryptedData, int jsonProcessorValue)
        {
            Mock<Encryptor> encryptor = TestEncryptorFactory.CreateMde(DekId, out _);
            using Stream plaintext = TestCommon.ToStream(new { id = "mde-1", Sensitive = "MDE secret" });
            using Stream encrypted = await EncryptionProcessor.EncryptAsync(
                plaintext, encryptor.Object,
                RequestOptionsOverrideHelper.Create(new EncryptionOptions
                {
                    DataEncryptionKeyId = DekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    PathsToEncrypt = new[] { "/Sensitive" },
                }, JsonProcessor.Newtonsoft),
                new CosmosDiagnosticsContext(), CancellationToken.None);
            JObject document = TestCommon.FromStream<JObject>(encrypted);
            Assert.AreEqual(JTokenType.Null, document["_ei"]["_ed"].Type);
            if (omitEncryptedData)
            {
                ((JObject)document["_ei"]).Remove("_ed");
            }

            using Stream input = TestCommon.ToStream(document);
            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(
                input, encryptor.Object, new CosmosDiagnosticsContext(),
                RequestOptionsOverrideHelper.Create((JsonProcessor)jsonProcessorValue), CancellationToken.None);
            using (decrypted)
            {
                Assert.AreEqual("MDE secret", TestCommon.FromStream<JObject>(decrypted)["Sensitive"].Value<string>());
                Assert.IsNotNull(context);
            }
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task Decrypt_StreamPreCanceledInvalidLegacy_PreservesCancellationPrecedence()
        {
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            using Stream input = TestCommon.ToStream(JObject.Parse(
                $"{{\"_ei\":{{\"_ef\":2,\"_ea\":\"{LegacyAlgorithm}\",\"_en\":\"{DekId}\",\"_ed\":null}}}}"));
            Mock<Encryptor> encryptor = new(MockBehavior.Strict);
            try
            {
                await EncryptionProcessor.DecryptAsync(
                    input, encryptor.Object, new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create(JsonProcessor.Stream), cancellation.Token);
                Assert.Fail("Pre-canceled Stream reads must observe cancellation before metadata validation.");
            }
            catch (OperationCanceledException)
            {
                Assert.IsTrue(input.CanRead);
            }
        }
#endif

        public static IEnumerable<object[]> SupportedJsonProcessors => EncryptionProcessorTests.SupportedJsonProcessors;

        public static IEnumerable<object[]> InvalidLegacyEncryptedDataMetadataCases
        {
            get
            {
                foreach (int processor in SupportedJsonProcessors.Select(values => (int)values[0]))
                {
                    yield return new object[] { "missing legacy encrypted data", $"{{\"_ef\":2,\"_ea\":\"{LegacyAlgorithm}\",\"_en\":\"{DekId}\",\"_ep\":[\"/Sensitive\"]}}", processor };
                    yield return new object[] { "null legacy encrypted data", $"{{\"_ef\":2,\"_ea\":\"{LegacyAlgorithm}\",\"_en\":\"{DekId}\",\"_ed\":null,\"_ep\":[\"/Sensitive\"]}}", processor };
                    yield return new object[] { "empty legacy encrypted data", $"{{\"_ef\":2,\"_ea\":\"{LegacyAlgorithm}\",\"_en\":\"{DekId}\",\"_ed\":\"\",\"_ep\":[\"/Sensitive\"]}}", processor };
                }
            }
        }

        public static IEnumerable<object[]> InvalidRecognizedMetadataCases
        {
            get
            {
                foreach (int processor in SupportedJsonProcessors.Select(values => (int)values[0]))
                {
                    foreach (string algorithm in new[] { LegacyAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized })
                    {
                        JObject metadata = new()
                        {
                            ["_ef"] = algorithm == LegacyAlgorithm ? 2 : 3,
                            ["_ea"] = algorithm,
                            ["_en"] = DekId,
                            ["_ep"] = new JArray("/Sensitive"),
                            ["_ed"] = algorithm == LegacyAlgorithm ? JValue.CreateString("AA==") : JValue.CreateNull(),
                        };
                        foreach (string field in new[] { "_ef", "_en" })
                        {
                            foreach (string state in new[] { "missing", "null", "malformed" })
                            {
                                JObject invalid = (JObject)metadata.DeepClone();
                                if (state == "missing")
                                {
                                    invalid.Remove(field);
                                }
                                else
                                {
                                    invalid[field] = state == "null" ? JValue.CreateNull() : JValue.CreateString(field == "_ef" ? "not-a-version" : "   ");
                                }

                                yield return new object[] { $"{algorithm} {state} {field}", invalid.ToString(), processor };
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<object[]> ValidLegacyEnvelopeCases
        {
            get
            {
                foreach (string version in new[] { "2", "\"2\"" })
                {
                    foreach (string paths in new[] { string.Empty, ",\"_ep\":null", ",\"_ep\":[]", ",\"_ep\":[\"/Sensitive\"]" })
                    {
                        yield return new object[] { $"{{\"_ef\":{version},\"_ea\":\"{LegacyAlgorithm}\",\"_en\":\"{DekId}\",\"_ed\":\"9wEC\"{paths}}}" };
                    }
                }
            }
        }

        public static IEnumerable<object[]> ValidMdeEncryptedDataCases
        {
            get
            {
                foreach (int processor in SupportedJsonProcessors.Select(values => (int)values[0]))
                {
                    yield return new object[] { false, processor };
                    yield return new object[] { true, processor };
                }
            }
        }

        private static async Task AssertInvalidMetadataRejectedBeforeCryptoAsync(string metadata, int processor)
        {
            byte[] payload = Encoding.UTF8.GetBytes("{\"id\":\"id1\",\"Sensitive\":\"AA==\",\"_ei\":" + metadata + "}");
            Mock<Encryptor> encryptor = new(MockBehavior.Strict);
            Mock<DataEncryptionKeyProvider> keyProvider = new(MockBehavior.Strict);
            foreach (Encryptor implementation in new[] { encryptor.Object, new CosmosEncryptor(keyProvider.Object) })
            {
                using MemoryStream input = new(payload);
                using MemoryStream output = new(new byte[] { 1, 2, 3, 4 });
                output.Position = 2;
                InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    async () => await EncryptionProcessor.DecryptAsync(
                        input, output, implementation, new CosmosDiagnosticsContext(),
                        RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None));
                Assert.AreEqual(EncryptionMetadataClassifier.InvalidMetadataMessage, exception.Message);
                Assert.IsTrue(input.CanRead);
                Assert.AreEqual(0, input.Position);
                CollectionAssert.AreEqual(payload, input.ToArray());
                Assert.AreEqual(2, output.Position);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, output.ToArray());
            }

            encryptor.Verify(instance => instance.DecryptAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            encryptor.Verify(instance => instance.GetEncryptionKeyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            keyProvider.Verify(provider => provider.FetchDataEncryptionKeyWithoutRawKeyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
