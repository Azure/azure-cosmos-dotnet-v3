//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Cosmos.Encryption
{
    /// <summary>
    /// Provides the default implementation for client-side encryption for Cosmos DB.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    class CosmosEncryptor : Encryptor
    {
        public DataEncryptionKeyProvider DataEncryptionKeyProvider { get; }

        /// <summary>
        /// Initializes a new instance of Cosmos Encryptor.
        /// </summary>
        /// <param name="dataEncryptionKeyProvider"></param>
        public CosmosEncryptor(DataEncryptionKeyProvider dataEncryptionKeyProvider)
        {
            this.DataEncryptionKeyProvider = dataEncryptionKeyProvider;
        }

        /// <inheritdoc/>
        public override async Task<byte[]> DecryptAsync(
            byte[] cipherText, 
            string dataEncryptionKeyId, 
            string encryptionAlgorithm, 
            CancellationToken cancellationToken = default)
        {
            DataEncryptionKey dek = await this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync(
                dataEncryptionKeyId,
                encryptionAlgorithm,
                cancellationToken);

            if (dek == null)
            {
                throw new InvalidOperationException($"Null {nameof(DataEncryptionKey)} returned from {nameof(DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync)}.");
            }

            return dek.DecryptData(cipherText);
        }

        /// <inheritdoc/>
        public override async Task<byte[]> EncryptAsync(
            byte[] plainText, 
            string dataEncryptionKeyId, 
            string encryptionAlgorithm, 
            CancellationToken cancellationToken = default)
        {
            DataEncryptionKey dek = await this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync(
                dataEncryptionKeyId, 
                encryptionAlgorithm,
                cancellationToken);

            if(dek == null)
            {
                throw new InvalidOperationException($"Null {nameof(DataEncryptionKey)} returned from {nameof(DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync)}.");
            }
            
            return dek.EncryptData(plainText);
        }
    }
}
