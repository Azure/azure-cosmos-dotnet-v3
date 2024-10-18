//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// Provides functionality to wrap (encrypt) and unwrap (decrypt) data encryption keys using Key Encryption Keys (KEKs) via EncryptionKeyStoreProvider.
    /// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
    internal sealed class MdeKeyWrapProvider : EncryptionKeyWrapProvider
#pragma warning restore CS0618 // Type or member is obsolete
    {
        public EncryptionKeyStoreProvider EncryptionKeyStoreProvider { get; }

        public MdeKeyWrapProvider(EncryptionKeyStoreProvider encryptionKeyStoreProvider)
        {
            this.EncryptionKeyStoreProvider = encryptionKeyStoreProvider ?? throw new ArgumentNullException(nameof(encryptionKeyStoreProvider));
        }

        public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(
            byte[] wrappedKey,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(metadata);
#else
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }
#endif

            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                metadata.Name,
                metadata.Value,
                this.EncryptionKeyStoreProvider);

            byte[] result = keyEncryptionKey.DecryptEncryptionKey(wrappedKey);
            return Task.FromResult(new EncryptionKeyUnwrapResult(result, TimeSpan.Zero));
        }

        public override Task<EncryptionKeyWrapResult> WrapKeyAsync(
            byte[] key,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(metadata);
#else
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }
#endif

            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                metadata.Name,
                metadata.Value,
                this.EncryptionKeyStoreProvider);

            byte[] result = keyEncryptionKey.EncryptEncryptionKey(key);
            return Task.FromResult(new EncryptionKeyWrapResult(result, metadata));
        }
    }
}
