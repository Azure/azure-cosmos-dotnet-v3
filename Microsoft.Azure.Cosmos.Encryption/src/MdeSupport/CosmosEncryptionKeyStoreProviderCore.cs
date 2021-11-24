//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// The purpose of this class is to utilize the cache provide by the EncryptionKeyStoreProvider abstract class. This class basically
    /// redirects all the corresponding calls to <see cref="CosmosEncryptionKeyStoreProvider"/> overridden methods and thus allowing us
    /// to utilize the virtual method <see cref="EncryptionKeyStoreProvider.GetOrCreateDataEncryptionKey"/> to access the cache.
    ///
    /// Note: Since <see cref="EncryptionKeyStoreProvider.Sign"/> and <see cref="EncryptionKeyStoreProvider.Verify"/> methods are not exposed, <see cref="EncryptionKeyStoreProvider.GetOrCreateSignatureVerificationResult"/> is not supported either.
    /// </summary>
    internal class CosmosEncryptionKeyStoreProviderCore : EncryptionKeyStoreProvider
    {
        private readonly CosmosEncryptionKeyStoreProvider cosmosEncryptionKeyStoreProvider;

        public CosmosEncryptionKeyStoreProviderCore(CosmosEncryptionKeyStoreProvider cosmosEncryptionKeyStoreProvider)
        {
            this.cosmosEncryptionKeyStoreProvider = cosmosEncryptionKeyStoreProvider;
        }

        public override string ProviderName => this.cosmosEncryptionKeyStoreProvider.ProviderName;

        public override byte[] UnwrapKey(string encryptionKeyId, KeyEncryptionKeyAlgorithm algorithm, byte[] encryptedKey)
        {
            return this.cosmosEncryptionKeyStoreProvider.UnwrapKeyAsync(encryptionKeyId, algorithm.ToString(), encryptedKey)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public override byte[] WrapKey(string encryptionKeyId, KeyEncryptionKeyAlgorithm algorithm, byte[] key)
        {
            return this.cosmosEncryptionKeyStoreProvider.WrapKeyAsync(encryptionKeyId, algorithm.ToString(), key)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public new byte[] GetOrCreateDataEncryptionKey(string encryptedDataEncryptionKey, Func<byte[]> createItem)
        {
            return base.GetOrCreateDataEncryptionKey(encryptedDataEncryptionKey, createItem);
        }

        /// <Remark>
        /// The public facing Cosmos Encryption library interface does not expose this method, hence not supported.
        /// </Remark>
        public override byte[] Sign(string encryptionKeyId, bool allowEnclaveComputations)
        {
            throw new NotSupportedException("This operation is not supported.  Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
        }

        /// <Remark>
        /// The public facing Cosmos Encryption library interface does not expose this method, hence not supported.
        /// </Remark>
        public override bool Verify(string encryptionKeyId, bool allowEnclaveComputations, byte[] signature)
        {
            throw new NotSupportedException("This operation is not supported.  Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
        }
    }
}
