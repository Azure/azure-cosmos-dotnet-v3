//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using global::Azure.Core.Cryptography;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// This class provides extension methods for <see cref="CosmosClient"/>.
    /// </summary>
    public static class EncryptionCosmosClientExtensions
    {
        /// <summary>
        /// Get Cosmos Client with Encryption support for performing operations using client-side encryption.
        /// </summary>
        /// <param name="cosmosClient">Regular Cosmos Client.</param>
        /// <param name="keyEncryptionKeyResolver">IKeyEncryptionKeyResolver that allows interaction with the key encryption keys.</param>
        /// <param name="keyEncryptionKeyResolverId">Identifier of the resolver, eg. KeyEncryptionKeyResolverId.AzureKeyVault. </param>
        /// <param name="keyCacheTimeToLive">Time for which raw keys are cached in-memory. Defaults to 1 hour.</param>
        /// <returns> CosmosClient to perform operations supporting client-side encryption / decryption.</returns>
        public static CosmosClient WithEncryption(
            this CosmosClient cosmosClient,
            IKeyEncryptionKeyResolver keyEncryptionKeyResolver,
            string keyEncryptionKeyResolverId,
            TimeSpan? keyCacheTimeToLive = null)
        {
            if (keyEncryptionKeyResolver == null)
            {
                throw new ArgumentNullException(nameof(keyEncryptionKeyResolver));
            }

            if (keyEncryptionKeyResolverId == null)
            {
                throw new ArgumentNullException(nameof(keyEncryptionKeyResolverId));
            }

            if (cosmosClient == null)
            {
                throw new ArgumentNullException(nameof(cosmosClient));
            }

            return new EncryptionCosmosClient(cosmosClient, keyEncryptionKeyResolver, keyEncryptionKeyResolverId, keyCacheTimeToLive);
        }
    }
}
