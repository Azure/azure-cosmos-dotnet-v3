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
    public class EncryptionProcessorDiagnosticsEdgeTests
    {
        private static MemoryStream ToStream(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);

    private static EncryptionSettings CreateSettings(string propertyName, Mde.AeadAes256CbcHmac256EncryptionAlgorithm algo) => TestCryptoHelpers.CreateSettingsWithInjected(propertyName, Mde.EncryptionType.Deterministic, algo);
    private static Mde.AeadAes256CbcHmac256EncryptionAlgorithm Algo() => TestCryptoHelpers.CreateAlgorithm(Mde.EncryptionType.Deterministic);

        [TestMethod]
        public async Task Encrypt_PropConfiguredButMissing_CountZero()
        {
            var settings = CreateSettings("Secret", Algo());
            JObject doc = new JObject { ["id"] = "1", ["x"] = 1 };
            var diag = new EncryptionDiagnosticsContext();
            using Stream enc = await EncryptionProcessor.EncryptAsync(EncryptionProcessor.BaseSerializer.ToStream(doc), settings, diag, CancellationToken.None);
            Assert.AreEqual(0, diag.EncryptContent[Constants.DiagnosticsPropertiesEncryptedCount].Value<int>());
        }

        [TestMethod]
        public async Task Decrypt_PropPresentNull_CountOne()
        {
            var settings = CreateSettings("Secret", Algo());
            // Build a doc with Secret: null (encryption path should leave it null, decrypt should count property)
            JObject doc = new JObject { ["id"] = "1", ["Secret"] = null };

            // Encrypt
            using Stream enc = await EncryptionProcessor.EncryptAsync(EncryptionProcessor.BaseSerializer.ToStream(doc), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject encDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(enc);

            // Decrypt and assert count == 1 (property present)
            var decDiag = new EncryptionDiagnosticsContext();
            using Stream dec = await EncryptionProcessor.DecryptAsync(EncryptionProcessor.BaseSerializer.ToStream(encDoc), settings, decDiag, CancellationToken.None);
            Assert.AreEqual(1, decDiag.DecryptContent[Constants.DiagnosticsPropertiesDecryptedCount].Value<int>());
        }

    // Centralized helpers used instead
    }
}
