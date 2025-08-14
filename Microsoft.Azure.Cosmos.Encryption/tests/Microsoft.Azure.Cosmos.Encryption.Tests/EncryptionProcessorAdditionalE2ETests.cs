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
    using Mde = Microsoft.Data.Encryption.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionProcessorAdditionalE2ETests
    {
        private static Mde.AeadAes256CbcHmac256EncryptionAlgorithm CreateDeterministicAlgorithm()
        {
            var fakeKek = new EncryptionProcessorEndToEndTests.TestKeyEncryptionKeyAccessor();
            var pdek = new Mde.ProtectedDataEncryptionKey("testPdek", fakeKek);
            return new Mde.AeadAes256CbcHmac256EncryptionAlgorithm(pdek, Mde.EncryptionType.Deterministic);
        }

        private static EncryptionSettings CreateSettings(string propertyName, Mde.AeadAes256CbcHmac256EncryptionAlgorithm algorithm)
        {
            var settings = new EncryptionSettings("rid", new List<string> { propertyName });
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            var forProperty = new EncryptionSettingForProperty(
                clientEncryptionKeyId: "cek1",
                encryptionType: Mde.EncryptionType.Deterministic,
                encryptionContainer: container,
                databaseRid: "dbRid",
                injectedAlgorithm: algorithm);
            settings.SetEncryptionSettingForProperty(propertyName, forProperty);
            return settings;
        }

        private static MemoryStream ToStream(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

        [TestMethod]
        public async Task RoundTrip_Unicode_And_Large_String_And_Null()
        {
            // Build a large unicode string including multi-byte characters.
            var sb = new StringBuilder();
            string chunk = "😀漢字🌍🔥";
            for (int i = 0; i < 10000; i++) sb.Append(chunk);
            string large = sb.ToString();

            var algo = CreateDeterministicAlgorithm();
            var settings = CreateSettings("Secret", algo);

            JObject doc = new JObject
            {
                ["id"] = "unicode-large",
                ["Secret"] = large,
                ["AlsoNull"] = null
            };

            using Stream encIn = EncryptionProcessor.BaseSerializer.ToStream(doc);
            var encDiag = new EncryptionDiagnosticsContext();
            using Stream encrypted = await EncryptionProcessor.EncryptAsync(encIn, settings, encDiag, CancellationToken.None);
            Assert.AreEqual(1, encDiag.EncryptContent[Constants.DiagnosticsPropertiesEncryptedCount].Value<int>());

            var decDiag = new EncryptionDiagnosticsContext();
            using Stream decrypted = await EncryptionProcessor.DecryptAsync(encrypted, settings, decDiag, CancellationToken.None);
            Assert.AreEqual(1, decDiag.DecryptContent[Constants.DiagnosticsPropertiesDecryptedCount].Value<int>());

            JObject round = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decrypted);
            Assert.AreEqual(large, round.Value<string>("Secret"));
            Assert.IsTrue(round.TryGetValue("AlsoNull", out JToken nullToken) && nullToken.Type == JTokenType.Null);
        }

        [TestMethod]
        public async Task Encrypt_JToken_DateTime_And_DateTimeOffset_ShouldThrow()
        {
            var algo = CreateDeterministicAlgorithm();
            var settings = CreateSettings("Secret", algo);

            // We must call the internal JToken-level API to feed a Date token directly.
            var dateToken = new JValue(new DateTime(2021, 01, 02, 03, 04, 05, DateTimeKind.Utc));
            var dtoToken = new JValue(new DateTimeOffset(2021, 01, 02, 03, 04, 05, TimeSpan.Zero));

            EncryptionSettingForProperty forProperty = settings.GetEncryptionSettingForProperty("Secret");

            // DateTime
            var ex1 = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                // Clone token since it’s replaced on success
                await EncryptionProcessor.EncryptJTokenAsync(dateToken.DeepClone(), forProperty, shouldEscape: false, cancellationToken: CancellationToken.None);
            });
            StringAssert.Contains(ex1.Message, "Invalid or Unsupported Data Type Passed");

            // DateTimeOffset
            var ex2 = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await EncryptionProcessor.EncryptJTokenAsync(dtoToken.DeepClone(), forProperty, shouldEscape: false, cancellationToken: CancellationToken.None);
            });
            StringAssert.Contains(ex2.Message, "Invalid or Unsupported Data Type Passed");
        }

        [TestMethod]
        public async Task Decrypt_Id_InvalidBase64_ShouldThrow()
        {
            var algo = CreateDeterministicAlgorithm();
            var settings = CreateSettings("id", algo);

            // Craft an invalid base64 (URL-safe) payload
            JObject enc = new JObject
            {
                ["id"] = "not_base64__--**",
                ["p"] = 1
            };

            using Stream input = EncryptionProcessor.BaseSerializer.ToStream(enc);
            await Assert.ThrowsExceptionAsync<FormatException>(async () =>
            {
                await EncryptionProcessor.DecryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            });
        }

        [TestMethod]
        public async Task DeserializeAndDecryptResponseAsync_Aggregates_Decrypted_Count()
        {
            var algo = CreateDeterministicAlgorithm();
            var settings = CreateSettings("Secret", algo);

            // Prepare original docs
            var originals = new[]
            {
                new JObject { ["id"] = "a", ["Secret"] = "one", ["x"] = 1 },
                new JObject { ["id"] = "b", ["Secret"] = "two", ["y"] = 2 },
            };

            // Encrypt individually
            JArray encryptedDocs = new JArray();
            foreach (JObject o in originals)
            {
                using Stream s = EncryptionProcessor.BaseSerializer.ToStream(o);
                using Stream es = await EncryptionProcessor.EncryptAsync(s, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
                encryptedDocs.Add(EncryptionProcessor.BaseSerializer.FromStream<JObject>(es));
            }

            // Compose feed response shape { "Documents": [ ... ] }
            JObject feed = new JObject { [Constants.DocumentsResourcePropertyName] = encryptedDocs };

            using Stream feedStream = EncryptionProcessor.BaseSerializer.ToStream(feed);
            var diag = new EncryptionDiagnosticsContext();
            using Stream decryptedFeedStream = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(feedStream, settings, diag, CancellationToken.None);
            Assert.AreEqual(2, diag.DecryptContent[Constants.DiagnosticsPropertiesDecryptedCount].Value<int>());

            // Validate both docs round-trip
            JObject decryptedFeed = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decryptedFeedStream);
            JArray resultDocs = (JArray)decryptedFeed[Constants.DocumentsResourcePropertyName]!;
            Assert.AreEqual(2, resultDocs.Count);
            for (int i = 0; i < originals.Length; i++)
            {
                Assert.IsTrue(JToken.DeepEquals(originals[i], (JObject)resultDocs[i]));
            }
        }

        [TestMethod]
        public async Task EncryptAsync_Ignores_Canceled_Token_CurrentBehavior()
        {
            var algo = CreateDeterministicAlgorithm();
            var settings = CreateSettings("Secret", algo);
            JObject doc = new JObject { ["id"] = "canceled-encrypt", ["Secret"] = "abc" };
            using Stream input = EncryptionProcessor.BaseSerializer.ToStream(doc);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Current implementation does not observe the token; this should still succeed.
            using Stream encrypted = await EncryptionProcessor.EncryptAsync(input, settings, operationDiagnostics: null, cancellationToken: cts.Token);
            using Stream decrypted = await EncryptionProcessor.DecryptAsync(encrypted, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject round = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decrypted);
            Assert.AreEqual("abc", round.Value<string>("Secret"));
        }

        [TestMethod]
        public async Task DeserializeAndDecryptResponseAsync_Ignores_Canceled_Token_CurrentBehavior()
        {
            var algo = CreateDeterministicAlgorithm();
            var settings = CreateSettings("Secret", algo);
            JObject original = new JObject { ["id"] = "canceled-feed", ["Secret"] = "abc" };
            using Stream s = EncryptionProcessor.BaseSerializer.ToStream(original);
            using Stream es = await EncryptionProcessor.EncryptAsync(s, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject encryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(es);
            JObject feed = new JObject { [Constants.DocumentsResourcePropertyName] = new JArray(encryptedDoc) };
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            using Stream feedStream = EncryptionProcessor.BaseSerializer.ToStream(feed);
            using Stream output = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(feedStream, settings, operationDiagnostics: null, cancellationToken: cts.Token);
            JObject outFeed = EncryptionProcessor.BaseSerializer.FromStream<JObject>(output);
            JArray docs = (JArray)outFeed[Constants.DocumentsResourcePropertyName]!;
            Assert.AreEqual("abc", ((JObject)docs[0]).Value<string>("Secret"));
        }

        // Expose the inner TestKeyEncryptionKey from the other test class to reuse here without duplication.
        private class EncryptionProcessorEndToEndTests
        {
            public class TestKeyEncryptionKeyAccessor : Mde.KeyEncryptionKey
            {
                public TestKeyEncryptionKeyAccessor() : base(name: "testKek", path: "test://kek", keyStoreProvider: new TestStoreProvider()) { }

                private class TestStoreProvider : Mde.EncryptionKeyStoreProvider
                {
                    public override string ProviderName => "testProvider";
                    public override byte[] UnwrapKey(string encryptionKeyId, Mde.KeyEncryptionKeyAlgorithm algorithm, byte[] encryptedKey) => encryptedKey;
                    public override byte[] WrapKey(string encryptionKeyId, Mde.KeyEncryptionKeyAlgorithm algorithm, byte[] key) => key;
                    public override byte[] Sign(string encryptionKeyId, bool allowEnclaveComputations) => new byte[] { 1, 2, 3 };
                    public override bool Verify(string encryptionKeyId, bool allowEnclaveComputations, byte[] signature) => true;
                }
            }
        }
    }
}
