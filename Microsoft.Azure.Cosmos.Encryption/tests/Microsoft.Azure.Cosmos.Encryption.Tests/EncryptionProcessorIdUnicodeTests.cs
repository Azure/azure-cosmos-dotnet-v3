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
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Mde = Microsoft.Data.Encryption.Cryptography;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Encryption.Tests.TestHelpers;

    [TestClass]
    public class EncryptionProcessorIdUnicodeTests
    {
        private static EncryptionSettings CreateSettings(Mde.AeadAes256CbcHmac256EncryptionAlgorithm algo)
        {
            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            settings.SetEncryptionSettingForProperty("id", new EncryptionSettingForProperty("cek1", Mde.EncryptionType.Deterministic, container, "dbRid", algo));
            return settings;
        }

    private static Mde.AeadAes256CbcHmac256EncryptionAlgorithm Algo() => TestCryptoHelpers.CreateAlgorithm(Mde.EncryptionType.Deterministic);

        [TestMethod]
        public async Task EncryptDecrypt_Id_With_Unicode_And_ProblemChars_RoundTrips()
        {
            string id = "id/漢字+emoji😀?hash#back\\slash";
            JObject doc = new JObject { ["id"] = id, ["p"] = 1 };
            var settings = CreateSettings(Algo());

            using Stream enc = await EncryptionProcessor.EncryptAsync(EncryptionProcessor.BaseSerializer.ToStream(doc), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject encDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(enc);
            string encId = encDoc.Value<string>("id");
            Assert.IsFalse(encId.Contains('/'));
            Assert.IsFalse(encId.Contains('+'));
            Assert.IsFalse(encId.Contains('?'));
            Assert.IsFalse(encId.Contains('#'));
            Assert.IsFalse(encId.Contains('\\'));

            using Stream dec = await EncryptionProcessor.DecryptAsync(EncryptionProcessor.BaseSerializer.ToStream(encDoc), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject round = EncryptionProcessor.BaseSerializer.FromStream<JObject>(dec);
            Assert.AreEqual(id, round.Value<string>("id"));
        }

        [TestMethod]
        public async Task EncryptDecrypt_Id_Large_MixedUnicode_RoundTrips()
        {
            // Build a large, mixed-unicode id string
            var sb = new StringBuilder();
            string chunk = "😀漢字🌍🔥/+#?\\";
            for (int i = 0; i < 500; i++) sb.Append(chunk); // length ~3500 chars
            string id = sb.ToString();

            JObject doc = new JObject { ["id"] = id, ["p"] = 1 };
            var settings = CreateSettings(Algo());

            using Stream enc = await EncryptionProcessor.EncryptAsync(EncryptionProcessor.BaseSerializer.ToStream(doc), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject encDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(enc);
            string encId = encDoc.Value<string>("id");
            Assert.IsFalse(encId.Contains('/'));
            Assert.IsFalse(encId.Contains('+'));
            Assert.IsFalse(encId.Contains('?'));
            Assert.IsFalse(encId.Contains('#'));
            Assert.IsFalse(encId.Contains('\\'));

            using Stream dec = await EncryptionProcessor.DecryptAsync(EncryptionProcessor.BaseSerializer.ToStream(encDoc), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject round = EncryptionProcessor.BaseSerializer.FromStream<JObject>(dec);
            Assert.AreEqual(id, round.Value<string>("id"));
        }

    // Centralized helpers used instead
    }
}
