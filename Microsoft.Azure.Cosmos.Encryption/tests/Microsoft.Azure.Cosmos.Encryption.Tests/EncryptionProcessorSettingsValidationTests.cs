//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EncryptionProcessorSettingsValidationTests
    {
        private static MemoryStream ToStream(string json)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);
        }

        private static EncryptionSettings CreateSettingsWithMissingMapping(params string[] properties)
        {
            // Create settings and only declare PropertiesToEncrypt via real mappings, then remove them
            // to simulate missing mapping when traversing documents.
            var settings = new EncryptionSettings("rid", new System.Collections.Generic.List<string> { "/id" });
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

            // Now clear the mapping dictionary via reflection to simulate Keys present but value missing.
            var dictField = typeof(EncryptionSettings).GetField("encryptionSettingsDictByPropertyName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var dict = (System.Collections.Generic.Dictionary<string, EncryptionSettingForProperty>)dictField.GetValue(settings);
            foreach (string p in properties)
            {
                dict[p] = null;
            }

            return settings;
        }

        [TestMethod]
        public async Task EncryptAsync_PropertyWithoutSetting_Throws_And_DoesNotDisposeInput()
        {
            // Arrange: The item contains property 'foo', settings list 'foo' for encryption, but no mapping is configured.
            using var input = ToStream("{\"id\":\"1\",\"foo\":123}");
            EncryptionSettings settings = CreateSettingsWithMissingMapping("foo");

            // Act
            try
            {
                await EncryptionProcessor.EncryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
                Assert.Fail("Expected ArgumentException due to missing EncryptionSettingForProperty mapping.");
            }
            catch (ArgumentException ex)
            {
                StringAssert.Contains(ex.Message, "Invalid Encryption Setting for the Property:foo");
            }

            // Assert: The input was fully consumed. Some serializers may dispose the stream during read.
            try
            {
                Assert.AreEqual(input.Length, input.Position, "Input stream position should be at end after failure.");
            }
            catch (ObjectDisposedException)
            {
                // Acceptable: FromStream may dispose the input stream after reading.
            }
        }

        [TestMethod]
        public async Task DecryptAsync_PropertyWithoutSetting_Throws_And_DoesNotDisposeInput()
        {
            // Arrange: The document contains property 'bar', settings list 'bar' for encryption, but no mapping is configured.
            using var input = ToStream("{\"id\":\"1\",\"bar\":\"someValue\"}");
            EncryptionSettings settings = CreateSettingsWithMissingMapping("bar");

            // Act
            try
            {
                await EncryptionProcessor.DecryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
                Assert.Fail("Expected ArgumentException due to missing EncryptionSettingForProperty mapping.");
            }
            catch (ArgumentException ex)
            {
                StringAssert.Contains(ex.Message, "Invalid Encryption Setting for Property:bar");
            }

            // Assert: Input should NOT be disposed, and since it was fully read, position should be at end.
            Assert.IsTrue(input.CanRead, "Input stream should not be disposed on failure.");
            Assert.AreEqual(input.Length, input.Position, "Input stream position should be at end after failure.");
        }
    }
}
