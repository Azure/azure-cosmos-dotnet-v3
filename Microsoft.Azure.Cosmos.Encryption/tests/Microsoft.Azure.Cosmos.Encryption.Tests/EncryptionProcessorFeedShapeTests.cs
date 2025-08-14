//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Encryption.Tests.TestHelpers;

    [TestClass]
    public class EncryptionProcessorFeedShapeTests
    {
    // Internals are visible to this test assembly; call the internal method directly.

        [TestMethod]
        public async Task DeserializeAndDecryptResponseAsync_MissingDocuments_Throws()
        {
            // { "NotDocuments": [] }
            string json = "{ \"NotDocuments\": [] }";
            using Stream content = new MemoryStream(Encoding.UTF8.GetBytes(json));

            // Fabricate an EncryptionSettings instance with non-empty PropertiesToEncrypt via uninitialized object hack.
            var settings = (EncryptionSettings)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionSettings));
            // Set PropertiesToEncrypt backing field to any non-empty enumerable to bypass early return.
            var backingField = typeof(EncryptionSettings).GetField("<PropertiesToEncrypt>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
            backingField.SetValue(settings, new[] { "id" });

            try
            {
                await EncryptionProcessor.DeserializeAndDecryptResponseAsync(content, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
                Assert.Fail("Expected InvalidOperationException for missing Documents array");
            }
            catch (InvalidOperationException ioe)
            {
                StringAssert.Contains(ioe.Message, "Feed Response body contract was violated");
            }
        }

        [TestMethod]
        public async Task DeserializeAndDecryptResponseAsync_DocumentsNotArray_Throws()
        {
            // { "Documents": {} }
            string json = "{ \"Documents\": {} }";
            using Stream content = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var settings = (EncryptionSettings)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionSettings));
            var backingField = typeof(EncryptionSettings).GetField("<PropertiesToEncrypt>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
            backingField.SetValue(settings, new[] { "id" });

            try
            {
                await EncryptionProcessor.DeserializeAndDecryptResponseAsync(content, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
                Assert.Fail("Expected InvalidOperationException for Documents not being an array");
            }
            catch (InvalidOperationException ioe)
            {
                StringAssert.Contains(ioe.Message, "Feed Response body contract was violated");
            }
        }

        [TestMethod]
        public async Task DeserializeAndDecryptResponseAsync_DocumentsNull_Throws()
        {
            string json = "{ \"Documents\": null }";
            using Stream content = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var settings = (EncryptionSettings)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionSettings));
            var backingField = typeof(EncryptionSettings).GetField("<PropertiesToEncrypt>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
            backingField.SetValue(settings, new[] { "id" });

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await EncryptionProcessor.DeserializeAndDecryptResponseAsync(content, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            });
        }

        [TestMethod]
        public async Task DeserializeAndDecryptResponseAsync_DeeplyNestedDocuments_TraversesAndDecrypts()
        {
            // Build deep nested document under 'Secret' to exercise traversal in feed path.
            JObject deep = JObject.Parse("{ \"v\": \"x\" }");
            for (int i = 0; i < 40; i++) { deep = new JObject { ["o"] = deep }; }
            JObject original = new JObject { ["id"] = "deep-feed", ["Secret"] = deep };

            var algo = TestCryptoHelpers.CreateAlgorithm(Microsoft.Data.Encryption.Cryptography.EncryptionType.Deterministic);
            var settings = TestCryptoHelpers.CreateSettingsWithInjected("Secret", Microsoft.Data.Encryption.Cryptography.EncryptionType.Deterministic, algo);

            // Encrypt single doc and wrap in feed shape
            using Stream s = EncryptionProcessor.BaseSerializer.ToStream(original);
            using Stream es = await EncryptionProcessor.EncryptAsync(s, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject encDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(es);
            JObject feed = new JObject { [Constants.DocumentsResourcePropertyName] = new JArray(encDoc) };

            using Stream feedStream = EncryptionProcessor.BaseSerializer.ToStream(feed);
            using Stream outStream = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(feedStream, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject outFeed = EncryptionProcessor.BaseSerializer.FromStream<JObject>(outStream);
            JArray docs = (JArray)outFeed[Constants.DocumentsResourcePropertyName]!;
            Assert.AreEqual(1, docs.Count);
            Assert.IsTrue(JToken.DeepEquals(original, (JObject)docs[0]));
        }
    }
}
