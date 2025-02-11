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
        private readonly string databaseRid;

        private readonly EncryptionContainer encryptionContainer;

        public EncryptionSettingForProperty(
            string clientEncryptionKeyId,
            Data.Encryption.Cryptography.EncryptionType encryptionType,
            EncryptionContainer encryptionContainer,
            string databaseRid)
        {
            this.ClientEncryptionKeyId = string.IsNullOrEmpty(clientEncryptionKeyId) ? throw new ArgumentNullException(nameof(clientEncryptionKeyId)) : clientEncryptionKeyId;
            this.EncryptionType = encryptionType;
            this.encryptionContainer = encryptionContainer ?? throw new ArgumentNullException(nameof(encryptionContainer));
            this.databaseRid = string.IsNullOrEmpty(databaseRid) ? throw new ArgumentNullException(nameof(databaseRid)) : databaseRid;
        }

        public string ClientEncryptionKeyId { get; }

        public Data.Encryption.Cryptography.EncryptionType EncryptionType { get; }

        public async Task<AeadAes256CbcHmac256EncryptionAlgorithm> BuildEncryptionAlgorithmForSettingAsync(CancellationToken cancellationToken)
        {
            ClientEncryptionKeyProperties clientEncryptionKeyProperties = await this.encryptionContainer.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                    clientEncryptionKeyId: this.ClientEncryptionKeyId,
                    encryptionContainer: this.encryptionContainer,
                    databaseRid: this.databaseRid,
                    ifNoneMatchEtag: null,
                    shouldForceRefresh: false,
                    cancellationToken: cancellationToken);

            ProtectedDataEncryptionKey protectedDataEncryptionKey;
            try
            {
                // we pull out the Encrypted Data Encryption Key and build the Protected Data Encryption key
                // Here a request is sent out to unwrap using the Master Key configured via the Key Encryption Key.
                protectedDataEncryptionKey = await this.BuildProtectedDataEncryptionKeyAsync(
                    clientEncryptionKeyProperties,
                    this.ClientEncryptionKeyId,
                    cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Forbidden)
            {
                // The access to master key was probably revoked. Try to fetch the latest ClientEncryptionKeyProperties from the backend.
                // This will succeed provided the user has rewraped the Client Encryption Key with right set of meta data.
                // This is based on the AKV provider implementaion so we expect a RequestFailedException in case other providers are used in unwrap implementation.
                // first try to force refresh the local cache, we might have a stale cache.
                clientEncryptionKeyProperties = await this.encryptionContainer.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                    clientEncryptionKeyId: this.ClientEncryptionKeyId,
                    encryptionContainer: this.encryptionContainer,
                    databaseRid: this.databaseRid,
                    ifNoneMatchEtag: null,
                    shouldForceRefresh: true,
                    cancellationToken: cancellationToken);

                try
                {
                    // try to build the ProtectedDataEncryptionKey. If it fails, try to force refresh the gateway cache and get the latest client encryption key.
                    protectedDataEncryptionKey = await this.BuildProtectedDataEncryptionKeyAsync(
                        clientEncryptionKeyProperties,
                        this.ClientEncryptionKeyId,
                        cancellationToken);
                }
                catch (RequestFailedException exOnRetry) when (exOnRetry.Status == (int)HttpStatusCode.Forbidden)
                {
                    // the gateway cache could be stale. Force refresh the gateway cache.
                    // bail out if this fails.
                    protectedDataEncryptionKey = await this.ForceRefreshGatewayCacheAndBuildProtectedDataEncryptionKeyAsync(
                        existingCekEtag: clientEncryptionKeyProperties.ETag,
                        refreshRetriedOnException: exOnRetry,
                        cancellationToken: cancellationToken);
                }
            }

            AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm = new AeadAes256CbcHmac256EncryptionAlgorithm(
                   protectedDataEncryptionKey,
                   this.EncryptionType);

            return aeadAes256CbcHmac256EncryptionAlgorithm;
        }

        /// <summary>
        /// Helper function which force refreshes the gateway cache to fetch the latest client encryption key to build ProtectedDataEncryptionKey object for the encryption setting.
        /// </summary>
        /// <param name="existingCekEtag">Client encryption key etag to be passed, which is used as If-None-Match Etag for the request. </param>
        /// <param name="refreshRetriedOnException"> KEK expired exception. </param>
        /// <param name="cancellationToken"> Cancellation token. </param>
        /// <returns>ProtectedDataEncryptionKey object. </returns>
        private async Task<ProtectedDataEncryptionKey> ForceRefreshGatewayCacheAndBuildProtectedDataEncryptionKeyAsync(
            string existingCekEtag,
            Exception refreshRetriedOnException,
            CancellationToken cancellationToken)
        {
            ClientEncryptionKeyProperties clientEncryptionKeyProperties;
            try
            {
                // passing ifNoneMatchEtags results in request being sent out with IfNoneMatchEtag set in RequestOptions, this results in the Gateway cache getting force refreshed.
                // shouldForceRefresh is set to true so that we dont look up our client cache.
                clientEncryptionKeyProperties = await this.encryptionContainer.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                    clientEncryptionKeyId: this.ClientEncryptionKeyId,
                    encryptionContainer: this.encryptionContainer,
                    databaseRid: this.databaseRid,
                    ifNoneMatchEtag: existingCekEtag,
                    shouldForceRefresh: true,
                    cancellationToken: cancellationToken);
            }
            catch (CosmosException ex)
            {
                // if there was a retry with ifNoneMatchEtags, the server will send back NotModified if the key resource has not been modified and is up to date.
                if (ex.StatusCode == HttpStatusCode.NotModified)
                {
                    // looks like the key was never rewrapped with a valid Key Encryption Key.
                    throw new EncryptionCosmosException(
                        $"The Client Encryption Key with key id:{this.ClientEncryptionKeyId} on database:{this.encryptionContainer.Database.Id} and container:{this.encryptionContainer.Id} , needs to be rewrapped with a valid Key Encryption Key using RewrapClientEncryptionKeyAsync. " +
                        $" The Key Encryption Key used to wrap the Client Encryption Key has been revoked: {refreshRetriedOnException.Message}. {ex.Message}." +
                        $" Please refer to https://aka.ms/CosmosClientEncryption for more details. ",
                        HttpStatusCode.BadRequest,
                        int.Parse(Constants.IncorrectContainerRidSubStatus),
                        ex.ActivityId,
                        ex.RequestCharge,
                        ex.Diagnostics);
                }
                else
                {
                    throw;
                }
            }

            ProtectedDataEncryptionKey protectedDataEncryptionKey = await this.BuildProtectedDataEncryptionKeyAsync(
                clientEncryptionKeyProperties,
                this.ClientEncryptionKeyId,
                cancellationToken);

            return protectedDataEncryptionKey;
        }

        private async Task<ProtectedDataEncryptionKey> BuildProtectedDataEncryptionKeyAsync(
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            string keyId,
            CancellationToken cancellationToken)
        {
            if (await EncryptionCosmosClient.EncryptionKeyCacheSemaphore.WaitAsync(-1, cancellationToken))
            {
                try
                {
                    KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                        clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Name,
                        clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Value,
                        this.encryptionContainer.EncryptionCosmosClient.EncryptionKeyStoreProviderImpl);

                    ProtectedDataEncryptionKey protectedDataEncryptionKey = ProtectedDataEncryptionKey.GetOrCreate(
                        keyId,
                        keyEncryptionKey,
                        clientEncryptionKeyProperties.WrappedDataEncryptionKey);

                    return protectedDataEncryptionKey;
                }
                finally
                {
                    EncryptionCosmosClient.EncryptionKeyCacheSemaphore.Release(1);
                }
            }

            throw new InvalidOperationException("Failed to build ProtectedDataEncryptionKey. ");
        }
    }
}