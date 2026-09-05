//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CompatMatrix
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Data.Encryption.Cryptography;

    internal sealed class MatrixKeyStoreProvider : EncryptionKeyStoreProvider
    {
        public override string ProviderName => "compat-matrix-store";

        public override byte[] UnwrapKey(
            string encryptionKeyId,
            KeyEncryptionKeyAlgorithm algorithm,
            byte[] encryptedKey)
        {
            int shift = GetShift(encryptionKeyId);
            return encryptedKey.Select(value => unchecked((byte)(value - shift))).ToArray();
        }

        public override byte[] WrapKey(
            string encryptionKeyId,
            KeyEncryptionKeyAlgorithm algorithm,
            byte[] key)
        {
            int shift = GetShift(encryptionKeyId);
            return key.Select(value => unchecked((byte)(value + shift))).ToArray();
        }

        public override byte[] Sign(string encryptionKeyId, bool allowEnclaveComputations)
        {
            return new[] { (byte)GetShift(encryptionKeyId) };
        }

        public override bool Verify(string encryptionKeyId, bool allowEnclaveComputations, byte[] signature)
        {
            return signature?.Length == 1 && signature[0] == GetShift(encryptionKeyId);
        }

        private static int GetShift(string value)
        {
            return (value?.Sum(character => (int)character) ?? 0) % 31 + 1;
        }
    }

    internal sealed class MatrixKeyWrapProvider : EncryptionKeyWrapProvider
    {
        public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(
            byte[] wrappedKey,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            int shift = GetShift(metadata?.Value);
            byte[] key = wrappedKey.Select(value => unchecked((byte)(value - shift))).ToArray();
            return Task.FromResult(new EncryptionKeyUnwrapResult(key, TimeSpan.FromMinutes(5)));
        }

        public override Task<EncryptionKeyWrapResult> WrapKeyAsync(
            byte[] key,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            int shift = GetShift(metadata?.Value);
            byte[] wrappedKey = key.Select(value => unchecked((byte)(value + shift))).ToArray();
            return Task.FromResult(new EncryptionKeyWrapResult(wrappedKey, metadata));
        }

        private static int GetShift(string value)
        {
            return (value?.Sum(character => (int)character) ?? 0) % 31 + 1;
        }
    }

    internal sealed class MatrixEncryptor : Encryptor
    {
        private readonly CosmosEncryptor inner;

        public MatrixEncryptor(DataEncryptionKeyProvider provider)
        {
            this.inner = new CosmosEncryptor(provider);
        }

        public override Task<byte[]> DecryptAsync(
            byte[] cipherText,
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            CancellationToken cancellationToken = default)
        {
            return this.inner.DecryptAsync(cipherText, dataEncryptionKeyId, encryptionAlgorithm, cancellationToken);
        }

        public override Task<byte[]> EncryptAsync(
            byte[] plainText,
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            CancellationToken cancellationToken = default)
        {
            return this.inner.EncryptAsync(plainText, dataEncryptionKeyId, encryptionAlgorithm, cancellationToken);
        }

#if COMPAT_CURRENT
        public override Task<Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKey> GetEncryptionKeyAsync(
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            CancellationToken cancellationToken = default)
        {
            return this.inner.GetEncryptionKeyAsync(dataEncryptionKeyId, encryptionAlgorithm, cancellationToken);
        }
#endif
    }
}
