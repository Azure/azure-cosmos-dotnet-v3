//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Preview07Compatibility
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;
    using CustomDataEncryptionKey = Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKey;

    public static class Preview07CompatibilityProbe
    {
        public static Encryptor CreateEncryptor()
        {
            return new Preview07Encryptor();
        }

        public static CustomDataEncryptionKey CreateDataEncryptionKey()
        {
            return new Preview07DataEncryptionKey();
        }

        public static CosmosDataEncryptionKeyProvider CreateStoreProviderWithDefault()
        {
            return new CosmosDataEncryptionKeyProvider(new Preview07EncryptionKeyStoreProvider());
        }

        public static CosmosDataEncryptionKeyProvider CreateStoreProviderWithNull()
        {
            return new CosmosDataEncryptionKeyProvider(
                new Preview07EncryptionKeyStoreProvider(),
                null);
        }

        public static CosmosDataEncryptionKeyProvider CreateStoreProviderWithTimeSpan()
        {
            return new CosmosDataEncryptionKeyProvider(
                new Preview07EncryptionKeyStoreProvider(),
                TimeSpan.FromMinutes(30));
        }

#pragma warning disable CS0618 // The released obsolete constructors remain binary compatibility requirements.
        public static CosmosDataEncryptionKeyProvider CreateWrapProvider()
        {
            return new CosmosDataEncryptionKeyProvider(new Preview07EncryptionKeyWrapProvider());
        }

        public static CosmosDataEncryptionKeyProvider CreateHybridProvider()
        {
            return new CosmosDataEncryptionKeyProvider(
                new Preview07EncryptionKeyWrapProvider(),
                new Preview07EncryptionKeyStoreProvider());
        }
#pragma warning restore CS0618
    }

    public sealed class Preview07Encryptor : Encryptor
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

        private static byte[] Transform(byte[] input)
        {
            byte[] transformed = (byte[])input.Clone();
            Array.Reverse(transformed);
            return transformed;
        }
    }

    public sealed class Preview07DataEncryptionKey : CustomDataEncryptionKey
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

        private static byte[] Transform(byte[] input)
        {
            byte[] transformed = (byte[])input.Clone();
            Array.Reverse(transformed);
            return transformed;
        }
    }

    internal sealed class Preview07EncryptionKeyStoreProvider : EncryptionKeyStoreProvider
    {
        public override string ProviderName => "preview07-store";

        public override byte[] UnwrapKey(
            string encryptionKeyId,
            KeyEncryptionKeyAlgorithm algorithm,
            byte[] encryptedKey)
        {
            return encryptedKey;
        }

        public override byte[] WrapKey(
            string encryptionKeyId,
            KeyEncryptionKeyAlgorithm algorithm,
            byte[] key)
        {
            return key;
        }

        public override byte[] Sign(string encryptionKeyId, bool allowEnclaveComputations)
        {
            return new byte[] { 1 };
        }

        public override bool Verify(
            string encryptionKeyId,
            bool allowEnclaveComputations,
            byte[] signature)
        {
            return signature?.Length == 1 && signature[0] == 1;
        }
    }

#pragma warning disable CS0618 // The released wrap-provider type remains a binary compatibility requirement.
    internal sealed class Preview07EncryptionKeyWrapProvider : EncryptionKeyWrapProvider
    {
        public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(
            byte[] wrappedKey,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new EncryptionKeyUnwrapResult(wrappedKey, TimeSpan.FromMinutes(30)));
        }

        public override Task<EncryptionKeyWrapResult> WrapKeyAsync(
            byte[] key,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new EncryptionKeyWrapResult(key, metadata));
        }
    }
#pragma warning restore CS0618
}
