//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
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
        /// <param name="encryptionKeyWrapProvider">EncryptionKeyWrapProvider, provider that allows interaction with the master keys.</param>
        /// <returns> CosmosClient to perform operations supporting client-side encryption / decryption.</returns>
        public static CosmosClient WithEncryption(
            this CosmosClient cosmosClient,
            EncryptionKeyWrapProvider encryptionKeyWrapProvider)
        {
            if (encryptionKeyWrapProvider == null)
            {
                throw new ArgumentNullException(nameof(encryptionKeyWrapProvider));
            }

            if (cosmosClient == null)
            {
                throw new ArgumentNullException(nameof(cosmosClient));
            }

            return new EncryptionCosmosClient(cosmosClient, encryptionKeyWrapProvider);
        }
    }
}
