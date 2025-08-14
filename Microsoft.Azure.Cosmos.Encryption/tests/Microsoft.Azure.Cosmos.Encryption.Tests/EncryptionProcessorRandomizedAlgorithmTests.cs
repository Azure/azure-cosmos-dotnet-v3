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
    using Microsoft.Azure.Cosmos.Encryption.Tests.TestHelpers;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionProcessorRandomizedAlgorithmTests
    {
    private static Mde.AeadAes256CbcHmac256EncryptionAlgorithm CreateRandomizedAlgorithm() => TestCryptoHelpers.CreateAlgorithm(Mde.EncryptionType.Randomized);

        private static EncryptionSettings CreateSettings(string propertyName, Mde.AeadAes256CbcHmac256EncryptionAlgorithm algo)
        {
            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            settings.SetEncryptionSettingForProperty(propertyName, new EncryptionSettingForProperty("cek1", Mde.EncryptionType.Randomized, container, "dbRid", algo));
            return settings;
        }

        [TestMethod]
        public async Task Randomized_Encrypt_Twice_DifferentCipher_SameDecrypt()
        {
            var algo = CreateRandomizedAlgorithm();
            var settings = CreateSettings("Secret", algo);

            JObject doc = new JObject { ["id"] = "1", ["Secret"] = new JObject { ["a"] = 1, ["b"] = "x" } };

            using Stream s1 = EncryptionProcessor.BaseSerializer.ToStream(doc);
            using Stream e1 = await EncryptionProcessor.EncryptAsync(s1, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject enc1 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(e1);

            using Stream s2 = EncryptionProcessor.BaseSerializer.ToStream(doc);
            using Stream e2 = await EncryptionProcessor.EncryptAsync(s2, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject enc2 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(e2);

            // Ciphertexts under randomized encryption should differ
            Assert.IsFalse(JToken.DeepEquals(enc1["Secret"], enc2["Secret"]));

            // Both decrypt back to the original
            using Stream d1 = await EncryptionProcessor.DecryptAsync(EncryptionProcessor.BaseSerializer.ToStream(enc1), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            using Stream d2 = await EncryptionProcessor.DecryptAsync(EncryptionProcessor.BaseSerializer.ToStream(enc2), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject r1 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(d1);
            JObject r2 = EncryptionProcessor.BaseSerializer.FromStream<JObject>(d2);
            Assert.IsTrue(JToken.DeepEquals(doc, r1));
            Assert.IsTrue(JToken.DeepEquals(doc, r2));
        }

    // no helpers needed here; common helper centralizes KEK/algorithm
    }
}
