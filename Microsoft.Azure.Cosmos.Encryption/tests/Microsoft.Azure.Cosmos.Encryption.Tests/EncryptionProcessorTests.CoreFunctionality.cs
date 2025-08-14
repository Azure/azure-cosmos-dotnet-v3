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

    /// <summary>
    /// Core functionality tests for EncryptionProcessor including end-to-end encryption/decryption,
    /// stream handling, JSON traversal, and main processing flows.
    /// </summary>
    public partial class EncryptionProcessorTests
    {
        #region End-to-End Tests

        [TestMethod]
        public void CoreFunctionality_Placeholder_To_Keep_Class_NonEmpty()
        {
            // No-op; real tests below. Keeping a trivial non-noop method is not required,
            // but this preserves structure if additional setup is added later.
            Assert.IsTrue(true);
        }

        [TestMethod]
        public async Task EndToEnd_EncryptDecrypt_RoundTrip_Primitives_And_Arrays_And_Objects()
        {
            var algorithm = CreateDeterministicAlgorithm();

            // Configure two properties for encryption: one primitive/array mix, one nested object
            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));

            var cfg1 = new EncryptionSettingForProperty("cek1", Mde.EncryptionType.Deterministic, container, "dbRid", algorithm);
            var cfg2 = new EncryptionSettingForProperty("cek2", Mde.EncryptionType.Deterministic, container, "dbRid", algorithm);

            settings.SetEncryptionSettingForProperty("Secret", cfg1);
            settings.SetEncryptionSettingForProperty("Nested", cfg2);

            string json = @"{
                ""id"": ""document-id"",
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
        public async Task EndToEnd_EncryptDecrypt_Id_ShouldEscape_And_RoundTrip()
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

        #endregion

        #region Stream Handling Tests

        [TestMethod]
        public async Task StreamHandling_EncryptAsync_Disposes_Input_And_Returns_New_Stream()
        {
            // Arrange
            var input = ToStream("{\"id\":\"abc\",\"p\":1}");
            var settings = CreateSettingsWithNoProperties();

            // Act
            Stream result = await EncryptionProcessor.EncryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreNotSame(input, result); // Should be a different stream
            Assert.IsFalse(input.CanRead); // Original should be disposed
            Assert.IsTrue(result.CanRead);

            // Verify content
            result.Position = 0; // Reset to beginning
            string content = ReadToEnd(result);
            Assert.AreEqual("{\"id\":\"abc\",\"p\":1}", content);
        }

        [TestMethod]
        public async Task StreamHandling_DecryptAsync_Disposes_Input_And_Returns_New_Stream()
        {
            // Arrange
            var input = ToStream("{\"id\":\"abc\",\"p\":1}");
            var settings = CreateSettingsWithNoProperties();

            // Act
            Stream result = await EncryptionProcessor.DecryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreNotSame(input, result); // Should be a different stream
            Assert.IsFalse(input.CanRead); // Original should be disposed
            Assert.IsTrue(result.CanRead);

            // Verify content
            result.Position = 0; // Reset to beginning
            string content = ReadToEnd(result);
            Assert.AreEqual("{\"id\":\"abc\",\"p\":1}", content);
        }

        [TestMethod]
        public async Task StreamHandling_EncryptDecrypt_NoPropertiesToEncrypt_ReturnsPassthrough()
        {
            var settings = CreateSettingsWithNoProperties();
            string originalJson = "{\"id\":\"test\",\"data\":\"value\",\"array\":[1,2,3]}";

            using var input = ToStream(originalJson);
            Stream encrypted = await EncryptionProcessor.EncryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            encrypted.Position = 0;
            string encryptedJson = ReadToEnd(encrypted);

            using var encryptedInput = ToStream(encryptedJson);
            Stream decrypted = await EncryptionProcessor.DecryptAsync(encryptedInput, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            decrypted.Position = 0;
            string decryptedJson = ReadToEnd(decrypted);

            Assert.AreEqual(originalJson, decryptedJson);
        }

        #endregion

        #region JSON Traversal Tests

        private static JToken MakeNestedNullGraph()
        {
            return JToken.Parse("{ \"a\": null, \"b\": [ null, null, { \"c\": null } ], \"d\": { \"e\": [ null ] } }");
        }

        [TestMethod]
        public async Task Traversal_EncryptJTokenAsync_Traverses_Nested_ObjectArray_WithNullLeaves_NoCrypto()
        {
            JToken token = MakeNestedNullGraph();
            // encryptionSettingForProperty: null is okay because null leaves short-circuit before usage
            await EncryptionProcessor.EncryptJTokenAsync(token, encryptionSettingForProperty: null, shouldEscape: false, cancellationToken: CancellationToken.None);

            // Remains structurally the same (all null leaves)
            Assert.IsTrue(JToken.DeepEquals(token, MakeNestedNullGraph()));
        }

        [TestMethod]
        public async Task Traversal_DecryptJTokenAsync_Traverses_Nested_ObjectArray_WithNullLeaves_NoCrypto()
        {
            JToken token = MakeNestedNullGraph();
            // isEscaped = true to exercise that branch (as if property == "id")
            await EncryptionProcessor.DecryptJTokenAsync(token, encryptionSettingForProperty: null, isEscaped: true, cancellationToken: CancellationToken.None);

            Assert.IsTrue(JToken.DeepEquals(token, MakeNestedNullGraph()));
        }

        [TestMethod]
        public async Task Traversal_EncryptJTokenAsync_ShouldEscapeTrue_SubtreeTraversal_NoCrypto_WithStringIdPresent()
        {
            // Document has a string id, but we traverse only the 'sub' subtree which has null leaves.
            JObject doc = JObject.Parse("{ \"id\": \"abc\", \"sub\": { \"a\": null, \"b\": [ null, { \"c\": null } ] } }");

            // Take the subtree token to avoid touching the top-level id string.
            JToken subtree = doc["sub"];
            await EncryptionProcessor.EncryptJTokenAsync(subtree, encryptionSettingForProperty: null, shouldEscape: true, cancellationToken: CancellationToken.None);

            // The subtree should remain unchanged, and the id property should remain unchanged too.
            Assert.AreEqual("abc", doc.Value<string>("id"));
            Assert.IsTrue(JToken.DeepEquals(subtree, JToken.Parse("{ \"a\": null, \"b\": [ null, { \"c\": null } ] }")));
        }

        #endregion
    }
}
