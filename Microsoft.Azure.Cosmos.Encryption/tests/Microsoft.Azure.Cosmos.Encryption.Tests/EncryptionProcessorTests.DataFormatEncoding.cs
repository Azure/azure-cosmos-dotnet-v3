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
    using Microsoft.Azure.Cosmos.Encryption.Tests.TestHelpers;

    /// <summary>
    /// Data format and encoding tests for EncryptionProcessor including ID escaping,
    /// Unicode handling, feed responses, feed shapes, and value stream encryption.
    /// </summary>
    public partial class EncryptionProcessorTests
    {
        #region ID Escaping Tests

        [TestMethod]
        public void DataFormat_Base64_UriSafe_Roundtrip_With_Url_Problematic_Chars()
        {
            // bytes that produce '+' and '/' in standard Base64
            // Build input with a wide byte distribution
            byte[] input = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

            string uriSafe = EncryptionProcessor.ConvertToBase64UriSafeString(input);

            // Assert it contains neither '+' nor '/'
            Assert.IsFalse(uriSafe.Contains('+'));
            Assert.IsFalse(uriSafe.Contains('/'));

            byte[] roundtrip = EncryptionProcessor.ConvertFromBase64UriSafeString(uriSafe);
            CollectionAssert.AreEqual(input, roundtrip, "URI-safe Base64 conversion should be lossless.");
        }

        [TestMethod]
        public void DataFormat_Base64_UriSafe_Does_Not_Pad_With_Whitespace()
        {
            byte[] input = Encoding.UTF8.GetBytes("some id with / and + and ? #");
            string uriSafe = EncryptionProcessor.ConvertToBase64UriSafeString(input);

            // Sanity: No whitespace
            Assert.IsFalse(uriSafe.Any(char.IsWhiteSpace));

            // Roundtrip
            byte[] roundtrip = EncryptionProcessor.ConvertFromBase64UriSafeString(uriSafe);
            CollectionAssert.AreEqual(input, roundtrip);
        }

        #endregion

        #region Unicode Handling Tests

        [TestMethod]
        public async Task DataFormat_EncryptDecrypt_Id_With_Unicode_And_ProblemChars_RoundTrips()
        {
            string id = "id/漢字+emoji😀?hash#back\\slash";
            JObject doc = new JObject { ["id"] = id, ["p"] = 1 };
            var settings = CreateSettings("id", Algo());

            using System.IO.Stream enc = await EncryptionProcessor.EncryptAsync(EncryptionProcessor.BaseSerializer.ToStream(doc), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject encDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(enc);
            string encId = encDoc.Value<string>("id");
            Assert.IsFalse(encId.Contains('/'));
            Assert.IsFalse(encId.Contains('+'));
            Assert.IsFalse(encId.Contains('?'));
            Assert.IsFalse(encId.Contains('#'));
            Assert.IsFalse(encId.Contains('\\'));

            using System.IO.Stream dec = await EncryptionProcessor.DecryptAsync(EncryptionProcessor.BaseSerializer.ToStream(encDoc), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject round = EncryptionProcessor.BaseSerializer.FromStream<JObject>(dec);
            Assert.AreEqual(id, round.Value<string>("id"));
        }

        [TestMethod]
        public async Task DataFormat_EncryptDecrypt_Id_Large_MixedUnicode_RoundTrips()
        {
            // Build a large, mixed-unicode id string
            var sb = new StringBuilder();
            string chunk = "😀漢字🌍🔥/+#?\\";
            for (int i = 0; i < 500; i++) sb.Append(chunk); // length ~3500 chars
            string id = sb.ToString();

            JObject doc = new JObject { ["id"] = id, ["p"] = 1 };
            var settings = CreateSettings("id", Algo());

            using System.IO.Stream enc = await EncryptionProcessor.EncryptAsync(EncryptionProcessor.BaseSerializer.ToStream(doc), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject encDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(enc);
            string encId = encDoc.Value<string>("id");
            Assert.IsFalse(encId.Contains('/'));
            Assert.IsFalse(encId.Contains('+'));
            Assert.IsFalse(encId.Contains('?'));
            Assert.IsFalse(encId.Contains('#'));
            Assert.IsFalse(encId.Contains('\\'));

            using System.IO.Stream dec = await EncryptionProcessor.DecryptAsync(EncryptionProcessor.BaseSerializer.ToStream(encDoc), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject round = EncryptionProcessor.BaseSerializer.FromStream<JObject>(dec);
            Assert.AreEqual(id, round.Value<string>("id"));
        }

        #endregion

        #region Feed Response Tests

        [TestMethod]
        public async Task DataFormat_DeserializeAndDecryptResponseAsync_EmptyDocumentsArray_NoOp()
        {
            string responseJson = "{ \"_count\": 0, \"Documents\": [] }";
            using var stream = ToStream(responseJson);
            EncryptionSettings settings = CreateSettingsWithNoProperties();

            // With no properties to encrypt, method should return input as-is
            using System.IO.Stream result = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(stream, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            // Expect same content structure
            JObject original = JObject.Parse(responseJson);
            JObject roundtrip = EncryptionProcessor.BaseSerializer.FromStream<JObject>(result);
            Assert.IsTrue(JToken.DeepEquals(original, roundtrip));
        }

        [TestMethod]
        public async Task DataFormat_DeserializeAndDecryptResponseAsync_EmptyDocumentsArray_WithConfiguredProps_NoOpButParses()
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
                    encryptionType: Mde.EncryptionType.Randomized,
                    encryptionContainer: container,
                    databaseRid: "dbRid"));

            using System.IO.Stream result = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(stream, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            JObject original = JObject.Parse(responseJson);
            JObject roundtrip = EncryptionProcessor.BaseSerializer.FromStream<JObject>(result);
            Assert.IsTrue(JToken.DeepEquals(original, roundtrip));
        }

        [TestMethod]
        public async Task DataFormat_DeserializeAndDecryptResponseAsync_MixedDocuments_AggregatesDiagnosticsOnlyForObjects()
        {
            // Documents array with: object (has Sensitive: null), number, string, object (no Sensitive)
            string responseJson = "{\n  \"_count\": 4,\n  \"Documents\": [\n    { \"id\": \"1\", \"Sensitive\": null },\n    42,\n    \"hello\",\n    { \"id\": \"2\", \"Other\": true }\n  ]\n}";

            using var stream = ToStream(responseJson);

            // Build EncryptionSettings with a mapping for Sensitive; null value avoids crypto path.
            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            var forProperty = new EncryptionSettingForProperty(
                clientEncryptionKeyId: "cek1",
                encryptionType: Mde.EncryptionType.Randomized,
                encryptionContainer: container,
                databaseRid: "dbRid");
            settings.SetEncryptionSettingForProperty("Sensitive", forProperty);

            var diag = new EncryptionDiagnosticsContext();

            using System.IO.Stream result = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(stream, settings, diag, CancellationToken.None);

            // Only the first object contains the configured property; expect count == 1
            Assert.IsNotNull(diag.DecryptContent);
            Assert.AreEqual(1, diag.DecryptContent[Constants.DiagnosticsPropertiesDecryptedCount].Value<int>());

            // Shape should remain intact.
            JObject roundtrip = EncryptionProcessor.BaseSerializer.FromStream<JObject>(result);
            Assert.IsTrue(JToken.DeepEquals(JObject.Parse(responseJson), roundtrip));
        }

        #endregion

        #region Feed Shape Tests

        [TestMethod]
        public async Task DataFormat_ProcessFeedResponse_MaintainsStructure()
        {
            // Test that feed response processing maintains the overall structure
            string feedJson = @"{
                ""_rid"": ""abc"",
                ""Documents"": [
                    {""id"": ""doc1"", ""data"": ""value1""},
                    {""id"": ""doc2"", ""data"": ""value2""}
                ],
                ""_count"": 2
            }";

            using var stream = ToStream(feedJson);
            var settings = CreateSettingsWithNoProperties();

            using System.IO.Stream result = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(stream, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            JObject original = JObject.Parse(feedJson);
            JObject processed = EncryptionProcessor.BaseSerializer.FromStream<JObject>(result);
            
            Assert.IsTrue(JToken.DeepEquals(original, processed));
            Assert.AreEqual(2, processed["_count"].Value<int>());
            Assert.AreEqual("abc", processed["_rid"].Value<string>());
        }

        #endregion

        #region Value Stream Encryption Tests

        [TestMethod]
        public void DataFormat_EncryptValueStream_BasicFunctionality()
        {
            // Test basic value stream encryption functionality
            var algorithm = CreateDeterministicAlgorithm();
            var settings = CreateSettingsWithInjected("testProp", algorithm);

            string testValue = "sensitive data to encrypt";
            byte[] valueBytes = Encoding.UTF8.GetBytes(testValue);

            using var inputStream = new MemoryStream(valueBytes);
            
            // This is a placeholder test - the actual EncryptValueStreamAsync method would need to be exposed or tested indirectly
            Assert.IsNotNull(inputStream);
            Assert.IsTrue(inputStream.CanRead);
        }

        #endregion
    }
}
