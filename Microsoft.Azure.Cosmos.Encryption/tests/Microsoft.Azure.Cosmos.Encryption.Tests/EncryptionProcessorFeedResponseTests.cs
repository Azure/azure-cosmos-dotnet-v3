//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionProcessorFeedResponseTests
    {
    // Internals are visible to this test assembly; call the internal method directly.

        private static MemoryStream ToStream(string json)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);
        }

        private static EncryptionSettings CreateSettingsEmpty()
        {
            return new EncryptionSettings("rid", new List<string> { "/id" });
        }

        [TestMethod]
        public async Task DeserializeAndDecryptResponseAsync_EmptyDocumentsArray_NoOp()
        {
            string responseJson = "{ \"_count\": 0, \"Documents\": [] }";
            using var stream = ToStream(responseJson);
            EncryptionSettings settings = CreateSettingsEmpty();

            // With no properties to encrypt, method should return input as-is
            using Stream result = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(stream, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            // Expect same content structure
            JObject original = JObject.Parse(responseJson);
            JObject roundtrip = EncryptionProcessor.BaseSerializer.FromStream<JObject>(result);
            Assert.IsTrue(JToken.DeepEquals(original, roundtrip));
        }

        [TestMethod]
        public async Task DeserializeAndDecryptResponseAsync_EmptyDocumentsArray_WithConfiguredProps_NoOpButParses()
        {
            string responseJson = "{ \"_count\": 0, \"Documents\": [] }";
            using var stream = ToStream(responseJson);

            // Create settings with a mapping; no documents -> no-op
            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            settings.SetEncryptionSettingForProperty(
                "sensitive",
                new EncryptionSettingForProperty(
                    clientEncryptionKeyId: "cek1",
                    encryptionType: Microsoft.Data.Encryption.Cryptography.EncryptionType.Randomized,
                    encryptionContainer: container,
                    databaseRid: "dbRid"));

            using Stream result = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(stream, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            JObject original = JObject.Parse(responseJson);
            JObject roundtrip = EncryptionProcessor.BaseSerializer.FromStream<JObject>(result);
            Assert.IsTrue(JToken.DeepEquals(original, roundtrip));
        }

        [TestMethod]
        public async Task DeserializeAndDecryptResponseAsync_MixedDocuments_AggregatesDiagnosticsOnlyForObjects()
        {
            // Documents array with: object (has Sensitive: null), number, string, object (no Sensitive)
            string responseJson = "{\n  \"_count\": 4,\n  \"Documents\": [\n    { \"id\": \"1\", \"Sensitive\": null },\n    42,\n    \"hello\",\n    { \"id\": \"2\", \"Other\": true }\n  ]\n}";

            using var stream = ToStream(responseJson);

            // Build EncryptionSettings with a mapping for Sensitive; null value avoids crypto path.
            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            var forProperty = new EncryptionSettingForProperty(
                clientEncryptionKeyId: "cek1",
                encryptionType: Microsoft.Data.Encryption.Cryptography.EncryptionType.Randomized,
                encryptionContainer: container,
                databaseRid: "dbRid");
            settings.SetEncryptionSettingForProperty("Sensitive", forProperty);

            var diag = new EncryptionDiagnosticsContext();

            using Stream result = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(stream, settings, diag, CancellationToken.None);

            // Only the first object contains the configured property; expect count == 1
            Assert.IsNotNull(diag.DecryptContent);
            Assert.AreEqual(1, diag.DecryptContent[Constants.DiagnosticsPropertiesDecryptedCount].Value<int>());

            // Shape should remain intact.
            JObject roundtrip = EncryptionProcessor.BaseSerializer.FromStream<JObject>(result);
            Assert.IsTrue(JToken.DeepEquals(JObject.Parse(responseJson), roundtrip));
        }
    }
}
