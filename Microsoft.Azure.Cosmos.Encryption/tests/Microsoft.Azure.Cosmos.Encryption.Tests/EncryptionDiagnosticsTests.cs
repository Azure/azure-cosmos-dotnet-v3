//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionDiagnosticsTests
    {
        private static T CreateUninitialized<T>() where T : class
            => (T)FormatterServices.GetUninitializedObject(typeof(T));

        private static EncryptionSettings CreateEncryptionSettingsWithEncryptedProperty(string propertyName, EncryptionContainer container)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));

            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            var forProperty = new EncryptionSettingForProperty(
                clientEncryptionKeyId: "cek1",
                encryptionType: Microsoft.Data.Encryption.Cryptography.EncryptionType.Randomized,
                encryptionContainer: container,
                databaseRid: "dbRid");
            settings.SetEncryptionSettingForProperty(propertyName, forProperty);
            return settings;
        }

        private static MemoryStream ToStream(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

        [TestMethod]
        public async Task Diagnostics_Encrypt_EndCalled_With_Expected_Count()
        {
            // Arrange: property exists and is null, so algorithm is not invoked but count still increments
            var json = "{\"id\":\"1\",\"Sensitive\":null}";
            using var input = ToStream(json);

            var container = CreateUninitialized<EncryptionContainer>();
            var settings = CreateEncryptionSettingsWithEncryptedProperty("Sensitive", container);
            var diag = new EncryptionDiagnosticsContext();

            // Act
            using Stream result = await EncryptionProcessor.EncryptAsync(input, settings, diag, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);

            // Verify diagnostics captured properties count
            JToken countToken = diag.EncryptContent[Constants.DiagnosticsPropertiesEncryptedCount];
            Assert.IsNotNull(countToken, "Encrypt diagnostics should include properties encrypted count.");
            Assert.AreEqual(1, countToken.Value<int>());

            Assert.IsNotNull(diag.EncryptContent[Constants.DiagnosticsStartTime]);
            Assert.IsNotNull(diag.EncryptContent[Constants.DiagnosticsDuration]);
        }

        [TestMethod]
        public async Task Diagnostics_Decrypt_EndCalled_With_Expected_Count()
        {
            // Arrange: property exists and is null; decrypt path still visits and counts it
            var json = "{\"id\":\"1\",\"Sensitive\":null}";
            using var input = ToStream(json); // MemoryStream is seekable (DEBUG assert satisfied)

            var container = CreateUninitialized<EncryptionContainer>();
            var settings = CreateEncryptionSettingsWithEncryptedProperty("Sensitive", container);
            var diag = new EncryptionDiagnosticsContext();

            // Act
            using Stream result = await EncryptionProcessor.DecryptAsync(input, settings, diag, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);

            JToken countToken = diag.DecryptContent[Constants.DiagnosticsPropertiesDecryptedCount];
            Assert.IsNotNull(countToken, "Decrypt diagnostics should include properties decrypted count.");
            Assert.AreEqual(1, countToken.Value<int>());

            Assert.IsNotNull(diag.DecryptContent[Constants.DiagnosticsStartTime]);
            Assert.IsNotNull(diag.DecryptContent[Constants.DiagnosticsDuration]);
        }
    }
}
