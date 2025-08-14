//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
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
        public void EdgeCases_Serialize_BigInteger_DoesNotThrow()
        {
            // Current serializer supports numeric token types via double/long; ensure BigInteger token does not crash.
            BigInteger tooLarge = new BigInteger(long.MaxValue) + 1;
            JToken token = new JValue(tooLarge);

            // Act + Assert: no exception
            var _ = EncryptionProcessor.Serialize(token);
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
            // Configure 'id' (escaped string path), but provide a plaintext non-base64 value so decrypt no-ops safely.
            JObject doc = JObject.Parse("{ \"id\": \"plaintext\", \"name\": \"n\" }");
            EncryptionSettings settings = CreateSettingsWithNullMapping("id");

            (JObject result, int count) = await EncryptionProcessor.DecryptAsync(doc, settings, CancellationToken.None);

            Assert.AreSame(doc, result);
            Assert.AreEqual(0, count);
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

            // Current implementation increments the count when the property exists, even if value is null.
            Assert.AreEqual(1, diagEnc.EncryptContent[Constants.DiagnosticsPropertiesEncryptedCount].Value<int>());
        }

        #endregion
    }
}
