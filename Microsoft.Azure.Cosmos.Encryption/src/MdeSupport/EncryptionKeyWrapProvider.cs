//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// Base class for all key store providers. A custom provider must derive from this
    /// class and override its member functions.
    /// </summary>
    public abstract class EncryptionKeyWrapProvider
    {
        internal EncryptionKeyWrapProvider()
        {
            this.EncryptionKeyStoreProviderImpl = new EncryptionKeyStoreProviderImpl(this);
        }

        /// <summary>
        /// Gets or sets the lifespan of the decrypted data encryption key in the cache.
        /// Once the timespan has elapsed, the decrypted data encryption key is discarded
        /// and must be revalidated.
        /// </summary>
        /// <remarks>
        /// Internally, there is a cache of key encryption keys (once they are unwrapped).
        /// This is useful for rapidly decrypting multiple data values. The default value is 2 hours.
        /// Setting the <see cref="DataEncryptionKeyCacheTimeToLive"/> to zero disables caching.
        /// </remarks>
        public TimeSpan? DataEncryptionKeyCacheTimeToLive
        {
            get => this.EncryptionKeyStoreProviderImpl.DataEncryptionKeyCacheTimeToLive;
            set
            {
                this.EncryptionKeyStoreProviderImpl.DataEncryptionKeyCacheTimeToLive = value;

                // set the TTL for ProtectedDataEncryption, so that we have a uniform expiry of the KeyStoreProvider and ProtectedDataEncryption cache items.
                if (this.EncryptionKeyStoreProviderImpl.DataEncryptionKeyCacheTimeToLive.HasValue)
                {
                    if (EncryptionCosmosClient.EncryptionKeyCacheSemaphore.Wait(-1))
                    {
                        try
                        {
                            // pick the min of the new value being set and ProtectedDataEncryptionKey's current TTL. Note ProtectedDataEncryptionKey TimeToLive is static
                            // and results in various instances to share this value. Hence we pick up whatever is the min value. If a TimeSpan.Zero is across any one instance
                            // it should be fine, since we look up the KeyStoreProvider cache. ProtectedDataEncryptionKey's own cache supersedes KeyStoreProvider cache, since it stores
                            // the RootKey which is derived from unwrapped key(Data Encryption Key).
                            // Note: DataEncryptionKeyCacheTimeToLive is nullable. When set to null this results in AbsoluteExpirationRelativeToNow to be set to null which caches forever.
                            // whatever is the current set value for ProtectedDataEncryptionKey TimeToLive(is not nullable) would be min if null value is passed.
                            if (TimeSpan.Compare(this.EncryptionKeyStoreProviderImpl.DataEncryptionKeyCacheTimeToLive.Value, ProtectedDataEncryptionKey.TimeToLive) < 0)
                            {
                                ProtectedDataEncryptionKey.TimeToLive = this.EncryptionKeyStoreProviderImpl.DataEncryptionKeyCacheTimeToLive.Value;
                            }
                        }
                        finally
                        {
                            EncryptionCosmosClient.EncryptionKeyCacheSemaphore.Release(1);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the unique name that identifies a particular implementation of the abstract <see cref="EncryptionKeyWrapProvider"/>.
        /// </summary>
        public abstract string ProviderName { get; }

        internal EncryptionKeyStoreProviderImpl EncryptionKeyStoreProviderImpl { get; }

        /// <summary>
        /// Unwraps the specified <paramref name="encryptedKey"/> of a data encryption key. The encrypted value is expected to be encrypted using
        /// the key encryption key with the specified <paramref name="encryptionKeyId"/> and using the specified <paramref name="keyEncryptionKeyAlgorithm"/>.
        /// </summary>
        /// <param name="encryptionKeyId">The key Id tells the provider where to find the key.</param>
        /// <param name="keyEncryptionKeyAlgorithm">The key encryption algorithm.</param>
        /// <param name="encryptedKey">The ciphertext key.</param>
        /// <returns>The unwrapped data encryption key.</returns>
        public abstract Task<byte[]> UnwrapKeyAsync(string encryptionKeyId, string keyEncryptionKeyAlgorithm, byte[] encryptedKey);

        /// <summary>
        /// Wraps a data encryption key using the key encryption key with the specified <paramref name="encryptionKeyId"/> and using the specified <paramref name="keyEncryptionKeyAlgorithm"/>.
        /// </summary>
        /// <param name="encryptionKeyId">The key Id tells the provider where to find the key.</param>
        /// <param name="keyEncryptionKeyAlgorithm">The key encryption algorithm.</param>
        /// <param name="key">The plaintext key.</param>
        /// <returns>The wrapped data encryption key.</returns>
        public abstract Task<byte[]> WrapKeyAsync(string encryptionKeyId, string keyEncryptionKeyAlgorithm, byte[] key);
    }
}
