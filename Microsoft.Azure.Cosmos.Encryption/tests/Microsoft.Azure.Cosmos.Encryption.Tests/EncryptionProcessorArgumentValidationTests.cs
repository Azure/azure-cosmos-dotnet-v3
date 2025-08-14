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

    [TestClass]
    public class EncryptionProcessorArgumentValidationTests
    {
        private static MemoryStream ToStream(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);

        private static EncryptionSettings CreateSettingsForId()
        {
            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            // Use an uninitialized container; it won't be used in the failure paths these tests exercise.
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            var forProperty = new EncryptionSettingForProperty(
                clientEncryptionKeyId: "cek1",
                encryptionType: Microsoft.Data.Encryption.Cryptography.EncryptionType.Deterministic,
                encryptionContainer: container,
                databaseRid: "dbRid");
            settings.SetEncryptionSettingForProperty("id", forProperty);
            return settings;
        }

        [TestMethod]
        public async Task EncryptAsync_NullInput_ThrowsArgumentNullException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
            {
                await EncryptionProcessor.EncryptAsync(input: null, encryptionSettings: CreateSettingsForId(), operationDiagnostics: null, cancellationToken: CancellationToken.None);
            }, "input");
        }

        [TestMethod]
        public async Task EncryptAsync_NullSettings_ThrowsOrFailsPredictably()
        {
            using var input = ToStream("{\"id\":\"1\"}");
            try
            {
                await EncryptionProcessor.EncryptAsync(input: input, encryptionSettings: null, operationDiagnostics: null, cancellationToken: CancellationToken.None);
                Assert.Fail("Expected an exception when encryptionSettings is null.");
            }
            catch (NullReferenceException)
            {
                // Current implementation: NRE when accessing PropertiesToEncrypt; acceptable documented behavior for now.
            }
            catch (ArgumentNullException)
            {
                // Future improvement may throw ANE; accept either to avoid test fragility.
            }
        }

        [TestMethod]
        public async Task EncryptAsync_IdNonStringWithShouldEscape_ThrowsArgumentException()
        {
            // Arrange: id is an integer, settings configured to encrypt 'id' which triggers shouldEscape
            var settings = CreateSettingsForId();
            using var input = ToStream("{\"id\": 42, \"p\": 1}");

            // Act & Assert
            var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            {
                await EncryptionProcessor.EncryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            });
            StringAssert.Contains(ex.Message, "value to escape has to be string type");
        }
    }
}
