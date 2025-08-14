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
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionProcessorDecryptNoOpTests
    {
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
                    encryptionType: Microsoft.Data.Encryption.Cryptography.EncryptionType.Randomized,
                    encryptionContainer: container,
                    databaseRid: "dbRid");
                settings.SetEncryptionSettingForProperty(p, forProperty);
            }

            return settings;
        }

        [TestMethod]
        public async Task Decrypt_JObject_NoPropertiesConfigured_ReturnsSameAndZeroCount()
        {
            JObject doc = JObject.Parse("{ \"id\": \"1\", \"name\": \"n\" }");
            EncryptionSettings settings = CreateSettingsEmpty();

            (JObject result, int count) = await EncryptionProcessor.DecryptAsync(doc, settings, CancellationToken.None);

            Assert.AreSame(doc, result);
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public async Task Decrypt_JObject_ConfiguredButMissingInDoc_ReturnsZeroCount()
        {
            JObject doc = JObject.Parse("{ \"id\": \"1\", \"name\": \"n\" }");
            EncryptionSettings settings = CreateSettingsWithNullMapping("sensitive");

            (JObject result, int count) = await EncryptionProcessor.DecryptAsync(doc, settings, CancellationToken.None);

            Assert.AreSame(doc, result);
            Assert.AreEqual(0, count);
        }
    }
}
