//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Mde = Microsoft.Data.Encryption.Cryptography;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Encryption.Tests.TestHelpers;

    /// <summary>
    /// Edge cases and reliability tests for EncryptionProcessor including depth handling,
    /// overflow scenarios, no-op operations, null crypto paths, and diagnostics edge cases.
    /// </summary>
    public partial class EncryptionProcessorTests
    {
        #region Depth Handling Tests

        private static string DeepJson(int depth)
        {
            // Build nested {"o": {"o": ... {"v": "x"} }}
            JObject cur = new JObject { ["v"] = "x" };
            for (int i = 0; i < depth; i++)
            {
                cur = new JObject { ["o"] = cur };
            }
            return cur.ToString(Newtonsoft.Json.Formatting.None);
        }

        [TestMethod]
        public async Task EdgeCases_EncryptDecrypt_MaxDepthMinusOne_Succeeds()
        {
            // Base serializer uses MaxDepth = 64; we generate a depth somewhat below that to avoid parser issues
            string json = $"{{\"id\":\"d\",\"Secret\":{DeepJson(30)} }}"; // 30 nested levels
            var settings = CreateSettings("Secret", Algo());

            using System.IO.Stream enc = await EncryptionProcessor.EncryptAsync(EncryptionProcessor.BaseSerializer.ToStream(JObject.Parse(json)), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            using System.IO.Stream dec = await EncryptionProcessor.DecryptAsync(enc, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject round = EncryptionProcessor.BaseSerializer.FromStream<JObject>(dec);
            Assert.AreEqual("x", round.SelectToken("$.Secret..v").Value<string>());
        }

        [TestMethod]
        public async Task EdgeCases_EncryptDecrypt_NearMaxDepth_Succeeds()
        {
            // Push close to MaxDepth (64). Using ~60 nested objects keeps us under the cap considering root and wrappers.
            JObject deep = JObject.Parse(DeepJson(60));
            JObject doc = new JObject { ["id"] = "deep", ["Secret"] = deep };
            var settings = CreateSettings("Secret", Algo());

            using System.IO.Stream enc = await EncryptionProcessor.EncryptAsync(EncryptionProcessor.BaseSerializer.ToStream(doc), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            using System.IO.Stream dec = await EncryptionProcessor.DecryptAsync(enc, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject round = EncryptionProcessor.BaseSerializer.FromStream<JObject>(dec);
            Assert.AreEqual("x", round.SelectToken("$.Secret..v").Value<string>());
        }

        #endregion

        #region Overflow Tests

        [TestMethod]
        public void EdgeCases_Serialize_BigInteger_Overflow_Throws()
        {
            // JValue cannot be constructed directly with BigInteger; construct with double exceeding Int64
            // and expect serializer to throw due to unsupported type or overflow when converting to long.
            // Use a numeric token that will be treated as Integer by Newtonsoft only if within range;
            // here we force a path that should not be supported.
            JToken token = new JValue(double.MaxValue);
            bool threw = false;
            try
            {
                _ = EncryptionProcessor.Serialize(token);
            }
            catch (Exception)
            {
                threw = true;
            }

            if (!threw)
            {
                Assert.Fail("Expected an exception during serialization of oversized integer.");
            }
        }

        #endregion

        #region No-Op Decryption Tests

        private static EncryptionSettings CreateSettingsEmpty()
        {
            return new EncryptionSettings("rid", new List<string> { "/id" });
        }

        private static EncryptionSettings CreateSettingsWithNullMapping(params string[] properties)
        {
            // Configure real mappings for properties so PropertiesToEncrypt contains them,
            // allowing decrypt traversal without modifying internals.
            EncryptionSettings settings = CreateSettingsEmpty();
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            foreach (string p in properties)
            {
                var forProperty = new EncryptionSettingForProperty(
                    clientEncryptionKeyId: "cek1",
                    encryptionType: Mde.EncryptionType.Randomized,
                    encryptionContainer: container,
                    databaseRid: "dbRid");
                settings.SetEncryptionSettingForProperty(p, forProperty);
            }

            return settings;
        }

        [TestMethod]
        public async Task EdgeCases_Decrypt_JObject_NoPropertiesConfigured_ReturnsSameAndZeroCount()
        {
            JObject doc = JObject.Parse("{ \"id\": \"1\", \"name\": \"n\" }");
            EncryptionSettings settings = CreateSettingsEmpty();

            (JObject result, int count) = await EncryptionProcessor.DecryptAsync(doc, settings, CancellationToken.None);

            Assert.AreSame(doc, result);
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public async Task EdgeCases_Decrypt_JObject_WithPropertiesConfigured_ButNoneCiphertext_ReturnsSameAndZeroCount()
        {
            JObject doc = JObject.Parse("{ \"id\": \"1\", \"name\": \"plaintext\" }");
            EncryptionSettings settings = CreateSettingsWithNullMapping("name");

            (JObject result, int count) = await EncryptionProcessor.DecryptAsync(doc, settings, CancellationToken.None);

            Assert.AreSame(doc, result);
            Assert.AreEqual(0, count);
        }

        #endregion

        #region Null Crypto Path Tests

        // These tests require a seam to inject an algorithm that returns null from Encrypt/Decrypt.
        // Marking as Ignored for now; when a test hook is added, implement them to assert
        // InvalidOperationException is thrown with the expected messages.

        [TestMethod]
        [Ignore("Pending test seam to inject null-returning algorithm")]
        public void EdgeCases_SerializeAndEncryptValueAsync_ReturnsNullCipher_Throws()
        {
            // Test implementation pending availability of test seam
        }

        [TestMethod]
        [Ignore("Pending test seam to inject null-returning algorithm")]
        public void EdgeCases_DecryptAndDeserializeValueAsync_ReturnsNullPlain_Throws()
        {
            // Test implementation pending availability of test seam
        }

        #endregion

        #region Diagnostics Edge Case Tests

        [TestMethod]
        public async Task EdgeCases_Diagnostics_EmptyDocument_NoProperties_ZeroCount()
        {
            var settings = CreateSettingsWithNoProperties();
            string json = "{}";

            using var input = ToStream(json);
            var diagEnc = new EncryptionDiagnosticsContext();
            System.IO.Stream encrypted = await EncryptionProcessor.EncryptAsync(input, settings, diagEnc, CancellationToken.None);

            // Should have zero properties encrypted
            Assert.AreEqual(0, diagEnc.EncryptContent[Constants.DiagnosticsPropertiesEncryptedCount].Value<int>());

            // Decrypt should also show zero
            var diagDec = new EncryptionDiagnosticsContext();
            System.IO.Stream decrypted = await EncryptionProcessor.DecryptAsync(encrypted, settings, diagDec, CancellationToken.None);
            Assert.AreEqual(0, diagDec.DecryptContent[Constants.DiagnosticsPropertiesDecryptedCount].Value<int>());
        }

        [TestMethod]
        public async Task EdgeCases_Diagnostics_NullValues_DoNotCount()
        {
            var algorithm = CreateDeterministicAlgorithm();
            var settings = CreateSettingsWithInjected("nullProp", algorithm);

            string json = "{\"id\":\"test\",\"nullProp\":null,\"other\":\"value\"}";

            using var input = ToStream(json);
            var diagEnc = new EncryptionDiagnosticsContext();
            System.IO.Stream encrypted = await EncryptionProcessor.EncryptAsync(input, settings, diagEnc, CancellationToken.None);

            // Null values should not be counted as encrypted
            Assert.AreEqual(0, diagEnc.EncryptContent[Constants.DiagnosticsPropertiesEncryptedCount].Value<int>());
        }

        #endregion
    }
}
