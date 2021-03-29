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
        /// <param name="dataEncryptionKeyAlgorithm"> Encryption Algorthm. </param>
        /// <param name="encryptionKeyWrapMetadata"> EncryptionKeyWrapMetadata.</param>
        /// <param name="cancellationToken"> cancellation token </param>
        /// <returns>Container to perform operations supporting client-side encryption / decryption.</returns>
        /// <example>
        /// This example shows how to create a new Client Encryption Key.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// ClientEncryptionKeyResponse response = await this.cosmosDatabase.CreateClientEncryptionKeyAsync(
        ///     "testKey",
        ///     DataEncryptionKeyAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256,
        ///     new EncryptionKeyWrapMetadata("metadataName", "MetadataValue"));
        /// ]]>
        /// </code>
        /// </example>
        public static async Task<ClientEncryptionKeyResponse> CreateClientEncryptionKeyAsync(
            this Database database,
            string clientEncryptionKeyId,
            DataEncryptionKeyAlgorithm dataEncryptionKeyAlgorithm,
            EncryptionKeyWrapMetadata encryptionKeyWrapMetadata,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(clientEncryptionKeyId))
            {
                throw new ArgumentNullException(nameof(clientEncryptionKeyId));
            }

            string encryptionAlgorithm = dataEncryptionKeyAlgorithm.ToString();

            if (!string.Equals(encryptionAlgorithm, DataEncryptionKeyAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256.ToString()))
            {
                throw new ArgumentException($"Invalid Encryption Algorithm '{encryptionAlgorithm}' passed. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
            }

            if (encryptionKeyWrapMetadata == null)
            {
                throw new ArgumentNullException(nameof(encryptionKeyWrapMetadata));
            }

            EncryptionCosmosClient encryptionCosmosClient;

            if (database is EncryptionDatabase encryptionDatabase)
            {
                encryptionCosmosClient = encryptionDatabase.EncryptionCosmosClient;
            }
            else
            {
                throw new ArgumentException("Creating a ClientEncryptionKey resource requires the use of an encryption - enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
            }

            EncryptionKeyStoreProvider encryptionKeyStoreProvider = encryptionCosmosClient.EncryptionKeyStoreProvider;

            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                encryptionKeyWrapMetadata.Name,
                encryptionKeyWrapMetadata.Value,
                encryptionKeyStoreProvider);

            ProtectedDataEncryptionKey protectedDataEncryptionKey = new ProtectedDataEncryptionKey(
                clientEncryptionKeyId,
                keyEncryptionKey);

            byte[] wrappedDataEncryptionKey = protectedDataEncryptionKey.EncryptedValue;

            // cache it.
            ProtectedDataEncryptionKey.GetOrCreate(
                   clientEncryptionKeyId,
                   keyEncryptionKey,
                   wrappedDataEncryptionKey);

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
        /// Rewrap an existing Client Encryption Key.
        /// </summary>
        /// <param name="database">Regular cosmos database.</param>
        /// <param name="clientEncryptionKeyId"> Client Encryption Key id.</param>
        /// <param name="newEncryptionKeyWrapMetadata"> EncryptionKeyWrapMetadata.</param>
        /// <param name="cancellationToken"> cancellation token </param>
        /// <returns>Container to perform operations supporting client-side encryption / decryption.</returns>
        /// <example>
        /// This example shows how to rewrap a Client Encryption Key.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// ClientEncryptionKeyResponse response = await this.cosmosDatabase.RewrapClientEncryptionKeyAsync(
        ///     "keyToRewrap",
        ///     new EncryptionKeyWrapMetadata("metadataName", "UpdatedMetadataValue")));
        /// ]]>
        /// </code>
        /// </example>
        public static async Task<ClientEncryptionKeyResponse> RewrapClientEncryptionKeyAsync(
            this Database database,
            string clientEncryptionKeyId,
            EncryptionKeyWrapMetadata newEncryptionKeyWrapMetadata,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(clientEncryptionKeyId))
            {
                throw new ArgumentNullException(nameof(clientEncryptionKeyId));
            }

            if (newEncryptionKeyWrapMetadata == null)
            {
                throw new ArgumentNullException(nameof(newEncryptionKeyWrapMetadata));
            }

            ClientEncryptionKey clientEncryptionKey = database.GetClientEncryptionKey(clientEncryptionKeyId);

            EncryptionCosmosClient encryptionCosmosClient;

            if (database is EncryptionDatabase encryptionDatabase)
            {
                encryptionCosmosClient = encryptionDatabase.EncryptionCosmosClient;
            }
            else
            {
                throw new ArgumentException("Rewraping a ClientEncryptionKey requires the use of an encryption - enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
            }

            EncryptionKeyStoreProvider encryptionKeyStoreProvider = encryptionCosmosClient.EncryptionKeyStoreProvider;

            ClientEncryptionKeyProperties clientEncryptionKeyProperties = await clientEncryptionKey.ReadAsync(cancellationToken: cancellationToken);

            RequestOptions requestOptions = new RequestOptions
            {
                IfMatchEtag = clientEncryptionKeyProperties.ETag,
            };

            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Name,
                clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Value,
                encryptionKeyStoreProvider);

            byte[] unwrappedKey = keyEncryptionKey.DecryptEncryptionKey(clientEncryptionKeyProperties.WrappedDataEncryptionKey);

            keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                newEncryptionKeyWrapMetadata.Name,
                newEncryptionKeyWrapMetadata.Value,
                encryptionKeyStoreProvider);

            byte[] rewrappedKey = keyEncryptionKey.EncryptEncryptionKey(unwrappedKey);

            clientEncryptionKeyProperties = new ClientEncryptionKeyProperties(
                clientEncryptionKeyId,
                clientEncryptionKeyProperties.EncryptionAlgorithm,
                rewrappedKey,
                newEncryptionKeyWrapMetadata);

            ClientEncryptionKeyResponse clientEncryptionKeyResponse = await clientEncryptionKey.ReplaceAsync(
                clientEncryptionKeyProperties,
                requestOptions,
                cancellationToken: cancellationToken);

            return clientEncryptionKeyResponse;
        }
    }
}
