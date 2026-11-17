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

        [DataTestMethod]
        [DataRow(false, true)]
        [DataRow(true, true)]
        [DataRow(true, false)]
        public async Task EncryptPatchOperationsAsync_Increment_On_Encrypted_Path_Throws(bool includePlain, bool encryptedFirst)
        {
            EncryptionContainer container = CreateUninitialized<EncryptionContainer>();
            EncryptionSettings settings = CreateEncryptionSettingsWithEncryptedProperty("Sensitive", container);
            PatchOperation encryptedOperation = PatchOperation.Increment("/Sensitive", 1);
            PatchOperation plainOperation = PatchOperation.Increment("/Plain", 2);
            List<PatchOperation> ops = new List<PatchOperation> { encryptedOperation };
            if (includePlain)
            {
                ops.Insert(encryptedFirst ? 1 : 0, plainOperation);
            }
            EncryptionDiagnosticsContext diag = new EncryptionDiagnosticsContext();

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await container.EncryptPatchOperationsAsync(ops, settings, diag, CancellationToken.None));

            StringAssert.Contains(ex.Message, "Increment patch operation is not allowed for encrypted path");
            StringAssert.Contains(ex.Message, "/Sensitive");
            Assert.AreEqual(includePlain ? 2 : 1, ops.Count);
            Assert.AreSame(encryptedOperation, ops[includePlain && !encryptedFirst ? 1 : 0]);
            if (includePlain)
            {
                Assert.AreSame(plainOperation, ops[encryptedFirst ? 1 : 0]);
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task EncryptPatchOperationsAsync_Increment_On_NonEncrypted_Path_Passes_Through(bool includeSecondOperation)
        {
            // Arrange: No encrypted settings for this path
            EncryptionContainer container = CreateUninitialized<EncryptionContainer>();
            EncryptionSettings settings = CreateEncryptionSettingsWithEncryptedProperty("Sensitive", container); // different property than used below

            PatchOperation firstOperation = PatchOperation.Increment("/Plain", 2);
            PatchOperation secondOperation = PatchOperation.Increment("/OtherPlain", 3);
            List<PatchOperation> ops = new List<PatchOperation> { firstOperation };
            if (includeSecondOperation)
            {
                ops.Add(secondOperation);
            }
            EncryptionDiagnosticsContext diag = new EncryptionDiagnosticsContext();

            // Act
            List<PatchOperation> result = await container.EncryptPatchOperationsAsync(ops, settings, diag, CancellationToken.None);

            // Assert: operation should be passed through unchanged
            Assert.AreEqual(includeSecondOperation ? 2 : 1, result.Count);
            Assert.AreSame(firstOperation, result[0]);
            Assert.AreEqual(PatchOperationType.Increment, result[0].OperationType);
            if (includeSecondOperation)
            {
                Assert.AreSame(secondOperation, result[1]);
                Assert.AreEqual(PatchOperationType.Increment, result[1].OperationType);
            }
        }
    }
}
