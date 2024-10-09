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
    /// Extension methods for <see cref="Database"/> to support client-side encryption.
    /// </summary>
    [CLSCompliant(false)]
    public static class EncryptionDatabaseExtensions
    {
        /// <summary>
        /// Creates a client encryption key.
        /// This generates a cryptographically random data encryption key, wraps it using
        /// information provided in the encryptionKeyWrapMetadata using the IKeyEncryptionKeyResolver instance configured on the client,
        /// and saves the wrapped data encryption key along with the encryptionKeyWrapMetadata at the Cosmos DB service.
        /// </summary>
        /// <param name="database">Database supporting encryption in which the client encryption key properties will be saved.</param>
        /// <param name="clientEncryptionKeyId">Identifier for the client encryption key.</param>
        /// <param name="encryptionAlgorithm">Algorithm which will be used for encryption with this key.</param>
        /// <param name="encryptionKeyWrapMetadata">Metadata used to wrap the data encryption key with a key encryption key.</param>
        /// <param name="cancellationToken">Token for request cancellation.</param>
        /// <returns>Response from the Cosmos DB service with <see cref="ClientEncryptionKeyProperties"/>.</returns>
        /// <example>
        /// This example shows how to create a client encryption key.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// Azure.Core.TokenCredential tokenCredential = new Azure.Identity.DefaultAzureCredential();
        /// Azure.Core.Cryptography.IKeyEncryptionKeyResolver keyResolver = new Azure.Security.KeyVault.Keys.Cryptography.KeyResolver(tokenCredential);
        /// CosmosClient client = (new CosmosClient(endpoint, authKey)).WithEncryption(keyResolver, KeyEncryptionKeyResolverName.AzureKeyVault);
        ///
        /// EncryptionKeyWrapMetadata wrapMetadata = new EncryptionKeyWrapMetadata(
        ///    type: KeyEncryptionKeyResolverName.AzureKeyVault,
        ///    name: "myKek",
        ///    value: "https://contoso.vault.azure.net/keys/myKek/78deebed173b48e48f55abf87ed4cf71",
        ///    algorithm: Azure.Security.KeyVault.Keys.Cryptography.EncryptionAlgorithm.RsaOaep.ToString());
        ///
        ///  ClientEncryptionKeyResponse response = await client.GetDatabase("databaseId").CreateClientEncryptionKeyAsync(
        ///    "myCek",
        ///    DataEncryptionAlgorithm.AeadAes256CbcHmacSha256,
        ///    wrapMetadata);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// See <see href="https://aka.ms/CosmosClientEncryption">client-side encryption documentation</see> for more details.
        /// </remarks>
        public static async Task<ClientEncryptionKeyResponse> CreateClientEncryptionKeyAsync(
            this Database database,
            string clientEncryptionKeyId,
            string encryptionAlgorithm,
            EncryptionKeyWrapMetadata encryptionKeyWrapMetadata,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(clientEncryptionKeyId))
            {
                throw new ArgumentNullException(nameof(clientEncryptionKeyId));
            }

            if (!string.Equals(encryptionAlgorithm, DataEncryptionAlgorithm.AeadAes256CbcHmacSha256))
            {
                throw new ArgumentException($"Invalid encryption algorithm '{encryptionAlgorithm}' passed. Please refer to https://aka.ms/CosmosClientEncryption for more details.");
            }

            if (encryptionKeyWrapMetadata == null)
            {
                throw new ArgumentNullException(nameof(encryptionKeyWrapMetadata));
            }

            if (encryptionKeyWrapMetadata.Algorithm != EncryptionKeyStoreProviderImpl.RsaOaepWrapAlgorithm)
            {
                throw new ArgumentException($"Invalid key wrap algorithm '{encryptionKeyWrapMetadata.Algorithm}' passed. Please refer to https://aka.ms/CosmosClientEncryption for more details.");
            }

            EncryptionCosmosClient encryptionCosmosClient = database is EncryptionDatabase encryptionDatabase
                ? encryptionDatabase.EncryptionCosmosClient
                : throw new ArgumentException("Creating a client encryption key requires the use of an encryption-enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details.");

            if (!string.Equals(encryptionKeyWrapMetadata.Type, encryptionCosmosClient.KeyEncryptionKeyResolverName))
            {
                throw new ArgumentException($"The Type of the EncryptionKeyWrapMetadata '{encryptionKeyWrapMetadata.Type}' does not match"
                    + $" with the keyEncryptionKeyResolverName '{encryptionCosmosClient.KeyEncryptionKeyResolverName}' configured."
                    + " Please refer to https://aka.ms/CosmosClientEncryption for more details.");
            }

            if (string.Equals(encryptionCosmosClient.KeyEncryptionKeyResolverName, KeyEncryptionKeyResolverName.AzureKeyVault))
            {
                // https://KEYVAULTNAME.vault.azure.net/keys/KEYNAME/KEYVERSION
                string[] keyVaultUriSegments = new Uri(encryptionKeyWrapMetadata.Value).Segments;

                if (keyVaultUriSegments.Length != 4 || !string.Equals(keyVaultUriSegments[1], "keys/", StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new ArgumentException($"Invalid Key Vault URI'{encryptionKeyWrapMetadata.Value}' passed. Pass the complete Azure keyvault key identifier. Please refer to https://aka.ms/CosmosClientEncryption for more details.");
                }
            }

            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                encryptionKeyWrapMetadata.Name,
                encryptionKeyWrapMetadata.Value,
                encryptionCosmosClient.EncryptionKeyStoreProviderImpl);

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
        /// Rewraps a client encryption key.
        /// This wraps the existing data encryption key using information provided in the newEncryptionKeyWrapMetadata
        /// using the IKeyEncryptionKeyResolver instance configured on the client,
        /// and saves the wrapped data encryption key along with the newEncryptionKeyWrapMetadata at the Cosmos DB service.
        /// </summary>
        /// <param name="database">Database supporting encryption in which the client encryption key properties will be updated.</param>
        /// <param name="clientEncryptionKeyId">Identifier for the client encryption key.</param>
        /// <param name="newEncryptionKeyWrapMetadata">Metadata used to wrap the data encryption key with a key encryption key.</param>
        /// <param name="cancellationToken">Token for request cancellation.</param>
        /// <returns>Response from the Cosmos DB service with updated <see cref="ClientEncryptionKeyProperties"/>.</returns>
        /// <example>
        /// This example shows how to rewrap a client encryption key.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// Azure.Core.TokenCredential tokenCredential = new Azure.Identity.DefaultAzureCredential();
        /// Azure.Core.Cryptography.IKeyEncryptionKeyResolver keyResolver = new Azure.Security.KeyVault.Keys.Cryptography.KeyResolver(tokenCredential);
        /// CosmosClient client = (new CosmosClient(endpoint, authKey)).WithEncryption(keyResolver, KeyEncryptionKeyResolverName.AzureKeyVault);
        ///
        /// EncryptionKeyWrapMetadata wrapMetadataForNewKeyVersion = new EncryptionKeyWrapMetadata(
        ///    type: KeyEncryptionKeyResolverName.AzureKeyVault,
        ///    name: "myKek",
        ///    value: "https://contoso.vault.azure.net/keys/myKek/0698c2156c1a4e1da5b6bab6f6422fd6",
        ///    algorithm: Azure.Security.KeyVault.Keys.Cryptography.EncryptionAlgorithm.RsaOaep.ToString());
        ///
        ///  ClientEncryptionKeyResponse response = await client.GetDatabase("databaseId").RewrapClientEncryptionKeyAsync(
        ///    "myCek",
        ///    wrapMetadataForNewKeyVersion);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// See <see href="https://aka.ms/CosmosClientEncryption">client-side encryption documentation</see> for more details.
        /// </remarks>
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

            if (newEncryptionKeyWrapMetadata.Algorithm != EncryptionKeyStoreProviderImpl.RsaOaepWrapAlgorithm)
            {
                throw new ArgumentException($"Invalid key wrap algorithm '{newEncryptionKeyWrapMetadata.Algorithm}' passed. Please refer to https://aka.ms/CosmosClientEncryption for more details.");
            }

            ClientEncryptionKey clientEncryptionKey = database.GetClientEncryptionKey(clientEncryptionKeyId);

            EncryptionCosmosClient encryptionCosmosClient = database is EncryptionDatabase encryptionDatabase
                ? encryptionDatabase.EncryptionCosmosClient
                : throw new ArgumentException("Rewrapping a client encryption key requires the use of an encryption-enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details.");

            if (!string.Equals(newEncryptionKeyWrapMetadata.Type, encryptionCosmosClient.KeyEncryptionKeyResolverName))
            {
                throw new ArgumentException($"The Type of the EncryptionKeyWrapMetadata '{newEncryptionKeyWrapMetadata.Type}' does not match"
                    + $" with the keyEncryptionKeyResolverName '{encryptionCosmosClient.KeyEncryptionKeyResolverName}' configured."
                    + " Please refer to https://aka.ms/CosmosClientEncryption for more details.");
            }

            if (string.Equals(encryptionCosmosClient.KeyEncryptionKeyResolverName, KeyEncryptionKeyResolverName.AzureKeyVault))
            {
                // https://KEYVAULTNAME.vault.azure.net/keys/KEYNAME/KEYVERSION
                string[] keyVaultUriSegments = new Uri(newEncryptionKeyWrapMetadata.Value).Segments;

                if (keyVaultUriSegments.Length != 4 || !string.Equals(keyVaultUriSegments[1], "keys/", StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new ArgumentException($"Invalid Key Vault URI'{newEncryptionKeyWrapMetadata.Value}' passed. Pass the complete Azure keyvault key identifier. Please refer to https://aka.ms/CosmosClientEncryption for more details.");
                }
            }

            ClientEncryptionKeyProperties clientEncryptionKeyProperties = await clientEncryptionKey.ReadAsync(cancellationToken: cancellationToken);

            RequestOptions requestOptions = new RequestOptions
            {
                IfMatchEtag = clientEncryptionKeyProperties.ETag,
            };

            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Name,
                clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Value,
                encryptionCosmosClient.EncryptionKeyStoreProviderImpl);

            byte[] unwrappedKey = keyEncryptionKey.DecryptEncryptionKey(clientEncryptionKeyProperties.WrappedDataEncryptionKey);

            keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                newEncryptionKeyWrapMetadata.Name,
                newEncryptionKeyWrapMetadata.Value,
                encryptionCosmosClient.EncryptionKeyStoreProviderImpl);

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
