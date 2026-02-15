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

        // Test-only hook: when provided, BuildEncryptionAlgorithmForSettingAsync returns this instance
        // instead of constructing one via key fetching/unwrapping. This is internal and used only by tests
        // through InternalsVisibleTo.
        private readonly Microsoft.Data.Encryption.Cryptography.AeadAes256CbcHmac256EncryptionAlgorithm injectedAlgorithm;

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

        // Internal constructor for tests to inject a ready algorithm to enable end-to-end unit testing
        // without standing up key providers. Other parameters remain for traceability but are not used
        // when an injected algorithm is supplied.
        internal EncryptionSettingForProperty(
            string clientEncryptionKeyId,
            Data.Encryption.Cryptography.EncryptionType encryptionType,
            EncryptionContainer encryptionContainer,
            string databaseRid,
            AeadAes256CbcHmac256EncryptionAlgorithm injectedAlgorithm)
            : this(clientEncryptionKeyId, encryptionType, encryptionContainer, databaseRid)
        {
            this.injectedAlgorithm = injectedAlgorithm ?? throw new ArgumentNullException(nameof(injectedAlgorithm));
        }

        public string ClientEncryptionKeyId { get; }

        public Data.Encryption.Cryptography.EncryptionType EncryptionType { get; }

        public async Task<AeadAes256CbcHmac256EncryptionAlgorithm> BuildEncryptionAlgorithmForSettingAsync(CancellationToken cancellationToken)
        {
            // Return the injected algorithm if provided (test-only path)
            if (this.injectedAlgorithm != null)
            {
                return this.injectedAlgorithm;
            }

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

        /// <summary>
        /// Builds a <see cref="ProtectedDataEncryptionKey"/> using a double-checked locking pattern.
        ///
        /// Fast path (cache hit): The SDK-side shadow cache is checked first WITHOUT acquiring the
        /// global semaphore. On hit, the PDEK is returned immediately — no contention.
        ///
        /// Slow path (cache miss): The semaphore is acquired to prevent a cache stampede (multiple
        /// threads calling Key Vault for the same key simultaneously). After acquiring, the shadow
        /// cache is re-checked (another thread may have populated it while we waited), then MDE's
        /// GetOrCreate is called which may trigger a sync Key Vault HTTP call on miss.
        /// </summary>
        private async Task<ProtectedDataEncryptionKey> BuildProtectedDataEncryptionKeyAsync(
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            string keyId,
            CancellationToken cancellationToken)
        {
            // Build a cache key that matches the granularity of MDE's PDEK cache:
            //   Tuple(name, keyEncryptionKey, encryptedKey.ToHexString())
            // We use a string composite of keyId + KEK path + wrapped DEK hex.
            // Key rewraps change the wrapped DEK bytes, producing a new cache key automatically.
            string wrappedKeyHex = clientEncryptionKeyProperties.WrappedDataEncryptionKey.ToHexString();
            string cacheKey = keyId
                + "/"
                + clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Value
                + "/"
                + wrappedKeyHex;

            // Fast path: check SDK-side shadow cache WITHOUT the semaphore.
            // On hit, the semaphore is never touched — zero contention for 99%+ of calls.
            if (EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.TryGetValue(cacheKey, out ProtectedDataEncryptionKeyCacheEntry cacheEntry)
                && !cacheEntry.IsExpired)
            {
                return cacheEntry.ProtectedDataEncryptionKey;
            }

            // Slow path: cache miss — acquire semaphore to prevent stampede.
            await EncryptionCosmosClient.EncryptionKeyCacheSemaphore
                .WaitAsync(-1, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                // Double-check: another thread may have populated the cache while we waited.
                if (EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.TryGetValue(cacheKey, out cacheEntry)
                    && !cacheEntry.IsExpired)
                {
                    return cacheEntry.ProtectedDataEncryptionKey;
                }

                KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                    clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Name,
                    clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Value,
                    this.encryptionContainer.EncryptionCosmosClient.EncryptionKeyStoreProviderImpl);

                ProtectedDataEncryptionKey protectedDataEncryptionKey = ProtectedDataEncryptionKey.GetOrCreate(
                    keyId,
                    keyEncryptionKey,
                    clientEncryptionKeyProperties.WrappedDataEncryptionKey);

                // Populate shadow cache with the result.
                EncryptionCosmosClient.ProtectedDataEncryptionKeyCache[cacheKey] =
                    new ProtectedDataEncryptionKeyCacheEntry(protectedDataEncryptionKey);

                return protectedDataEncryptionKey;
            }
            finally
            {
                EncryptionCosmosClient.EncryptionKeyCacheSemaphore.Release(1);
            }
        }
    }
}