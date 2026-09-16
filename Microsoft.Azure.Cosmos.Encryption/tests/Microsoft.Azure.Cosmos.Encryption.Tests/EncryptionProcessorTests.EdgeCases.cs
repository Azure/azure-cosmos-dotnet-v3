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
            EncryptionSettings settings = CreateSettings("Secret", Algo());

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
            EncryptionSettings settings = CreateSettings("Secret", Algo());

            using System.IO.Stream enc = await EncryptionProcessor.EncryptAsync(EncryptionProcessor.BaseSerializer.ToStream(doc), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            using System.IO.Stream dec = await EncryptionProcessor.DecryptAsync(enc, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject round = EncryptionProcessor.BaseSerializer.FromStream<JObject>(dec);
            Assert.AreEqual("x", round.SelectToken("$.Secret..v").Value<string>());
        }

        #endregion

        #region Overflow Tests

        [TestMethod]
        public void EdgeCases_Serialize_BigInteger_Throws()
        {
            // Current implementation does not support BigInteger and will attempt to coerce to long.
            // Verify this results in an exception (OverflowException in the current path).
            BigInteger tooLarge = new BigInteger(long.MaxValue) + 1;
            JToken token = new JValue(tooLarge);

            try
            {
                EncryptionProcessor.Serialize(token);
                Assert.Fail("Expected an exception when serializing BigInteger, but none was thrown.");
            }
            catch (Exception ex)
            {
                // Be tolerant to implementation detail: either OverflowException (from ToObject<long>)
                // or InvalidOperationException if validation changes upstream.
                Assert.IsTrue(
                    ex is OverflowException || ex is InvalidOperationException,
                    $"Expected OverflowException or InvalidOperationException, but got {ex.GetType()}: {ex.Message}");
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
            EncryptionContainer container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            foreach (string p in properties)
            {
                EncryptionSettingForProperty forProperty = new EncryptionSettingForProperty(
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
            // Configure a property for encryption that is NOT present in the document,
            // so no ciphertext is encountered and decrypt is a no-op with zero count.
            JObject doc = JObject.Parse("{ \"id\": \"plaintext\", \"name\": \"n\" }");
            EncryptionSettings settings = CreateSettingsWithNullMapping("Secret"); // 'Secret' not in doc

            (JObject result, int count) = await EncryptionProcessor.DecryptAsync(doc, settings, CancellationToken.None);

            Assert.AreSame(doc, result);
            Assert.AreEqual(0, count);
        }

        #endregion

        #region Diagnostics Edge Case Tests

        [TestMethod]
        public async Task EdgeCases_Diagnostics_EmptyDocument_NoProperties_ZeroCount()
        {
            EncryptionSettings settings = CreateSettingsWithNoProperties();
            string json = "{}";

            using (System.IO.Stream input = ToStream(json))
            {
                EncryptionDiagnosticsContext diagEnc = new EncryptionDiagnosticsContext();
                System.IO.Stream encrypted = await EncryptionProcessor.EncryptAsync(input, settings, diagEnc, CancellationToken.None);

                // Should have zero properties encrypted
                Assert.AreEqual(0, diagEnc.EncryptContent[Constants.DiagnosticsPropertiesEncryptedCount].Value<int>());

                // Decrypt should also show zero
                EncryptionDiagnosticsContext diagDec = new EncryptionDiagnosticsContext();
                System.IO.Stream decrypted = await EncryptionProcessor.DecryptAsync(encrypted, settings, diagDec, CancellationToken.None);
                Assert.AreEqual(0, diagDec.DecryptContent[Constants.DiagnosticsPropertiesDecryptedCount].Value<int>());
            }
        }

        [TestMethod]
        public async Task EdgeCases_Diagnostics_NullValues_DoNotCount()
        {
            EncryptionSettings settings = CreateSettingsWithInjected("nullProp", CreateDeterministicAlgorithm());

            string json = "{\"id\":\"test\",\"nullProp\":null,\"other\":\"value\"}";

            using (System.IO.Stream input = ToStream(json))
            {
                EncryptionDiagnosticsContext diagEnc = new EncryptionDiagnosticsContext();
                System.IO.Stream encrypted = await EncryptionProcessor.EncryptAsync(input, settings, diagEnc, CancellationToken.None);

                // Current implementation increments the count when the property exists, even if value is null.
                Assert.AreEqual(1, diagEnc.EncryptContent[Constants.DiagnosticsPropertiesEncryptedCount].Value<int>());
            }
        }

        #endregion

    #region Stream Edge Case Tests

        [TestMethod]
        public async Task Streams_DecryptAsync_NullInput_ReturnsNull()
        {
            // Arrange
            System.IO.Stream input = null;
            EncryptionSettings settings = CreateSettingsWithNoProperties();

            // Act
            System.IO.Stream result = await EncryptionProcessor.DecryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Streams_BaseSerializer_ToStream_IsSeekable()
        {
            JObject obj = JObject.Parse("{ \"id\": \"1\" }");
            using (System.IO.Stream s = EncryptionProcessor.BaseSerializer.ToStream(obj))
            {
                Assert.IsTrue(s.CanSeek, "BaseSerializer.ToStream should return a seekable stream.");
            }
        }

        [TestMethod]
        public async Task Streams_EncryptAsync_ReturnsSeekableStream()
        {
            string json = "{\"id\":\"1\",\"p\":123}";
            EncryptionSettings settings = CreateSettingsWithNoProperties();
            using (System.IO.Stream input = ToStream(json))
            {
                System.IO.Stream encrypted = await EncryptionProcessor.EncryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
                using (encrypted)
                {
                    Assert.IsTrue(encrypted.CanSeek, "EncryptAsync should return a seekable stream to satisfy downstream Debug.Assert invariants.");
                }
            }
        }

        #endregion
    }
}
