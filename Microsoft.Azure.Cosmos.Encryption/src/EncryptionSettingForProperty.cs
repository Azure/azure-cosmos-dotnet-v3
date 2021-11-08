//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using Microsoft.Data.Encryption.Cryptography;

    internal sealed class EncryptionSettingForProperty
    {
        private static readonly SemaphoreSlim EncryptionKeyCacheSemaphore = new SemaphoreSlim(1, 1);

        private readonly string databaseRid;

        private readonly EncryptionContainer encryptionContainer;

        public EncryptionSettingForProperty(
            string clientEncryptionKeyId,
            EncryptionType encryptionType,
            EncryptionContainer encryptionContainer,
            string databaseRid)
        {
            this.ClientEncryptionKeyId = string.IsNullOrEmpty(clientEncryptionKeyId) ? throw new ArgumentNullException(nameof(clientEncryptionKeyId)) : clientEncryptionKeyId;
            this.EncryptionType = encryptionType;
            this.encryptionContainer = encryptionContainer ?? throw new ArgumentNullException(nameof(encryptionContainer));
            this.databaseRid = string.IsNullOrEmpty(databaseRid) ? throw new ArgumentNullException(nameof(databaseRid)) : databaseRid;
        }

        public string ClientEncryptionKeyId { get; }

        public EncryptionType EncryptionType { get; }

        public async Task<AeadAes256CbcHmac256EncryptionAlgorithm> BuildEncryptionAlgorithmForSettingAsync(CancellationToken cancellationToken)
        {
            ClientEncryptionKeyProperties clientEncryptionKeyProperties = await this.encryptionContainer.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                    clientEncryptionKeyId: this.ClientEncryptionKeyId,
                    encryptionContainer: this.encryptionContainer,
                    databaseRid: this.databaseRid,
                    cancellationToken: cancellationToken);

            ProtectedDataEncryptionKey protectedDataEncryptionKey;

            try
            {
                // we pull out the Encrypted Data Encryption Key and build the Protected Data Encryption key
                // Here a request is sent out to unwrap using the Master Key configured via the Key Encryption Key.
                protectedDataEncryptionKey = await this.BuildProtectedDataEncryptionKeyAsync(
                    clientEncryptionKeyProperties,
                    this.encryptionContainer.EncryptionCosmosClient.EncryptionKeyStoreProvider,
                    this.ClientEncryptionKeyId,
                    cancellationToken);
            }
            catch (RequestFailedException ex)
            {
                // The access to master key was probably revoked. Try to fetch the latest ClientEncryptionKeyProperties from the backend.
                // This will succeed provided the user has rewraped the Client Encryption Key with right set of meta data.
                // This is based on the AKV provider implementaion so we expect a RequestFailedException in case other providers are used in unwrap implementation.
                if (ex.Status == (int)HttpStatusCode.Forbidden)
                {
                    clientEncryptionKeyProperties = await this.encryptionContainer.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                        clientEncryptionKeyId: this.ClientEncryptionKeyId,
                        encryptionContainer: this.encryptionContainer,
                        databaseRid: this.databaseRid,
                        cancellationToken: cancellationToken,
                        shouldForceRefresh: true);

                    // just bail out if this fails.
                    protectedDataEncryptionKey = await this.BuildProtectedDataEncryptionKeyAsync(
                        clientEncryptionKeyProperties,
                        this.encryptionContainer.EncryptionCosmosClient.EncryptionKeyStoreProvider,
                        this.ClientEncryptionKeyId,
                        cancellationToken);
                }
                else
                {
                    throw;
                }
            }

            AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm = new AeadAes256CbcHmac256EncryptionAlgorithm(
                   protectedDataEncryptionKey,
                   this.EncryptionType);

            return aeadAes256CbcHmac256EncryptionAlgorithm;
        }

        private async Task<ProtectedDataEncryptionKey> BuildProtectedDataEncryptionKeyAsync(
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            EncryptionKeyStoreProvider encryptionKeyStoreProvider,
            string keyId,
            CancellationToken cancellationToken)
        {
            if (await EncryptionKeyCacheSemaphore.WaitAsync(-1, cancellationToken))
            {
                try
                {
                    KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                        clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Name,
                        clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Value,
                        encryptionKeyStoreProvider);

                    ProtectedDataEncryptionKey protectedDataEncryptionKey = ProtectedDataEncryptionKey.GetOrCreate(
                        keyId,
                        keyEncryptionKey,
                        clientEncryptionKeyProperties.WrappedDataEncryptionKey);

                    return protectedDataEncryptionKey;
                }
                finally
                {
                    EncryptionKeyCacheSemaphore.Release(1);
                }
            }

            throw new InvalidOperationException("Failed to build ProtectedDataEncryptionKey. ");
        }
    }
}