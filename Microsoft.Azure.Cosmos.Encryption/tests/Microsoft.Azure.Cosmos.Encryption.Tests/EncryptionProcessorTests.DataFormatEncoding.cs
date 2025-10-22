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
            EncryptionSettings settings = CreateSettings("id", Algo());

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
            StringBuilder sb = new StringBuilder();
            string chunk = "😀漢字🌍🔥/+#?\\";
            for (int i = 0; i < 500; i++) sb.Append(chunk); // length ~3500 chars
            string id = sb.ToString();

            JObject doc = new JObject { ["id"] = id, ["p"] = 1 };
            EncryptionSettings settings = CreateSettings("id", Algo());

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
        public async Task DataFormat_DeserializeAndDecryptResponseAsync_Throws_When_Documents_NotArrayOrMissing()
        {
            // Arrange: Ensure PropertiesToEncrypt.Any() == true so we don't early-return
            EncryptionSettings settings = new EncryptionSettings("rid", new List<string> { "/id" });
            EncryptionContainer container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            settings.SetEncryptionSettingForProperty(
                "Sensitive",
                new EncryptionSettingForProperty(
                    clientEncryptionKeyId: "cek1",
                    encryptionType: Mde.EncryptionType.Randomized,
                    encryptionContainer: container,
                    databaseRid: "dbRid"));

            // Case 1: Missing Documents property
            using (System.IO.MemoryStream s1 = ToStream("{ \"_count\": 0 }"))
            {
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => EncryptionProcessor.DeserializeAndDecryptResponseAsync(s1, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None));
            }

            // Case 2: Documents is not an array (object)
            using (System.IO.MemoryStream s2 = ToStream("{ \"_count\": 1, \"Documents\": { \"id\": \"1\" } }"))
            {
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => EncryptionProcessor.DeserializeAndDecryptResponseAsync(s2, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None));
            }

            // Case 3: Documents is not an array (string)
            using (System.IO.MemoryStream s3 = ToStream("{ \"_count\": 1, \"Documents\": \"oops\" }"))
            {
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => EncryptionProcessor.DeserializeAndDecryptResponseAsync(s3, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None));
            }
        }

        [TestMethod]
        public async Task DataFormat_DeserializeAndDecryptResponseAsync_EmptyDocumentsArray_NoOp()
        {
            string responseJson = "{ \"_count\": 0, \"Documents\": [] }";
            using (System.IO.MemoryStream stream = ToStream(responseJson))
            {
                EncryptionSettings settings = CreateSettingsWithNoProperties();

                // With no properties to encrypt, method should return input as-is
                using System.IO.Stream result = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(stream, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

                // Expect same content structure
                JObject original = JObject.Parse(responseJson);
                JObject roundtrip = EncryptionProcessor.BaseSerializer.FromStream<JObject>(result);
                Assert.IsTrue(JToken.DeepEquals(original, roundtrip));
            }
        }

        [TestMethod]
        public async Task DataFormat_DeserializeAndDecryptResponseAsync_EmptyDocumentsArray_WithConfiguredProps_NoOpButParses()
        {
            string responseJson = "{ \"_count\": 0, \"Documents\": [] }";
            using (System.IO.MemoryStream stream = ToStream(responseJson))
            {
                // Create settings with a mapping; no documents -> no-op
                EncryptionSettings settings = new EncryptionSettings("rid", new List<string> { "/id" });
                EncryptionContainer container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
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
        }

        [TestMethod]
        public async Task DataFormat_DeserializeAndDecryptResponseAsync_MixedDocuments_AggregatesDiagnosticsOnlyForObjects()
        {
            // Documents array with: object (has Sensitive: null), number, string, object (no Sensitive)
            string responseJson = "{\n  \"_count\": 4,\n  \"Documents\": [\n    { \"id\": \"1\", \"Sensitive\": null },\n    42,\n    \"hello\",\n    { \"id\": \"2\", \"Other\": true }\n  ]\n}";

            using (System.IO.MemoryStream stream = ToStream(responseJson))
            {
                // Build EncryptionSettings with a mapping for Sensitive; null value avoids crypto path.
                EncryptionSettings settings = new EncryptionSettings("rid", new List<string> { "/id" });
                EncryptionContainer container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
                EncryptionSettingForProperty forProperty = new EncryptionSettingForProperty(
                    clientEncryptionKeyId: "cek1",
                    encryptionType: Mde.EncryptionType.Randomized,
                    encryptionContainer: container,
                    databaseRid: "dbRid");
                settings.SetEncryptionSettingForProperty("Sensitive", forProperty);

                EncryptionDiagnosticsContext diag = new EncryptionDiagnosticsContext();

                using System.IO.Stream result = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(stream, settings, diag, CancellationToken.None);

                // Only the first object contains the configured property; expect count == 1
                Assert.IsNotNull(diag.DecryptContent);
                Assert.AreEqual(1, diag.DecryptContent[Constants.DiagnosticsPropertiesDecryptedCount].Value<int>());

                // Shape should remain intact.
                JObject roundtrip = EncryptionProcessor.BaseSerializer.FromStream<JObject>(result);
                Assert.IsTrue(JToken.DeepEquals(JObject.Parse(responseJson), roundtrip));
            }
        }

        [TestMethod]
        public async Task DataFormat_FeedResponse_MultipleDocs_Aggregates_Count()
        {
            // Arrange: two documents with the configured property present
            string responseJson = "{\n  \"_count\": 2,\n  \"Documents\": [\n    { \"id\": \"1\", \"Sensitive\": null },\n    { \"id\": \"2\", \"Sensitive\": null }\n  ]\n}";

            using (System.IO.MemoryStream stream = ToStream(responseJson))
            {
                EncryptionSettings settings = new EncryptionSettings("rid", new List<string> { "/id" });
                EncryptionContainer container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
                EncryptionSettingForProperty forProperty = new EncryptionSettingForProperty(
                    clientEncryptionKeyId: "cek1",
                    encryptionType: Mde.EncryptionType.Randomized,
                    encryptionContainer: container,
                    databaseRid: "dbRid");
                settings.SetEncryptionSettingForProperty("Sensitive", forProperty);

                EncryptionDiagnosticsContext diag = new EncryptionDiagnosticsContext();
                using System.IO.Stream result = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(stream, settings, diag, CancellationToken.None);

                // Both objects have the property present -> count should be 2
                Assert.IsNotNull(diag.DecryptContent);
                Assert.AreEqual(2, diag.DecryptContent[Constants.DiagnosticsPropertiesDecryptedCount].Value<int>());

                // Shape preserved
                JObject roundtrip = EncryptionProcessor.BaseSerializer.FromStream<JObject>(result);
                Assert.IsTrue(JToken.DeepEquals(JObject.Parse(responseJson), roundtrip));
            }
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

            using (System.IO.MemoryStream stream = ToStream(feedJson))
            {
                EncryptionSettings settings = CreateSettingsWithNoProperties();

                using System.IO.Stream result = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(stream, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

                JObject original = JObject.Parse(feedJson);
                JObject processed = EncryptionProcessor.BaseSerializer.FromStream<JObject>(result);
                
                Assert.IsTrue(JToken.DeepEquals(original, processed));
                Assert.AreEqual(2, processed["_count"].Value<int>());
                Assert.AreEqual("abc", processed["_rid"].Value<string>());
            }
        }

        #endregion

        #region Value Stream Encryption Tests

        [TestMethod]
        public async Task DataFormat_EncryptValueStream_Scalar_String_ShouldEscapeFalse_RoundTrip()
        {
            Mde.AeadAes256CbcHmac256EncryptionAlgorithm algo = Algo();
            // Create settings just to reuse its configured property settings
            EncryptionSettings settings = CreateSettings("s", algo);
            EncryptionSettingForProperty propSetting = settings.GetEncryptionSettingForProperty("s");

            using (System.IO.MemoryStream valueStream = ToStream("\"hello\""))
            {
                using System.IO.Stream enc = await EncryptionProcessor.EncryptValueStreamAsync(valueStream, propSetting, shouldEscape: false, cancellationToken: CancellationToken.None);
                JToken encryptedToken = EncryptionProcessor.BaseSerializer.FromStream<JToken>(enc);

                // Decrypt via wrapper
                JObject wrapper = new JObject { ["s"] = encryptedToken };
                await EncryptionProcessor.DecryptJTokenAsync(wrapper["s"], propSetting, isEscaped: false, cancellationToken: CancellationToken.None);
                Assert.AreEqual("hello", wrapper.Value<string>("s"));
            }
        }

        [TestMethod]
        public async Task DataFormat_EncryptValueStream_Scalar_String_ShouldEscapeTrue_RoundTrip()
        {
            Mde.AeadAes256CbcHmac256EncryptionAlgorithm algo = Algo();
            EncryptionSettings settings = CreateSettings("s", algo);
            EncryptionSettingForProperty propSetting = settings.GetEncryptionSettingForProperty("s");

            using (System.IO.MemoryStream valueStream = ToStream("\"id/with+chars?#\"")) // JSON string: id/with+chars?#
            {
                using System.IO.Stream enc = await EncryptionProcessor.EncryptValueStreamAsync(valueStream, propSetting, shouldEscape: true, cancellationToken: CancellationToken.None);
                JToken encryptedToken = EncryptionProcessor.BaseSerializer.FromStream<JToken>(enc);

                // Encrypted token must be a URI-safe base64 string
                string cipher = encryptedToken.Value<string>();
                Assert.IsFalse(cipher.Contains('/'));
                Assert.IsFalse(cipher.Contains('+'));
                Assert.IsFalse(cipher.Contains('?'));
                Assert.IsFalse(cipher.Contains('#'));
                Assert.IsFalse(cipher.Contains('\\'));

                // Decrypt via wrapper
                JObject wrapper = new JObject { ["s"] = encryptedToken };
                await EncryptionProcessor.DecryptJTokenAsync(wrapper["s"], propSetting, isEscaped: true, cancellationToken: CancellationToken.None);
                Assert.AreEqual("id/with+chars?#", wrapper.Value<string>("s"));
            }
        }

        [TestMethod]
        public async Task DataFormat_EncryptValueStream_Object_Traverse_Encrypts_Leaves_RoundTrip()
        {
            Mde.AeadAes256CbcHmac256EncryptionAlgorithm algo = Algo();
            EncryptionSettings settings = CreateSettings("s", algo);
            EncryptionSettingForProperty propSetting = settings.GetEncryptionSettingForProperty("s");

            string payload = "{\"a\":1,\"b\":\"x\",\"c\":null,\"d\":[true,2,\"y\",null]}";
            using (System.IO.Stream enc = await EncryptionProcessor.EncryptValueStreamAsync(ToStream(payload), propSetting, shouldEscape: false, cancellationToken: CancellationToken.None))
            {
                JToken encryptedToken = EncryptionProcessor.BaseSerializer.FromStream<JToken>(enc);

                JObject wrapper = new JObject { ["s"] = encryptedToken };
                await EncryptionProcessor.DecryptJTokenAsync(wrapper["s"], propSetting, isEscaped: false, cancellationToken: CancellationToken.None);

                Assert.IsTrue(JToken.DeepEquals(JObject.Parse(payload), wrapper["s"]));
            }
        }

        [TestMethod]
        public async Task DataFormat_EncryptValueStream_ShouldEscapeTrue_With_NonStringLeaf_Throws()
        {
            Mde.AeadAes256CbcHmac256EncryptionAlgorithm algo = Algo();
            EncryptionSettings settings = CreateSettings("s", algo);
            EncryptionSettingForProperty propSetting = settings.GetEncryptionSettingForProperty("s");

            // Object contains non-string leaf (1) and shouldEscape=true should fail
            using (System.IO.MemoryStream valueStream = ToStream("{\"a\":1,\"b\":\"x\"}"))
            {
                ArgumentException ex = await Assert.ThrowsExceptionAsync<ArgumentException>(
                    () => EncryptionProcessor.EncryptValueStreamAsync(valueStream, propSetting, shouldEscape: true, cancellationToken: CancellationToken.None));
                StringAssert.Contains(ex.Message, "value to escape has to be string type");
            }
        }

        [TestMethod]
        public async Task DataFormat_EncryptValueStream_NullArgs_Throw()
        {
            Mde.AeadAes256CbcHmac256EncryptionAlgorithm algo = Algo();
            EncryptionSettings settings = CreateSettings("s", algo);
            EncryptionSettingForProperty propSetting = settings.GetEncryptionSettingForProperty("s");

            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => EncryptionProcessor.EncryptValueStreamAsync(null, propSetting, shouldEscape: false, cancellationToken: CancellationToken.None));

            using (System.IO.MemoryStream valueStream = ToStream("\"x\""))
            {
                await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                    () => EncryptionProcessor.EncryptValueStreamAsync(valueStream, encryptionSettingForProperty: null, shouldEscape: false, cancellationToken: CancellationToken.None));
            }
        }

        #endregion
    }
}
