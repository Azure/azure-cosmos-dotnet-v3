//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ReleasedSubclassCompatibilityTests
    {
        [TestMethod]
        public async Task Preview07StyleEncryptor_RemainsConcreteAndCallable()
        {
            Encryptor encryptor = new Preview07StyleEncryptor();
            byte[] plainText = new byte[] { 1, 2, 3, 4 };

            byte[] cipherText = await encryptor.EncryptAsync(
                plainText,
                "dek",
                "algorithm",
                CancellationToken.None);
            byte[] roundTrip = await encryptor.DecryptAsync(
                cipherText,
                "dek",
                "algorithm",
                CancellationToken.None);

            CollectionAssert.AreEqual(plainText, roundTrip);
        }

        [TestMethod]
        public void Preview07StyleDataEncryptionKey_RemainsConcreteAndCallable()
        {
            DataEncryptionKey key = new Preview07StyleDataEncryptionKey();
            byte[] plainText = new byte[] { 1, 2, 3, 4 };

            byte[] cipherText = key.EncryptData(plainText);
            byte[] roundTrip = key.DecryptData(cipherText);

            CollectionAssert.AreEqual(plainText, roundTrip);
        }

        [TestMethod]
        public void UnpublishedMembers_AreAbsentFromPublicSurface()
        {
            BindingFlags publicInstanceDeclared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            Assert.IsNull(typeof(Encryptor).GetMethod("GetEncryptionKeyAsync", publicInstanceDeclared));
            Assert.IsNull(typeof(CosmosEncryptor).GetMethod("GetEncryptionKeyAsync", publicInstanceDeclared));
            Assert.IsNull(typeof(DataEncryptionKey).GetMethod("GetEncryptByteCount", publicInstanceDeclared));
            Assert.IsNull(typeof(DataEncryptionKey).GetMethod("GetDecryptByteCount", publicInstanceDeclared));
            Assert.IsNull(
                typeof(DataEncryptionKey).GetMethod(
                    "EncryptData",
                    publicInstanceDeclared,
                    binder: null,
                    types: new[] { typeof(byte[]), typeof(int), typeof(int), typeof(byte[]), typeof(int) },
                    modifiers: null));
            Assert.IsNull(
                typeof(DataEncryptionKey).GetMethod(
                    "DecryptData",
                    publicInstanceDeclared,
                    binder: null,
                    types: new[] { typeof(byte[]), typeof(int), typeof(int), typeof(byte[]), typeof(int) },
                    modifiers: null));
        }

        private sealed class Preview07StyleEncryptor : Encryptor
        {
            public override Task<byte[]> EncryptAsync(
                byte[] plainText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Transform(plainText));
            }

            public override Task<byte[]> DecryptAsync(
                byte[] cipherText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Transform(cipherText));
            }
        }

        private sealed class Preview07StyleDataEncryptionKey : DataEncryptionKey
        {
            public override byte[] RawKey => null;

            public override string EncryptionAlgorithm => "preview07-compatible";

            public override byte[] EncryptData(byte[] plainText)
            {
                return Transform(plainText);
            }

            public override byte[] DecryptData(byte[] cipherText)
            {
                return Transform(cipherText);
            }
        }

        private static byte[] Transform(byte[] input)
        {
            byte[] transformed = (byte[])input.Clone();
            Array.Reverse(transformed);
            return transformed;
        }
    }
}
