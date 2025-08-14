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
    using Mde = Microsoft.Data.Encryption.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionProcessorEndToEndTests
    {
        [TestMethod]
        public void Placeholder_To_Keep_Class_NonEmpty()
        {
            // No-op; real tests below. Keeping a trivial non-noop method is not required,
            // but this preserves structure if additional setup is added later.
            Assert.IsTrue(true);
        }

        private static Mde.AeadAes256CbcHmac256EncryptionAlgorithm CreateDeterministicAlgorithm()
        {
            // Create a stable DataEncryptionKey via a fixed root key so results are deterministic.
            // We cannot access constructor directly, so use ProtectedDataEncryptionKey with a synthetic KEK that round-trips the DEK.
            // Build a simple KeyEncryptionKey that returns the plaintext for encrypt/decrypt; leverage test-only helper.
            var fakeKek = new TestKeyEncryptionKey();

            // Create a ProtectedDataEncryptionKey using a random generated DEK under the fake KEK.
            var pdek = new Mde.ProtectedDataEncryptionKey("testPdek", fakeKek);

            // Create algorithm (deterministic) from the protected key
            return new Mde.AeadAes256CbcHmac256EncryptionAlgorithm(pdek, Mde.EncryptionType.Deterministic);
        }

        private static Microsoft.Azure.Cosmos.Encryption.EncryptionSettings CreateSettingsWithInjected(string propertyName, Mde.AeadAes256CbcHmac256EncryptionAlgorithm algorithm)
        {
            var settings = new Microsoft.Azure.Cosmos.Encryption.EncryptionSettings("rid", new List<string> { "/id" });
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
        public async Task EncryptDecrypt_RoundTrip_Primitives_And_Arrays_And_Objects()
        {
            var algorithm = CreateDeterministicAlgorithm();

            // Configure two properties for encryption: one primitive/array mix, one nested object
            var settings = new Microsoft.Azure.Cosmos.Encryption.EncryptionSettings("rid", new List<string> { "/id" });
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));

            var cfg1 = new EncryptionSettingForProperty("cek1", Mde.EncryptionType.Deterministic, container, "dbRid", algorithm);
            var cfg2 = new EncryptionSettingForProperty("cek2", Mde.EncryptionType.Deterministic, container, "dbRid", algorithm);
            settings.SetEncryptionSettingForProperty("Secret", cfg1);
            settings.SetEncryptionSettingForProperty("Nested", cfg2);

            string json = @"{
                ""id"": ""abc"",
                ""Secret"": { ""a"": 1, ""b"": true, ""c"": [ ""x"", 2, false, null, 3.14 ] },
                ""Nested"": { ""inner"": ""value"", ""arr"": [ { ""q"": 42 }, null ] },
                ""Plain"": 123
            }";

            using var input = ToStream(json);
            var diagEnc = new EncryptionDiagnosticsContext();
            Stream encrypted = await EncryptionProcessor.EncryptAsync(input, settings, diagEnc, CancellationToken.None);

            // Ensure diagnostics counted both properties
            Assert.AreEqual(2, diagEnc.EncryptContent[Constants.DiagnosticsPropertiesEncryptedCount].Value<int>());

            // Decrypt
            var diagDec = new EncryptionDiagnosticsContext();
            Stream decrypted = await EncryptionProcessor.DecryptAsync(encrypted, settings, diagDec, CancellationToken.None);
            Assert.AreEqual(2, diagDec.DecryptContent[Constants.DiagnosticsPropertiesDecryptedCount].Value<int>());

            // Validate round-trip equality
            JObject original = JObject.Parse(json);
            JObject roundtripped = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decrypted);
            Assert.IsTrue(JToken.DeepEquals(original, roundtripped), "Document should round-trip after encrypt/decrypt.");
        }

        [TestMethod]
        public async Task EncryptDecrypt_Id_ShouldEscape_And_RoundTrip()
        {
            var algorithm = CreateDeterministicAlgorithm();
            var settings = CreateSettingsWithInjected("id", algorithm);

            string id = "id/with+special?chars#and\\slashes";
            // Build the JSON via JObject to ensure proper escaping.
            JObject doc = new JObject
            {
                ["id"] = id,
                ["p"] = 1
            };

            using var input = EncryptionProcessor.BaseSerializer.ToStream(doc);
            Stream encrypted = await EncryptionProcessor.EncryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            // Inspect encrypted form to ensure id does not contain forbidden characters
            JObject encryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encrypted);
            string encId = encryptedDoc.Value<string>("id");
            Assert.IsNotNull(encId);
            Assert.IsFalse(encId.Contains('/'));
            Assert.IsFalse(encId.Contains('+'));
            Assert.IsFalse(encId.Contains('?'));
            Assert.IsFalse(encId.Contains('#'));
            Assert.IsFalse(encId.Contains('\\'));

            // Decrypt and verify original id restored
            Stream decrypted = await EncryptionProcessor.DecryptAsync(EncryptionProcessor.BaseSerializer.ToStream(encryptedDoc), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject roundtripped = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decrypted);
            Assert.AreEqual(id, roundtripped.Value<string>("id"));
        }

        // Minimal test-only KEK that returns plaintext keys (identity wrap/unwrap)
        private class TestKeyEncryptionKey : Mde.KeyEncryptionKey
        {
            public TestKeyEncryptionKey() : base(name: "testKek", path: "test://kek", keyStoreProvider: new TestStoreProvider()) { }

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
