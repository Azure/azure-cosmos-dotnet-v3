//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EncryptionContainerPatchTests
    {
        private static T CreateUninitialized<T>() where T : class
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }

        private static EncryptionSettings CreateEncryptionSettingsWithEncryptedProperty(string propertyName, EncryptionContainer container)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));

            EncryptionSettings settings = new EncryptionSettings("rid", new List<string> { "/id" });
            EncryptionSettingForProperty forProperty = new EncryptionSettingForProperty(
                clientEncryptionKeyId: "cek1",
                encryptionType: Microsoft.Data.Encryption.Cryptography.EncryptionType.Randomized,
                encryptionContainer: container,
                databaseRid: "dbRid");

            settings.SetEncryptionSettingForProperty(propertyName, forProperty);
            return settings;
        }

        [TestMethod]
        public async Task EncryptPatchOperationsAsync_Increment_On_Encrypted_Path_Throws()
        {
            // Arrange
            EncryptionContainer container = CreateUninitialized<EncryptionContainer>();
            EncryptionSettings settings = CreateEncryptionSettingsWithEncryptedProperty("Sensitive", container);
            List<PatchOperation> ops = new List<PatchOperation>
            {
                PatchOperation.Increment("/Sensitive", 1)
            };

            EncryptionDiagnosticsContext diag = new EncryptionDiagnosticsContext();

            // Act + Assert
            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await container.EncryptPatchOperationsAsync(ops, settings, diag, CancellationToken.None));

            StringAssert.Contains(ex.Message, "Increment patch operation is not allowed for encrypted path");
            StringAssert.Contains(ex.Message, "/Sensitive");
        }

        [TestMethod]
        public async Task EncryptPatchOperationsAsync_Increment_On_NonEncrypted_Path_Passes_Through()
        {
            // Arrange: No encrypted settings for this path
            EncryptionContainer container = CreateUninitialized<EncryptionContainer>();
            EncryptionSettings settings = CreateEncryptionSettingsWithEncryptedProperty("Other", container); // different property than used below

            PatchOperation op = PatchOperation.Increment("/Plain", 2);
            List<PatchOperation> ops = new List<PatchOperation> { op };
            EncryptionDiagnosticsContext diag = new EncryptionDiagnosticsContext();

            // Act
            List<PatchOperation> result = await container.EncryptPatchOperationsAsync(ops, settings, diag, CancellationToken.None);

            // Assert: operation should be passed through unchanged
            Assert.AreEqual(1, result.Count);
            Assert.AreSame(op, result[0]);
            Assert.AreEqual(PatchOperationType.Increment, result[0].OperationType);
        }
    }
}
