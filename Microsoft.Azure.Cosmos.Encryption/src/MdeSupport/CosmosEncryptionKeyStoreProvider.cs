//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Implementation of key encryption key store provider that allows client applications to access data when a
    /// key encryption key is stored in Microsoft Azure Key Vault.
    /// </summary>
    public abstract class CosmosEncryptionKeyStoreProvider
    {
        internal CosmosEncryptionKeyStoreProvider()
        {
            this.CosmosEncryptionKeyStoreProviderCore = new CosmosEncryptionKeyStoreProviderCore(this);
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
        public virtual TimeSpan? DataEncryptionKeyCacheTimeToLive
        {
            get => this.CosmosEncryptionKeyStoreProviderCore.DataEncryptionKeyCacheTimeToLive.Value;
            set => this.CosmosEncryptionKeyStoreProviderCore.DataEncryptionKeyCacheTimeToLive = value;
        }

        /// <summary>
        /// Gets the unique name that identifies a particular implementation of the abstract <see cref="CosmosEncryptionKeyStoreProvider"/>.
        /// </summary>
        public abstract string ProviderName { get; }

        internal CosmosEncryptionKeyStoreProviderCore CosmosEncryptionKeyStoreProviderCore { get; }

        /// <summary>
        /// Unwraps the specified <paramref name="encryptedKey"/> of a data encryption key. The encrypted value is expected to be encrypted using
        /// the key encryption key with the specified <paramref name="encryptionKeyId"/> and using the specified <paramref name="algorithm"/>.
        /// </summary>
        /// <param name="encryptionKeyId">The key Id tells the provider where to find the key.</param>
        /// <param name="keyEncryptionKeyAlgorithm">The encryption algorithm.</param>
        /// <param name="encryptedKey">The ciphertext key.</param>
        /// <returns>The unwrapped data encryption key.</returns>
        public abstract Task<byte[]> UnwrapKeyAsync(string encryptionKeyId, string keyEncryptionKeyAlgorithm, byte[] encryptedKey);

        /// <summary>
        /// Wraps a data encryption key using the key encryption key with the specified <paramref name="encryptionKeyId"/> and using the specified <paramref name="algorithm"/>.
        /// </summary>
        /// <param name="encryptionKeyId">The key Id tells the provider where to find the key.</param>
        /// <param name="keyEncryptionKeyAlgorithm">The encryption algorithm.</param>
        /// <param name="key">The plaintext key.</param>
        /// <returns>The wrapped data encryption key.</returns>
        public abstract Task<byte[]> WrapKeyAsync(string encryptionKeyId, string keyEncryptionKeyAlgorithm, byte[] key);

        /// <summary>
        /// Returns the cached decrypted data encryption key, or unwraps the encrypted data encryption if not present.
        /// </summary>
        /// <param name="encryptedDataEncryptionKey">Encrypted Data Encryption Key. </param>
        /// <param name="createItem">The delegate function that will decrypt the encrypted column encryption key.</param>
        /// <returns>Return cached Data Encryption Key.</returns>
        protected virtual async Task<byte[]> GetOrCreateDataEncryptionKeyAsync(string encryptedDataEncryptionKey, Func<byte[]> createItem)
        {
            return await Task.Run(() => this.CosmosEncryptionKeyStoreProviderCore.GetOrCreateDataEncryptionKey(encryptedDataEncryptionKey, createItem));
        }

        /// <summary>
        /// Returns the cached signature verification result, or proceeds to verify if not present.
        /// </summary>
        /// <param name="keyInformation">The encryptionKeyId, allowEnclaveComputations and hexadecimal signature.</param>
        /// <param name="createItem">The delegate function that will perform the verification.</param>
        /// <returns> Return signature verification result. </returns>
        protected virtual async Task<bool> GetOrCreateSignatureVerificationResultAsync(Tuple<string, bool, string> keyInformation, Func<bool> createItem)
        {
            return await Task.Run(() => this.CosmosEncryptionKeyStoreProviderCore.GetOrCreateSignatureVerificationResult(keyInformation, createItem));
        }
    }
}
