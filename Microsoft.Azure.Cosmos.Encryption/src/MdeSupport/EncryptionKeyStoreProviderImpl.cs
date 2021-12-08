//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// The purpose/intention to introduce this class is to utilize the cache provide by the <see cref="EncryptionKeyStoreProvider"/> abstract class. This class basically
    /// redirects all the corresponding calls to <see cref="CosmosEncryptionKeyStoreProvider"/> 's overridden methods and thus allowing us
    /// to utilize the virtual method <see cref="EncryptionKeyStoreProvider.GetOrCreateDataEncryptionKey"/> to access the cache.
    ///
    /// Note: Since <see cref="EncryptionKeyStoreProvider.Sign"/> and <see cref="EncryptionKeyStoreProvider.Verify"/> methods are not exposed, <see cref="EncryptionKeyStoreProvider.GetOrCreateSignatureVerificationResult"/> is not supported either.
    /// </summary>
    internal class EncryptionKeyStoreProviderImpl : EncryptionKeyStoreProvider
    {
        private readonly CosmosEncryptionKeyStoreProvider cosmosEncryptionKeyStoreProvider;

        public EncryptionKeyStoreProviderImpl(CosmosEncryptionKeyStoreProvider cosmosEncryptionKeyStoreProvider)
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

        public byte[] GetOrCreateDataEncryptionKeyHelper(string encryptedDataEncryptionKey, Func<byte[]> createItem)
        {
            return this.GetOrCreateDataEncryptionKey(encryptedDataEncryptionKey, createItem);
        }

        /// <Remark>
        /// The public facing Cosmos Encryption library interface does not expose this method, hence not supported.
        /// </Remark>
        public override byte[] Sign(string encryptionKeyId, bool allowEnclaveComputations)
        {
            throw new NotSupportedException("The Sign operation is not supported. ");
        }

        /// <Remark>
        /// The public facing Cosmos Encryption library interface does not expose this method, hence not supported.
        /// </Remark>
        public override bool Verify(string encryptionKeyId, bool allowEnclaveComputations, byte[] signature)
        {
            throw new NotSupportedException("The Verify operation is not supported. ");
        }
    }
}
