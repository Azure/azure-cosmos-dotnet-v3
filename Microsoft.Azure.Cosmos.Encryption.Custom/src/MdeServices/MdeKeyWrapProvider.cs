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

        public override async Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(
            byte[] wrappedKey,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            ArgumentValidation.ThrowIfNull(metadata);

            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                metadata.Name,
                metadata.Value,
                this.EncryptionKeyStoreProvider);

            byte[] result = await keyEncryptionKey.DecryptEncryptionKeyAsync(wrappedKey, cancellationToken).ConfigureAwait(false);

            return new EncryptionKeyUnwrapResult(result, TimeSpan.Zero);
        }

        public override async Task<EncryptionKeyWrapResult> WrapKeyAsync(
            byte[] key,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            ArgumentValidation.ThrowIfNull(metadata);

            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                metadata.Name,
                metadata.Value,
                this.EncryptionKeyStoreProvider);

            byte[] result = await keyEncryptionKey.EncryptEncryptionKeyAsync(key, cancellationToken).ConfigureAwait(false);

            return new EncryptionKeyWrapResult(result, metadata);
        }
    }
}
