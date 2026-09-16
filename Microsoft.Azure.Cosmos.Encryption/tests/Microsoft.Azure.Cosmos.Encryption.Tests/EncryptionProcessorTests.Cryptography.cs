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
    using Microsoft.Azure.Cosmos.Encryption.Tests.TestHelpers;

    /// <summary>
    /// Algorithm and cryptography tests for EncryptionProcessor including randomized algorithms,
    /// different encryption modes, and cryptographic behavior verification.
    /// </summary>
    public partial class EncryptionProcessorTests
    {
        #region Randomized Algorithm Tests

        private static Mde.AeadAes256CbcHmac256EncryptionAlgorithm CreateRandomizedAlgorithm() => TestCryptoHelpers.CreateAlgorithm(Mde.EncryptionType.Randomized);

        private static EncryptionSettings CreateRandomizedSettings(string propertyName, Mde.AeadAes256CbcHmac256EncryptionAlgorithm algo)
        {
            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            settings.SetEncryptionSettingForProperty(propertyName, new EncryptionSettingForProperty("cek1", Mde.EncryptionType.Randomized, container, "dbRid", algo));
            return settings;
        }

        [TestMethod]
        public async Task Cryptography_Randomized_Encrypt_Twice_DifferentCipher_SameDecrypt()
        {
            var algo = CreateRandomizedAlgorithm();
            var settings = CreateRandomizedSettings("Secret", algo);

            JObject doc = new JObject { ["id"] = "1", ["Secret"] = new JObject { ["a"] = 1, ["b"] = "x" } };

            using System.IO.Stream s1 = EncryptionProcessor.BaseSerializer.ToStream(doc);
            using System.IO.Stream e1 = await EncryptionProcessor.EncryptAsync(s1, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject enc1 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(e1);

            using System.IO.Stream s2 = EncryptionProcessor.BaseSerializer.ToStream(doc);
            using System.IO.Stream e2 = await EncryptionProcessor.EncryptAsync(s2, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject enc2 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(e2);

            // Ciphertexts under randomized encryption should differ
            Assert.IsFalse(JToken.DeepEquals(enc1["Secret"], enc2["Secret"]));

            // Both decrypt back to the original
            using System.IO.Stream d1 = await EncryptionProcessor.DecryptAsync(EncryptionProcessor.BaseSerializer.ToStream(enc1), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            using System.IO.Stream d2 = await EncryptionProcessor.DecryptAsync(EncryptionProcessor.BaseSerializer.ToStream(enc2), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject r1 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(d1);
            JObject r2 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(d2);
            Assert.IsTrue(JToken.DeepEquals(doc, r1));
            Assert.IsTrue(JToken.DeepEquals(doc, r2));
        }

        #endregion

        #region Deterministic vs Randomized Comparison Tests

        [TestMethod]
        public async Task Cryptography_Deterministic_Encrypt_Twice_SameCipher()
        {
            var detAlgo = CreateDeterministicAlgorithm();
            var settings = CreateSettingsWithInjected("Secret", detAlgo);

            JObject doc = new JObject { ["id"] = "1", ["Secret"] = "consistent value" };

            using System.IO.Stream s1 = EncryptionProcessor.BaseSerializer.ToStream(doc);
            using System.IO.Stream e1 = await EncryptionProcessor.EncryptAsync(s1, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject enc1 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(e1);

            using System.IO.Stream s2 = EncryptionProcessor.BaseSerializer.ToStream(doc);
            using System.IO.Stream e2 = await EncryptionProcessor.EncryptAsync(s2, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject enc2 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(e2);

            // Ciphertexts under deterministic encryption should be identical
            Assert.IsTrue(JToken.DeepEquals(enc1["Secret"], enc2["Secret"]));

            // Both decrypt back to the original
            using System.IO.Stream d1 = await EncryptionProcessor.DecryptAsync(EncryptionProcessor.BaseSerializer.ToStream(enc1), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            using System.IO.Stream d2 = await EncryptionProcessor.DecryptAsync(EncryptionProcessor.BaseSerializer.ToStream(enc2), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject r1 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(d1);
            JObject r2 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(d2);
            Assert.IsTrue(JToken.DeepEquals(doc, r1));
            Assert.IsTrue(JToken.DeepEquals(doc, r2));
        }

        #endregion

        #region Different Encryption Modes Tests

        [TestMethod]
        public async Task Cryptography_MixedEncryptionTypes_SingleDocument()
        {
            var detAlgo = CreateDeterministicAlgorithm();
            var randAlgo = CreateRandomizedAlgorithm();

            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            
            // Configure one property as deterministic, another as randomized
            settings.SetEncryptionSettingForProperty("DeterministicProp", new EncryptionSettingForProperty("cek1", Mde.EncryptionType.Deterministic, container, "dbRid", detAlgo));
            settings.SetEncryptionSettingForProperty("RandomizedProp", new EncryptionSettingForProperty("cek2", Mde.EncryptionType.Randomized, container, "dbRid", randAlgo));

            JObject doc = new JObject 
            { 
                ["id"] = "1", 
                ["DeterministicProp"] = "searchable value",
                ["RandomizedProp"] = "secure value",
                ["PlainProp"] = "unencrypted value"
            };

            using System.IO.Stream encrypted = await EncryptionProcessor.EncryptAsync(EncryptionProcessor.BaseSerializer.ToStream(doc), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            using System.IO.Stream decrypted = await EncryptionProcessor.DecryptAsync(encrypted, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            
            JObject result = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decrypted);
            Assert.IsTrue(JToken.DeepEquals(doc, result));
        }

        #endregion

        #region Key Management Tests

        [TestMethod]
        public async Task Cryptography_DifferentKeys_SameAlgorithm()
        {
            var algo1 = CreateDeterministicAlgorithm();
            var algo2 = CreateDeterministicAlgorithm(); // Different key internally

            var settings1 = CreateSettingsWithInjected("Secret", algo1);
            var settings2 = CreateSettingsWithInjected("Secret", algo2);

            JObject doc = new JObject { ["id"] = "1", ["Secret"] = "shared value" };

            // Encrypt with first key
            using System.IO.Stream s1 = EncryptionProcessor.BaseSerializer.ToStream(doc);
            using System.IO.Stream e1 = await EncryptionProcessor.EncryptAsync(s1, settings1, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject enc1 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(e1);

            // Encrypt with second key
            using System.IO.Stream s2 = EncryptionProcessor.BaseSerializer.ToStream(doc);
            using System.IO.Stream e2 = await EncryptionProcessor.EncryptAsync(s2, settings2, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject enc2 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(e2);

            // Different keys should produce different ciphertexts (even with deterministic encryption)
            Assert.IsFalse(JToken.DeepEquals(enc1["Secret"], enc2["Secret"]));

            // Each decrypts correctly with its own key
            using System.IO.Stream d1 = await EncryptionProcessor.DecryptAsync(EncryptionProcessor.BaseSerializer.ToStream(enc1), settings1, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            using System.IO.Stream d2 = await EncryptionProcessor.DecryptAsync(EncryptionProcessor.BaseSerializer.ToStream(enc2), settings2, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject r1 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(d1);
            JObject r2 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(d2);
            Assert.IsTrue(JToken.DeepEquals(doc, r1));
            Assert.IsTrue(JToken.DeepEquals(doc, r2));
        }

        #endregion

        #region Algorithm Behavior Tests

        [TestMethod]
        public void Cryptography_AlgorithmProperties_ValidConfiguration()
        {
            var detAlgo = CreateDeterministicAlgorithm();
            var randAlgo = CreateRandomizedAlgorithm();

            Assert.IsNotNull(detAlgo);
            Assert.IsNotNull(randAlgo);

            // These algorithms should be configured correctly for their respective encryption types
            // Additional property checks would require access to internal algorithm state
        }

        #endregion
    }
}
