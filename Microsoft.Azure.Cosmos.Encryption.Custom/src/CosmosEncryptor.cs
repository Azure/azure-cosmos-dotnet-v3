﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides the default implementation for client-side encryption for Cosmos DB.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public sealed class CosmosEncryptor : Encryptor
    {
        /// <summary>
        /// Gets Container for data encryption keys.
        /// </summary>
        public DataEncryptionKeyProvider DataEncryptionKeyProvider { get; }

        /// <summary>
        /// Initializes a new instance of Cosmos Encryptor.
        /// </summary>
        /// <param name="dataEncryptionKeyProvider">DataEncryptionKeyProvider instance.</param>
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
                throw new InvalidOperationException($"Null {nameof(DataEncryptionKey)} returned from {nameof(this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync)}.");
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

            if (dek == null)
            {
                throw new InvalidOperationException($"Null {nameof(DataEncryptionKey)} returned from {nameof(this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync)}.");
            }

            return dek.EncryptData(plainText);
        }
    }
}
