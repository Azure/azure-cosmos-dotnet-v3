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
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using H = EncryptionScenarioTestHelpers;

    // These tests assert ownership/publication invariants, not reader equivalence for unspecified inputs.
    [TestClass]
    public class EncryptionScenarioDecisionTests
    {
        public TestContext TestContext { get; set; }

        [DataTestMethod]
        [DynamicData(nameof(TypedPlaintextRows), DynamicDataSourceType.Method)]
        public async Task Mde_KnownContainerMarker_RecordsReturnedPlaintextInterpretation(int marker, string plaintext, bool valid, int processor)
        {
            JObject item = H.MdeItem();
            item["Sensitive"] = Convert.ToBase64String(new byte[] { (byte)marker, 0xF7 });
            H.PublicEncryptor encryptor = new()
            {
                OnDecrypt = (bytes, algorithm, token) =>
                {
                    CollectionAssert.AreEqual(new byte[] { 0xF7 }, bytes);
                    return Task.FromResult(Encoding.UTF8.GetBytes(plaintext));
                },
            };
            using MemoryStream input = H.Stream(item.ToString(Formatting.None));
            byte[] inputBefore = input.ToArray();
            using MemoryStream output = H.Output();
            byte[] before = output.ToArray();
            DecryptionContext context = null;
            Exception failure = await H.CaptureExceptionAsync(async () =>
                context = await EncryptionProcessor.DecryptAsync(input, output, encryptor, new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None));
            JObject parsed = null;
            string publishedText = null;
            string fieldType = null;
            string parseError = null;
            string parseMessage = null;
            if (failure != null)
            {
                CollectionAssert.AreEqual(before, output.ToArray());
                Assert.AreEqual(2L, output.Position);
                Assert.IsTrue(input.CanRead);
            }
            else
            {
                Assert.AreEqual(0L, output.Position);
                Assert.IsNotNull(context);
                CollectionAssert.AreEqual(new[] { "/Sensitive" }, context.DecryptionInfoList.Single().PathsDecrypted.ToArray());
                publishedText = Encoding.UTF8.GetString(output.ToArray().Skip(2).ToArray());
                try
                {
                    parsed = JObject.Parse(publishedText);
                    fieldType = parsed["Sensitive"]?.Type.ToString();
                }
                catch (JsonException exception)
                {
                    parseError = exception.GetType().FullName;
                    parseMessage = exception.Message;
                }

                if (parsed != null)
                {
                    Assert.AreEqual((string)item["id"], (string)parsed["id"]);
                    Assert.AreEqual((string)item["PK"], (string)parsed["PK"]);
                    Assert.AreEqual((string)item["Plain"], (string)parsed["Plain"]);
                    Assert.IsNull(parsed["_ei"]);
                }
            }

            if (valid)
            {
                Assert.IsNull(failure, $"A canonical plaintext control failed: {failure}");
                Assert.IsNull(parseError, $"A canonical plaintext control published invalid JSON: {parseMessage}");
                Assert.IsNotNull(parsed);
                Assert.AreEqual(marker == 7 ? JTokenType.Object : JTokenType.Array, parsed["Sensitive"].Type);
                Assert.IsTrue(JToken.DeepEquals(JToken.Parse(plaintext), parsed["Sensitive"]));
            }

            CollectionAssert.AreEqual(inputBefore, input.ToArray());
            Assert.IsTrue(output.CanRead);
            Assert.IsTrue(output.CanWrite);
            Assert.AreEqual(1, encryptor.DecryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
            this.TestContext.WriteLine("DECISION " + JsonConvert.SerializeObject(new
            {
                family = "E12",
                marker,
                plaintext,
                canonicalControl = valid,
                processor,
                operationError = failure?.GetType().FullName,
                operationMessage = failure?.Message,
                publishedText,
                publishedFieldType = fieldType,
                parseError,
                parseMessage,
                outputBase64 = Convert.ToBase64String(output.ToArray()),
                interpretation = "Observation only: malformed rejection and declared-shape enforcement require a contract decision.",
            }));
        }

        public static IEnumerable<object[]> TypedPlaintextRows()
        {
            foreach (int processor in H.Processors)
            {
                foreach (int marker in new[] { 7, 6 })
                {
                    yield return new object[] { marker, marker == 7 ? "{}" : "[]", true, processor };
                    yield return new object[] { marker, "NOT_JSON", false, processor };
                    yield return new object[] { marker, marker == 7 ? "[]" : "{}", false, processor };
                }
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(DuplicateFeedRows), DynamicDataSourceType.Method)]
        public async Task Lazy_StructurallyInvalidEnvelope_RecordsBestEffortDiagnostics(int processor)
        {
            JObject document = H.MdeItem();
            document["_ei"]["_ep"] = new JArray(JValue.CreateNull());
            H.PublicEncryptor encryptor = new();
            Mock<CosmosSerializer> serializer = new(MockBehavior.Strict);
            DecryptableItem item;
#if NET8_0_OR_GREATER
            if (processor == (int)JsonProcessor.Stream)
            {
                item = new StreamDecryptableItem(H.Stream(document.ToString(Formatting.None)), encryptor, serializer.Object);
            }
            else
#endif
            {
                item = new DecryptableItemCore(document, encryptor, serializer.Object);
            }

            try
            {
                EncryptionException failure = await Assert.ThrowsExceptionAsync<EncryptionException>(() => item.GetItemAsync<JObject>());
                Assert.AreEqual(EncryptionMetadataClassifier.InvalidMetadataMessage, failure.InnerException.Message);
                Assert.AreEqual(0, encryptor.DecryptCalls);
                Assert.AreEqual(0, encryptor.KeyCalls);
                if (failure.EncryptedContent.Length != 0)
                {
                    Assert.IsTrue(JToken.DeepEquals(document, JObject.Parse(failure.EncryptedContent)));
                }

                this.TestContext.WriteLine("DECISION " + JsonConvert.SerializeObject(new
                {
                    family = "E05",
                    scenario = "recognized algorithm and usable key; invalid path member",
                    processor,
                    keyId = failure.DataEncryptionKeyId,
                    contentAvailable = failure.EncryptedContent.Length != 0,
                    primaryError = failure.InnerException.Message,
                }));
            }
            finally
            {
                await item.DisposeAsync();
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(DecisionDocumentRows), DynamicDataSourceType.Method)]
        public async Task NoncanonicalStoredInput_RecordsReaderOutcomeAndChecksFailurePublication(string family, string scenario, string json, int processor)
        {
            using MemoryStream input = H.Stream(json);
            byte[] before = input.ToArray();
            using MemoryStream output = H.Output();
            byte[] outputBefore = output.ToArray();
            H.PublicEncryptor encryptor = new();
            DecryptionContext context = null;
            Exception failure = await H.CaptureExceptionAsync(async () =>
                context = await EncryptionProcessor.DecryptAsync(input, output, encryptor, new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None));

            CollectionAssert.AreEqual(before, input.ToArray());
            Assert.AreEqual(0, encryptor.KeyCalls);
            if (failure != null)
            {
                Assert.IsTrue(input.CanRead);
                CollectionAssert.AreEqual(outputBefore, output.ToArray());
                Assert.AreEqual(2L, output.Position);
            }

            this.Record(family, scenario, processor, failure, encryptor.DecryptCalls, output.ToArray(), context);
        }

        public static IEnumerable<object[]> DecisionDocumentRows()
        {
            foreach (int processor in H.Processors)
            {
                foreach (string version in new[] { "\"+3\"", "\" 3 \"", "\"03\"", "3.0", "\"3.0\"" })
                {
                    JObject metadata = H.Metadata(H.Mde, "/Sensitive");
                    string raw = metadata.ToString(Formatting.None).Replace("\"_ef\":3", "\"_ef\":" + version);
                    yield return new object[] { "E03", "version=" + version, "{\"id\":\"mde\",\"PK\":\"p\",\"Sensitive\":\"AnNlY3JldCE=\",\"_ei\":" + raw + "}", processor };
                }

                foreach (string value in new[] { "42", "true", "{}", "[2,65]", "null", "missing" })
                {
                    JObject item = H.MdeItem();
                    if (value == "missing")
                    {
                        item.Remove("Sensitive");
                    }
                    else
                    {
                        item["Sensitive"] = JToken.Parse(value);
                    }

                    yield return new object[] { "E11", "target=" + value, item.ToString(Formatting.None), processor };
                }

                foreach (string paths in new[] { "[\"/Sensitive\",\"/Sensitive\"]", "[\"/id\"]", "[\"/_ei\"]", "[\"Sensitive\"]", "[\"/\"]", "[\"/Sensitive/nested\"]" })
                {
                    JObject item = H.MdeItem();
                    item["_ei"]["_ep"] = JToken.Parse(paths);
                    yield return new object[] { "E13", "paths=" + paths, item.ToString(Formatting.None), processor };
                }

                JObject canonical = H.MdeItem();
                string duplicateKey = canonical.ToString(Formatting.None).Replace("\"_en\":\"" + H.KeyId + "\"",
                    "\"_en\":\"earlier-key\",\"_en\":\"" + H.KeyId + "\"");
                yield return new object[] { "E14", "duplicate key identifier, last canonical", duplicateKey, processor };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(LegacyDecisionRows), DynamicDataSourceType.Method)]
        public async Task Legacy_NoncanonicalTransportOrTrailingPlaintext_RecordsConversion(string scenario, int processor)
        {
            JObject item = H.LegacyItem();
            string returnedPlaintext = scenario == "trailing plaintext"
                ? "{\"Sensitive\":\"secret!\"}{\"Ignored\":true}"
                : "{\"Sensitive\":\"secret!\"}";
            if (scenario == "GUID transport")
            {
                item["_ei"]["_ed"] = "00000000-0000-0000-0000-000000000000";
            }

            int receivedLength = -1;
            H.PublicEncryptor encryptor = new()
            {
                OnDecrypt = (bytes, algorithm, token) =>
                {
                    receivedLength = bytes.Length;
                    return Task.FromResult(Encoding.UTF8.GetBytes(returnedPlaintext));
                },
            };
            using MemoryStream input = H.Stream(item.ToString(Formatting.None));
            using MemoryStream output = H.Output();
            byte[] before = output.ToArray();
            Exception failure = await H.CaptureExceptionAsync(async () =>
                await EncryptionProcessor.DecryptAsync(input, output, encryptor, new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None));
            if (failure != null)
            {
                CollectionAssert.AreEqual(before, output.ToArray());
                Assert.AreEqual(2L, output.Position);
                Assert.IsTrue(input.CanRead);
            }

            this.Record(scenario == "GUID transport" ? "E02" : "E08", scenario + "; receivedCipherLength=" + receivedLength,
                processor, failure, encryptor.DecryptCalls, output.ToArray(), null);
        }

        public static IEnumerable<object[]> LegacyDecisionRows()
        {
            foreach (int processor in H.Processors)
            {
                yield return new object[] { "GUID transport", processor };
                yield return new object[] { "trailing plaintext", processor };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(MigrationDuplicateRows), DynamicDataSourceType.Method)]
        public async Task TypedMigration_DuplicateMetadata_RecordsReplayOutcome(string scenario, string json, int processor)
        {
            using MemoryStream input = H.Stream(json);
            byte[] before = input.ToArray();
            H.PublicEncryptor encryptor = new();
            Stream encrypted = null;
            Exception failure = await H.CaptureExceptionAsync(async () =>
                encrypted = await EncryptionProcessor.EncryptAsync(input, encryptor, H.Options(processor),
                    new CosmosDiagnosticsContext(), CancellationToken.None, replacePlaintextEncryptionMetadata: true));
            byte[] result = Array.Empty<byte>();
            if (encrypted != null)
            {
                using (encrypted)
                using (MemoryStream copy = new())
                {
                    await encrypted.CopyToAsync(copy);
                    result = copy.ToArray();
                }
            }
            else
            {
                Assert.IsNotNull(failure);
                Assert.IsTrue(input.CanRead);
            }

            CollectionAssert.AreEqual(before, input.ToArray());
            this.Record("E14", scenario, processor, failure, encryptor.EncryptCalls, result, null);
        }

        public static IEnumerable<object[]> MigrationDuplicateRows()
        {
            foreach (int processor in H.Processors)
            {
                const string prefix = "{\"id\":\"i\",\"PK\":\"p\",\"Sensitive\":\"secret!\",";
                const string unsafeMetadata = "{\"_ea\":\"future-algorithm\",\"_ep\":[]}";
                yield return new object[] { "earlier unsafe, later null", prefix + "\"_ei\":" + unsafeMetadata + ",\"_ei\":null}", processor };
                yield return new object[] { "earlier null, later unsafe", prefix + "\"_ei\":null,\"_ei\":" + unsafeMetadata + "}", processor };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(DuplicateFeedRows), DynamicDataSourceType.Method)]
        public async Task Feed_DuplicateDocumentsWithEarlierInvalidItem_RecordsReaderOutcome(int processor)
        {
            JObject invalid = H.MdeItem("earlier");
            invalid["_ei"]["_ep"] = new JArray(JValue.CreateNull());
            string json = "{\"Documents\":[" + invalid.ToString(Formatting.None) + "],\"Documents\":[" +
                H.MdeItem("later").ToString(Formatting.None) + "],\"_count\":1}";
            using MemoryStream input = H.Stream(json);
            byte[] before = input.ToArray();
            H.PublicEncryptor encryptor = new();
            Stream result = null;
            Exception failure = await H.CaptureExceptionAsync(async () =>
                result = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(input, encryptor, (JsonProcessor)processor, CancellationToken.None));
            byte[] resultBytes = Array.Empty<byte>();
            if (result != null)
            {
                using (result)
                using (MemoryStream copy = new())
                {
                    await result.CopyToAsync(copy);
                    resultBytes = copy.ToArray();
                }
            }
            else
            {
                Assert.IsNotNull(failure);
                CollectionAssert.AreEqual(before, input.ToArray());
                Assert.IsTrue(input.CanRead);
            }

            this.Record("E18", "earlier invalid Documents, later valid", processor, failure, encryptor.DecryptCalls, resultBytes, null);
        }

        public static IEnumerable<object[]> DuplicateFeedRows()
            => H.Processors.Select(processor => new object[] { processor });

        [DataTestMethod]
        [DynamicData(nameof(LegacyCustomResultRows), DynamicDataSourceType.Method)]
        public async Task Legacy_NullTaskOrEmptyCiphertext_RecordsUnspecifiedResultBehavior(string scenario)
        {
            H.PublicEncryptor encryptor = new()
            {
                OnEncrypt = (bytes, algorithm, token) => scenario == "null task" ? null : Task.FromResult(Array.Empty<byte>()),
            };
            using MemoryStream input = H.Stream(H.PlainItem().ToString(Formatting.None));
            Stream result = null;
            Exception failure = await H.CaptureExceptionAsync(async () =>
                result = await EncryptionProcessor.EncryptAsync(input, encryptor, H.Options(0, H.Legacy),
                    new CosmosDiagnosticsContext(), CancellationToken.None));
            byte[] bytes = Array.Empty<byte>();
            if (result != null)
            {
                using (result)
                using (MemoryStream copy = new())
                {
                    await result.CopyToAsync(copy);
                    bytes = copy.ToArray();
                }
            }

            Assert.AreEqual(1, encryptor.EncryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
            Assert.IsTrue(result != null || failure != null);
            this.Record("E06", scenario + "; callerInputOpen=" + input.CanRead, 0, failure, encryptor.EncryptCalls, bytes, null);
        }

        public static IEnumerable<object[]> LegacyCustomResultRows()
        {
            yield return new object[] { "null task" };
            yield return new object[] { "empty cipher" };
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Legacy_NoncooperatingCustomTask_RecordsCancellationWaitPolicy(bool decrypt)
        {
            using CancellationTokenSource cancellation = new();
            TaskCompletionSource<byte[]> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            byte[] retained = null;
            H.PublicEncryptor encryptor = new();
            Task<byte[]> Crypto(byte[] bytes, string algorithm, CancellationToken token)
            {
                Assert.AreEqual(cancellation.Token, token);
                retained = bytes.ToArray();
                return completion.Task;
            }

            if (decrypt)
            {
                encryptor.OnDecrypt = Crypto;
            }
            else
            {
                encryptor.OnEncrypt = Crypto;
            }

            using MemoryStream input = H.Stream((decrypt ? H.LegacyItem() : H.PlainItem()).ToString(Formatting.None));
            byte[] before = input.ToArray();
            Stream result = null;
            Task operation = decrypt ? DecryptAsync() : EncryptAsync();
            Assert.IsNotNull(retained, "The actual Legacy route must have reached the custom task.");
            cancellation.Cancel();
            bool completedBeforeRelease = operation.IsCompleted;
            completion.SetResult(decrypt ? Encoding.UTF8.GetBytes("{\"Sensitive\":\"secret!\"}") : retained);
            Exception failure = await H.CaptureExceptionAsync(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
            byte[] output = Array.Empty<byte>();
            if (result != null)
            {
                using (result)
                using (MemoryStream copy = new())
                {
                    await result.CopyToAsync(copy);
                    output = copy.ToArray();
                }
            }

            CollectionAssert.AreEqual(before, input.ToArray());
            Assert.AreEqual(0, encryptor.KeyCalls);
            Assert.IsFalse(failure is TimeoutException, "The released custom task must finish; the wait policy itself is not asserted.");
            this.Record("E06", $"decrypt={decrypt}; completedBeforeRelease={completedBeforeRelease}",
                0, failure, decrypt ? encryptor.DecryptCalls : encryptor.EncryptCalls, output, null);

            async Task EncryptAsync()
            {
                result = await EncryptionProcessor.EncryptAsync(input, encryptor, H.Options(0, H.Legacy),
                    new CosmosDiagnosticsContext(), cancellation.Token);
            }

            async Task DecryptAsync()
            {
                (result, _) = await EncryptionProcessor.DecryptAsync(input, encryptor, new CosmosDiagnosticsContext(), cancellation.Token);
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(RawByteDecisionRows), DynamicDataSourceType.Method)]
        public async Task Json_RawUtf8Boundary_RecordsReaderCompatibility(string scenario, byte[] bytes, int processor)
        {
            using MemoryStream input = new(bytes);
            using MemoryStream output = H.Output();
            byte[] before = output.ToArray();
            H.PublicEncryptor encryptor = new();
            Exception failure = await H.CaptureExceptionAsync(async () =>
                await EncryptionProcessor.DecryptAsync(input, output, encryptor, new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None));
            CollectionAssert.AreEqual(bytes, input.ToArray());
            CollectionAssert.AreEqual(before, output.ToArray());
            Assert.AreEqual(2L, output.Position);
            Assert.IsTrue(input.CanRead);
            Assert.AreEqual(0, encryptor.DecryptCalls);
            this.Record("E25", scenario, processor, failure, encryptor.DecryptCalls, output.ToArray(), null);
        }

        public static IEnumerable<object[]> RawByteDecisionRows()
        {
            foreach (int processor in H.Processors)
            {
                yield return new object[] { "UTF8 BOM", new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("{\"id\":\"i\"}")).ToArray(), processor };
                yield return new object[] { "invalid UTF8 in string", Encoding.UTF8.GetBytes("{\"id\":\"").Concat(new byte[] { 0xC3, 0x28 }).Concat(Encoding.UTF8.GetBytes("\"}")).ToArray(), processor };
            }
        }

        [TestMethod]
        public async Task AllVersionsChangeFeed_RecordsSupportWithoutAssumingPassthroughIsDesiredEncryption()
        {
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            Container.ChangeFeedHandler<ChangeFeedItem<JObject>> captured = null;
            inner.Setup(c => c.GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes("scenario",
                    It.IsAny<Container.ChangeFeedHandler<ChangeFeedItem<JObject>>>()))
                .Callback<string, Container.ChangeFeedHandler<ChangeFeedItem<JObject>>>((name, handler) => captured = handler)
                .Returns((ChangeFeedProcessorBuilder)null);
            int calls = 0;
            IReadOnlyCollection<ChangeFeedItem<JObject>> delivered = null;
            container.GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes<JObject>("scenario",
                (context, items, token) =>
                {
                    calls++;
                    delivered = items;
                    return Task.CompletedTask;
                });
            ChangeFeedItem<JObject> change = JsonConvert.DeserializeObject<ChangeFeedItem<JObject>>(
                "{\"current\":" + H.MdeItem().ToString(Formatting.None) + ",\"previous\":" + H.LegacyItem().ToString(Formatting.None) + "}");
            await captured(new Mock<ChangeFeedProcessorContext>().Object, new[] { change }, CancellationToken.None);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, delivered.Count);
            Assert.AreEqual("mde", (string)delivered.Single().Current["id"]);
            Assert.AreEqual("legacy", (string)delivered.Single().Previous["id"]);
            this.TestContext.WriteLine($"DECISION E22 currentHasMetadata={delivered.Single().Current["_ei"] != null}; previousHasMetadata={delivered.Single().Previous["_ei"] != null}; decryptCalls={encryptor.DecryptCalls}");
        }

#if NET8_0_OR_GREATER
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task StreamEncrypt_LateFailure_RecordsAccessorPublicationBoundary(bool accessor)
        {
            InvalidOperationException expected = new("second encryption failed");
            H.PublicEncryptor publicEncryptor = new();
            publicEncryptor.OnEncrypt = (bytes, algorithm, token) => publicEncryptor.EncryptCalls == 2
                ? Task.FromException<byte[]>(expected)
                : Task.FromResult(bytes);
            LateFailureKeyEncryptor keyEncryptor = new(expected);
            Encryptor encryptor = accessor ? keyEncryptor : publicEncryptor;
            JObject item = H.PlainItem();
            item["Other"] = "other";
            using MemoryStream input = H.Stream(item.ToString(Formatting.None));
            byte[] inputBefore = input.ToArray();
            using MemoryStream output = H.Output();
            byte[] before = output.ToArray();
            Exception failure = await H.CaptureExceptionAsync(() =>
                EncryptionProcessor.EncryptAsync(input, output, encryptor,
                    H.Options((int)JsonProcessor.Stream, H.Mde, "/Sensitive", "/Other").EncryptionOptions,
                    JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None));
            Assert.AreSame(expected, failure);
            Assert.IsTrue(input.CanRead);
            CollectionAssert.AreEqual(inputBefore, input.ToArray());
            if (!accessor)
            {
                CollectionAssert.AreEqual(before, output.ToArray());
                Assert.AreEqual(2L, output.Position);
            }

            this.Record("E16", "accessor=" + accessor, (int)JsonProcessor.Stream, failure,
                accessor ? keyEncryptor.Key.Calls : publicEncryptor.EncryptCalls, output.ToArray(), null);
        }

        private sealed class LateFailureKeyEncryptor : Encryptor, IDataEncryptionKeyAccessor
        {
            internal LateFailureKeyEncryptor(Exception failure)
            {
                this.Key = new LateFailureKey(failure);
            }

            internal LateFailureKey Key { get; }

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
                => Task.FromResult<DataEncryptionKey>(this.Key);

            public override Task<byte[]> EncryptAsync(byte[] plainText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
                => throw new AssertFailedException("The explicitly selected accessor path must use its key.");

            public override Task<byte[]> DecryptAsync(byte[] cipherText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
                => throw new AssertFailedException("Decryption is outside this accessor-only publication probe.");
        }

        private sealed class LateFailureKey : DataEncryptionKey
        {
            private readonly Exception failure;

            internal LateFailureKey(Exception failure)
            {
                this.failure = failure;
            }

            internal int Calls { get; private set; }

            public override byte[] RawKey => null;

            public override string EncryptionAlgorithm => H.Mde;

            public override byte[] EncryptData(byte[] plainText)
            {
                this.Calls++;
                if (this.Calls == 2)
                {
                    throw this.failure;
                }

                return plainText.ToArray();
            }

            public override byte[] DecryptData(byte[] cipherText) => throw new NotSupportedException();

            public override int EncryptData(byte[] plainText, int plainTextOffset, int plainTextLength, byte[] output, int outputOffset)
                => throw new NotSupportedException("No optional buffer capability.");

            public override int DecryptData(byte[] cipherText, int cipherTextOffset, int cipherTextLength, byte[] output, int outputOffset)
                => throw new NotSupportedException("No optional buffer capability.");

            public override int GetEncryptByteCount(int plainTextLength) => throw new NotSupportedException("No optional buffer capability.");

            public override int GetDecryptByteCount(int cipherTextLength) => throw new NotSupportedException("No optional buffer capability.");
        }
#endif

        private void Record(string family, string scenario, int processor, Exception failure, int cryptoCalls, byte[] bytes, DecryptionContext context)
        {
            this.TestContext.WriteLine("DECISION " + JsonConvert.SerializeObject(new
            {
                family,
                scenario,
                processor,
                error = failure?.GetType().FullName,
                message = failure?.Message,
                cryptoCalls,
                outputBase64 = Convert.ToBase64String(bytes),
                paths = context?.DecryptionInfoList.SelectMany(info => info.PathsDecrypted).ToArray(),
            }));
        }
    }
}
