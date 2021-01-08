//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// This class provides extension methods for <see cref="Database"/>.
    /// </summary>
    public static class EncryptionDatabaseExtensions
    {
        /// <summary>
        /// Create a Client Encryption Key used to Encrypt data.
        /// </summary>
        /// <param name="database">Regular cosmos database.</param>
        /// <param name="clientEncryptionKeyId"> Client Encryption Key id.</param>
        /// <param name="encryptionAlgorithm"> Encryption Algorthm. </param>
        /// <param name="encryptionKeyWrapMetadata"> EncryptionKeyWrapMetadata.</param>
        /// <param name="cancellationToken"> cancellation token </param>
        /// <returns>Container to perform operations supporting client-side encryption / decryption.</returns>
        public static async Task<ClientEncryptionKeyResponse> CreateClientEncryptionKeyAsync(
            this Database database,
            string clientEncryptionKeyId,
            string encryptionAlgorithm,
            Cosmos.EncryptionKeyWrapMetadata encryptionKeyWrapMetadata,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            EncryptionCosmosClient encryptionCosmosClient;

            if (database is EncryptionDatabase encryptionDatabase)
            {
                encryptionCosmosClient = encryptionDatabase.EncryptionCosmosClient;
            }
            else
            {
                throw new ArgumentException("Unsupported database type.For Creating Client Encryption Keys please configure Cosmos Client with Encryption Support");
            }

            EncryptionKeyStoreProvider encryptionKeyStoreProvider = encryptionCosmosClient.EncryptionKeyStoreProvider;

            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                encryptionKeyWrapMetadata.Name,
                encryptionKeyWrapMetadata.Value,
                encryptionKeyStoreProvider);

            ProtectedDataEncryptionKey protectedDataEncryptionKey = new ProtectedDataEncryptionKey(
                encryptionKeyWrapMetadata.Name,
                keyEncryptionKey);

            byte[] wrappedDataEncryptionKey = protectedDataEncryptionKey.EncryptedValue;

            ClientEncryptionKeyProperties clientEncryptionKeyProperties = new ClientEncryptionKeyProperties(
                clientEncryptionKeyId,
                encryptionAlgorithm,
                wrappedDataEncryptionKey,
                encryptionKeyWrapMetadata);

            ClientEncryptionKeyResponse clientEncryptionKeyResponse = await database.CreateClientEncryptionKeyAsync(
                clientEncryptionKeyProperties,
                cancellationToken: cancellationToken);

            return clientEncryptionKeyResponse;
        }

        /// <summary>
        /// ReWrap an existing Client Encryption Key.
        /// </summary>
        /// <param name="database">Regular cosmos database.</param>
        /// <param name="clientEncryptionKeyId"> Client Encryption Key id.</param>
        /// <param name="encryptionAlgorithm"> Encryption Algorthm. </param>
        /// <param name="encryptionKeyWrapMetadata"> EncryptionKeyWrapMetadata.</param>
        /// <param name="cancellationToken"> cancellation token </param>
        /// <returns>Container to perform operations supporting client-side encryption / decryption.</returns>
        public static async Task<ClientEncryptionKeyResponse> RewrapClientEncryptionKeyAsync(
            this Database database,
            string clientEncryptionKeyId,
            string encryptionAlgorithm,
            Cosmos.EncryptionKeyWrapMetadata encryptionKeyWrapMetadata,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ClientEncryptionKey clientEncryptionKey = database.GetClientEncryptionKey(clientEncryptionKeyId);

            EncryptionCosmosClient encryptionCosmosClient;

            if (database is EncryptionDatabase encryptionDatabase)
            {
                encryptionCosmosClient = encryptionDatabase.EncryptionCosmosClient;
            }
            else
            {
                throw new ArgumentException("Unsupported database type.For rewraping Client Encryption Keys please configure Cosmos Client with Encryption Support");
            }

            EncryptionKeyStoreProvider encryptionKeyStoreProvider = encryptionCosmosClient.EncryptionKeyStoreProvider;

            ClientEncryptionKeyProperties clientEncryptionKeyProperties = await clientEncryptionKey.ReadAsync(cancellationToken: cancellationToken);

            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                encryptionKeyWrapMetadata.Name,
                encryptionKeyWrapMetadata.Value,
                encryptionKeyStoreProvider);

            byte[] unWrappedKey = keyEncryptionKey.DecryptEncryptionKey(clientEncryptionKeyProperties.WrappedDataEncryptionKey);

            byte[] reWrappedKey = keyEncryptionKey.EncryptEncryptionKey(unWrappedKey);

            clientEncryptionKeyProperties = new ClientEncryptionKeyProperties(
                clientEncryptionKeyId,
                encryptionAlgorithm,
                reWrappedKey,
                encryptionKeyWrapMetadata);

            ClientEncryptionKeyResponse clientEncryptionKeyResponse = await clientEncryptionKey.ReplaceAsync(
                clientEncryptionKeyProperties,
                cancellationToken: cancellationToken);

            return clientEncryptionKeyResponse;
        }
    }
}
