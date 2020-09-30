//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.AAP_PH.Cryptography;

    /// <summary>
    /// Provides functionality to wrap (encrypt) and unwrap (decrypt) data encryption keys using master keys stored in Key Vault via EncryptionKeyStoreProvider.
    /// Unwrapped data encryption keys will be cached within the client SDK for a period of 1 hour.
    /// </summary>
    internal class AapKeyWrapProvider : EncryptionKeyWrapProvider
    {
        private static TimeSpan rawDekCacheTimeToLive = Constants.DefaultUnwrappedDekCacheTimeToLive;

        public EncryptionKeyStoreProvider EncryptionKeyStoreProvider { get; }

        /// <summary>
        /// Creates a new instance of a provider to wrap (encrypt) and unwrap (decrypt) data encryption keys using master keys stored via EncryptionKeyStoreProvider
        /// </summary>
        /// <param name="encryptionKeyStoreProvider"> EncryptionKeyStoreProvider for Wrap/UnWrap services. </param>
        /// Amount of time the unencrypted form of the data encryption key can be cached on the client before <see cref="UnwrapKeyAsync"/> needs to be called again.
        public AapKeyWrapProvider(EncryptionKeyStoreProvider encryptionKeyStoreProvider)
        {
            this.EncryptionKeyStoreProvider = encryptionKeyStoreProvider;
        }

        public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(
            byte[] wrappedKey,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            KeyEncryptionKey masterKey = new KeyEncryptionKey(
                metadata.Name,
                metadata.Value,
                this.EncryptionKeyStoreProvider);

            byte[] result = masterKey.DecryptEncryptionKey(wrappedKey);
            return Task.FromResult(new EncryptionKeyUnwrapResult(result, AapKeyWrapProvider.rawDekCacheTimeToLive));
        }

        public override Task<EncryptionKeyWrapResult> WrapKeyAsync(
            byte[] key,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            KeyEncryptionKey masterKey = new KeyEncryptionKey(
                metadata.Name,
                metadata.Value,
                this.EncryptionKeyStoreProvider);

            byte[] result = masterKey.EncryptEncryptionKey(key);
            EncryptionKeyWrapMetadata responseMetadata = new EncryptionKeyWrapMetadata(metadata);

            return Task.FromResult(new EncryptionKeyWrapResult(result, responseMetadata));
        }
    }
}
