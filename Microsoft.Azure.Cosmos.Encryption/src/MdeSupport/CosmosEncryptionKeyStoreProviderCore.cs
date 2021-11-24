//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using Microsoft.Data.Encryption.Cryptography;

    internal class CosmosEncryptionKeyStoreProviderCore : EncryptionKeyStoreProvider
    {
        private readonly CosmosEncryptionKeyStoreProvider cosmosEncryptionKeyStoreProvider;

        public CosmosEncryptionKeyStoreProviderCore(CosmosEncryptionKeyStoreProvider cosmosEncryptionKeyStoreProvider)
        {
            this.cosmosEncryptionKeyStoreProvider = cosmosEncryptionKeyStoreProvider;
        }

        public override string ProviderName => this.cosmosEncryptionKeyStoreProvider.ProviderName;

        public override byte[] Sign(string encryptionKeyId, bool allowEnclaveComputations)
        {
            throw new NotImplementedException();
        }

        public override byte[] UnwrapKey(string encryptionKeyId, KeyEncryptionKeyAlgorithm algorithm, byte[] encryptedKey)
        {
            return this.cosmosEncryptionKeyStoreProvider.UnwrapKeyAsync(encryptionKeyId, algorithm.ToString(), encryptedKey)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public override bool Verify(string encryptionKeyId, bool allowEnclaveComputations, byte[] signature)
        {
            throw new NotImplementedException();
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

        public new bool GetOrCreateSignatureVerificationResult(Tuple<string, bool, string> keyInformation, Func<bool> createItem)
        {
            return base.GetOrCreateSignatureVerificationResult(keyInformation, createItem);
        }
    }
}
