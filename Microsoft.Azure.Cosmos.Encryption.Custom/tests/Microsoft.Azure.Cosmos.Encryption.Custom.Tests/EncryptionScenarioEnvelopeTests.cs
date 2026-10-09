//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using H = EncryptionScenarioTestHelpers;

    [TestClass]
    public class EncryptionScenarioEnvelopeTests
    {
        public TestContext TestContext { get; set; }

        [DataTestMethod]
        [DynamicData(nameof(InvalidEnvelopeRows), DynamicDataSourceType.Method)]
        public async Task CompleteEnvelope_SingleInvalidField_RejectsBeforeCrypto(string scenario, string metadata, int processor)
        {
            using MemoryStream input = H.Stream("{\"id\":\"i\",\"PK\":\"p\",\"Sensitive\":\"AA==\",\"_ei\":" + metadata + "}");
            byte[] before = input.ToArray();
            using MemoryStream output = H.Output();
            byte[] outputBefore = output.ToArray();
            H.PublicEncryptor encryptor = new();

            InvalidOperationException failure = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await EncryptionProcessor.DecryptAsync(input, output, encryptor, new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None));

            Assert.AreEqual(EncryptionMetadataClassifier.InvalidMetadataMessage, failure.Message, scenario);
            Assert.AreEqual(0, encryptor.DecryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
            Assert.IsTrue(input.CanRead);
            Assert.AreEqual(0L, input.Position);
            CollectionAssert.AreEqual(before, input.ToArray());
            CollectionAssert.AreEqual(outputBefore, output.ToArray());
            Assert.AreEqual(2L, output.Position);
        }

        public static IEnumerable<object[]> InvalidEnvelopeRows()
        {
            foreach (int processor in H.Processors)
            {
                foreach (string algorithm in new[] { H.Legacy, H.Mde })
                {
                    foreach ((string field, string value) in new[]
                    {
                        ("_ea", "42"), ("_ea", "false"), ("_ea", "{}"), ("_ea", "[]"), ("_ea", "\"\""),
                        ("_en", "{}"), ("_en", "[]"), ("_en", "true"), ("_en", "42"),
                        ("_ef", "true"), ("_ef", "{}"), ("_ef", "[]"), ("_ef", "3.25"), ("_ef", "2147483648"), ("_ef", "\"2147483648\""),
                        ("_ep", "{}"), ("_ep", "true"), ("_ep", "\"/Sensitive\""), ("_ep", "[null]"), ("_ep", "[1]"), ("_ep", "[\"\"]"),
                        ("_ed", "{}"), ("_ed", "[]"), ("_ed", "42"), ("_ed", "false"),
                    })
                    {
                        JObject metadata = H.Metadata(algorithm, "/Sensitive");
                        metadata[field] = JToken.Parse(value);
                        yield return new object[] { algorithm + " " + field + "=" + value, metadata.ToString(Formatting.None), processor };
                    }

                    if (algorithm == H.Mde)
                    {
                        foreach (bool missing in new[] { true, false })
                        {
                            JObject metadata = H.Metadata(algorithm, "/Sensitive");
                            if (missing)
                            {
                                metadata.Remove("_ep");
                            }
                            else
                            {
                                metadata["_ep"] = JValue.CreateNull();
                            }

                            yield return new object[] { "MDE " + (missing ? "missing" : "null") + " paths", metadata.ToString(Formatting.None), processor };
                        }
                    }
                }

                JObject unknown = H.Metadata("future-algorithm", "/Sensitive");
                unknown["_ep"] = new JArray(JValue.CreateNull());
                yield return new object[] { "unknown algorithm does not mask invalid path member", unknown.ToString(Formatting.None), processor };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(LegacyTransportRows), DynamicDataSourceType.Method)]
        public async Task Legacy_InvalidBase64Transport_RejectsWithoutCryptoOrPublication(string transport, int processor)
        {
            JObject item = H.LegacyItem();
            item["_ei"]["_ed"] = transport;
            using MemoryStream input = H.Stream(item.ToString(Formatting.None));
            byte[] before = input.ToArray();
            using MemoryStream output = H.Output();
            byte[] outputBefore = output.ToArray();
            H.PublicEncryptor encryptor = new();

            Exception failure = await H.CaptureExceptionAsync(async () =>
                await EncryptionProcessor.DecryptAsync(input, output, encryptor, new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None));

            Assert.IsNotNull(failure, "Malformed base64 transport must not be accepted.");
            Assert.AreEqual(0, encryptor.DecryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
            Assert.IsTrue(input.CanRead);
            Assert.AreEqual(0L, input.Position);
            CollectionAssert.AreEqual(before, input.ToArray());
            CollectionAssert.AreEqual(outputBefore, output.ToArray());
            Assert.AreEqual(2L, output.Position);
            this.TestContext.WriteLine($"E02 transport={transport}; processor={processor}; error={failure.GetType().FullName}");
        }

        public static IEnumerable<object[]> LegacyTransportRows()
        {
            foreach (int processor in H.Processors)
            {
                foreach (string transport in new[] { "%%%", "A", "A===" })
                {
                    yield return new object[] { transport, processor };
                }
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(LegacyOptionalPathsRows), DynamicDataSourceType.Method)]
        public async Task Legacy_OpaqueCiphertext_OptionalPathsAndQuotedVersion_Decrypt(string paths, bool quotedVersion, int processor)
        {
            JObject item = H.LegacyItem();
            JObject metadata = (JObject)item["_ei"];
            metadata["_ed"] = "9wEC";
            if (paths == "missing")
            {
                metadata.Remove("_ep");
            }
            else
            {
                metadata["_ep"] = JToken.Parse(paths);
            }

            if (quotedVersion)
            {
                metadata["_ef"] = "2";
            }

            H.PublicEncryptor encryptor = new()
            {
                OnDecrypt = (bytes, algorithm, token) =>
                {
                    CollectionAssert.AreEqual(new byte[] { 0xF7, 1, 2 }, bytes);
                    Assert.AreEqual(H.Legacy, algorithm);
                    return Task.FromResult(Encoding.UTF8.GetBytes("{\"Sensitive\":\"secret!\"}"));
                },
            };
            using MemoryStream input = H.Stream(item.ToString(Formatting.None));
            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(input, encryptor,
                new CosmosDiagnosticsContext(), RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None);
            using (decrypted)
            {
                JObject actual = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decrypted);
                Assert.AreEqual("secret!", (string)actual["Sensitive"]);
                Assert.AreEqual("legacy", (string)actual["id"]);
                Assert.AreEqual("p", (string)actual["PK"]);
                Assert.IsNull(actual["_ei"]);
                CollectionAssert.AreEqual(new[] { "/Sensitive" }, context.DecryptionInfoList.Single().PathsDecrypted.ToArray());
            }

            Assert.AreEqual(1, encryptor.DecryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
            Assert.IsFalse(input.CanRead);
        }

        public static IEnumerable<object[]> LegacyOptionalPathsRows()
        {
            foreach (int processor in H.Processors)
            {
                foreach (string paths in new[] { "missing", "null", "[]" })
                {
                    foreach (bool quoted in new[] { false, true })
                    {
                        yield return new object[] { paths, quoted, processor };
                    }
                }
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(UnsupportedVersionRows), DynamicDataSourceType.Method)]
        public async Task RecognizedEnvelope_UnsupportedVersion_RejectsBeforeCrypto(string algorithm, int version, int processor)
        {
            JObject item = algorithm == H.Legacy ? H.LegacyItem() : H.MdeItem();
            item["_ei"]["_ef"] = version;
            using MemoryStream input = H.Stream(item.ToString(Formatting.None));
            using MemoryStream output = H.Output();
            byte[] before = output.ToArray();
            H.PublicEncryptor encryptor = new();

            NotSupportedException failure = await Assert.ThrowsExceptionAsync<NotSupportedException>(async () =>
                await EncryptionProcessor.DecryptAsync(input, output, encryptor, new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None));

            StringAssert.Contains(failure.Message, "Unknown encryption format version");
            Assert.AreEqual(0, encryptor.KeyCalls);
            Assert.AreEqual(0, encryptor.DecryptCalls);
            CollectionAssert.AreEqual(before, output.ToArray());
            Assert.AreEqual(2L, output.Position);
            Assert.IsTrue(input.CanRead);
        }

        public static IEnumerable<object[]> UnsupportedVersionRows()
        {
            foreach (int processor in H.Processors)
            {
                foreach (string algorithm in new[] { H.Legacy, H.Mde })
                {
                    foreach (int version in new[] { 0, -1, int.MaxValue, algorithm == H.Legacy ? 3 : 2 })
                    {
                        yield return new object[] { algorithm, version, processor };
                    }
                }
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(EmptyMdeCiphertextRows), DynamicDataSourceType.Method)]
        public async Task Mde_EmptyCustomCiphertext_HasMarkerAndCrossReaderRoundtrips(int writer, int reader)
        {
            H.PublicEncryptor encryptor = new()
            {
                OnEncrypt = (bytes, algorithm, token) => Task.FromResult(Array.Empty<byte>()),
                OnDecrypt = (bytes, algorithm, token) =>
                {
                    Assert.AreEqual(0, bytes.Length, "Only SDK framing, not custom ciphertext, may be removed.");
                    return Task.FromResult(Encoding.UTF8.GetBytes("secret!"));
                },
            };
            using MemoryStream plaintext = H.Stream(H.PlainItem().ToString(Formatting.None));
            using Stream encrypted = await EncryptionProcessor.EncryptAsync(plaintext, encryptor, H.Options(writer),
                new CosmosDiagnosticsContext(), CancellationToken.None);
            JObject stored = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encrypted);
            Assert.AreEqual("Ag==", (string)stored["Sensitive"]);
            using MemoryStream input = H.Stream(stored.ToString(Formatting.None));
            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(input, encryptor,
                new CosmosDiagnosticsContext(), RequestOptionsOverrideHelper.Create((JsonProcessor)reader), CancellationToken.None);
            using (decrypted)
            {
                JObject actual = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decrypted);
                Assert.AreEqual("secret!", (string)actual["Sensitive"]);
                Assert.IsNull(actual["_ei"]);
                Assert.IsNotNull(context);
            }

            Assert.AreEqual(1, encryptor.EncryptCalls);
            Assert.AreEqual(1, encryptor.DecryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
        }

        public static IEnumerable<object[]> EmptyMdeCiphertextRows()
        {
            foreach (int writer in H.Processors)
            {
                foreach (int reader in H.Processors)
                {
                    yield return new object[] { writer, reader };
                }
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(MissingFramingRows), DynamicDataSourceType.Method)]
        public async Task Mde_AbsentFramingOrInvalidTransport_FailsWithoutPublication(string value, int processor)
        {
            JObject item = H.MdeItem();
            item["Sensitive"] = value;
            using MemoryStream input = H.Stream(item.ToString(Formatting.None));
            byte[] inputBefore = input.ToArray();
            using MemoryStream output = H.Output();
            byte[] before = output.ToArray();
            H.PublicEncryptor encryptor = new();
            Exception failure = await H.CaptureExceptionAsync(async () =>
                await EncryptionProcessor.DecryptAsync(input, output, encryptor, new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None));
            Assert.IsNotNull(failure);
            Assert.AreEqual(0, encryptor.DecryptCalls);
            Assert.IsTrue(input.CanRead);
            CollectionAssert.AreEqual(inputBefore, input.ToArray());
            CollectionAssert.AreEqual(before, output.ToArray());
            Assert.AreEqual(2L, output.Position);
            this.TestContext.WriteLine($"E10 value={value}; processor={processor}; error={failure.GetType().FullName}");
        }

        public static IEnumerable<object[]> MissingFramingRows()
        {
            foreach (int processor in H.Processors)
            {
                yield return new object[] { string.Empty, processor };
                yield return new object[] { "!", processor };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(LegacyBadPlaintextRows), DynamicDataSourceType.Method)]
        public async Task Legacy_InvalidPlaintextOrPropertyCollision_DoesNotPublish(string plaintext, int processor)
        {
            H.PublicEncryptor encryptor = new()
            {
                OnDecrypt = (bytes, algorithm, token) => Task.FromResult(Encoding.UTF8.GetBytes(plaintext)),
            };
            JObject item = H.LegacyItem();
            using MemoryStream input = H.Stream(item.ToString(Formatting.None));
            byte[] inputBefore = input.ToArray();
            using MemoryStream output = H.Output();
            byte[] before = output.ToArray();
            Exception failure = await H.CaptureExceptionAsync(async () =>
                await EncryptionProcessor.DecryptAsync(input, output, encryptor, new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None));
            Assert.IsNotNull(failure);
            Assert.AreEqual(1, encryptor.DecryptCalls);
            Assert.IsTrue(input.CanRead);
            CollectionAssert.AreEqual(inputBefore, input.ToArray());
            CollectionAssert.AreEqual(before, output.ToArray());
            Assert.AreEqual(2L, output.Position);
            Assert.AreEqual("legacy", (string)item["id"]);
            this.TestContext.WriteLine($"E08 plaintext={plaintext}; processor={processor}; error={failure.GetType().FullName}");
        }

        public static IEnumerable<object[]> LegacyBadPlaintextRows()
        {
            foreach (int processor in H.Processors)
            {
                foreach (string plaintext in new[] { "[]", "null", "NOT_JSON", "{\"Sensitive\":", "{\"Added\":\"x\",\"id\":\"overwrite\"}", "{\"Added\":\"x\",\"_ei\":{}}" })
                {
                    yield return new object[] { plaintext, processor };
                }
            }
        }
    }
}
