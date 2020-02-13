//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Provides operations for reading, re-wrapping, or deleting a specific data encryption key by Id.
    /// See <see cref="Cosmos.Database"/> for operations to create a data encryption key.
    /// </summary>
    internal class DataEncryptionKeyCore : DataEncryptionKey
    {
        /// <summary>
        /// Only used for unit testing
        /// </summary>
        internal DataEncryptionKeyCore()
        {
        }

        internal DataEncryptionKeyCore(
            CosmosClientContext clientContext,
            DatabaseCore database,
            string keyId)
        {
            this.Id = keyId;
            this.ClientContext = clientContext;
            this.LinkUri = DataEncryptionKeyCore.CreateLinkUri(clientContext, database, keyId);

            this.Database = database;
        }

        /// <inheritdoc/>
        public override string Id { get; }

        /// <summary>
        /// Returns a reference to a database object that contains this encryption key. 
        /// </summary>
        public Database Database { get; }

        internal virtual Uri LinkUri { get; }

        internal virtual CosmosClientContext ClientContext { get; }

        /// <inheritdoc/>
        public override async Task<DataEncryptionKeyResponse> ReadAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            DataEncryptionKeyResponse response = await this.ReadInternalAsync(requestOptions, cancellationToken);
            this.ClientContext.DekCache.Set(this.Database.Id, this.LinkUri, response.Resource);
            return response;
        }

        /// <inheritdoc/>
        public override async Task<DataEncryptionKeyResponse> RewrapAsync(
           EncryptionKeyWrapMetadata newWrapMetadata,
           RequestOptions requestOptions = null,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            if (newWrapMetadata == null)
            {
                throw new ArgumentNullException(nameof(newWrapMetadata));
            }

            (DataEncryptionKeyProperties dekProperties, InMemoryRawDek inMemoryRawDek) = await this.FetchUnwrappedAsync(cancellationToken);

            (byte[] wrappedDek, EncryptionKeyWrapMetadata updatedMetadata, InMemoryRawDek updatedRawDek) =
                await this.WrapAsync(inMemoryRawDek.RawDek, dekProperties.EncryptionAlgorithmId, newWrapMetadata, cancellationToken);

            if (requestOptions == null)
            {
                requestOptions = new RequestOptions();
            }

            requestOptions.IfMatchEtag = dekProperties.ETag;

            DataEncryptionKeyProperties newDekProperties = new DataEncryptionKeyProperties(dekProperties);
            newDekProperties.WrappedDataEncryptionKey = wrappedDek;
            newDekProperties.EncryptionKeyWrapMetadata = updatedMetadata;

            Task<ResponseMessage> responseMessage = this.ProcessStreamAsync(
                this.ClientContext.SerializerCore.ToStream(newDekProperties),
                OperationType.Replace,
                requestOptions,
                cancellationToken);

            DataEncryptionKeyResponse response = await this.ClientContext.ResponseFactory.CreateDataEncryptionKeyResponseAsync(this, responseMessage);
            this.ClientContext.DekCache.Set(this.Database.Id, this.LinkUri, response.Resource);
            this.ClientContext.DekCache.SetRawDek(response.Resource.ResourceId, updatedRawDek);
            return response;
        }

        internal static Uri CreateLinkUri(CosmosClientContext clientContext, DatabaseCore database, string keyId)
        {
            return clientContext.CreateLink(
                parentLink: database.LinkUri.OriginalString,
                uriPathSegment: Paths.ClientEncryptionKeysPathSegment,
                id: keyId);
        }

        internal async Task<(DataEncryptionKeyProperties, InMemoryRawDek)> FetchUnwrappedAsync(CancellationToken cancellationToken)
        {
            DataEncryptionKeyProperties dekProperties = null;

            try
            {
                dekProperties = await this.ClientContext.DekCache.GetOrAddByNameLinkUriAsync(this.LinkUri, this.Database.Id, this.ReadResourceAsync, cancellationToken);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new CosmosException(HttpStatusCode.NotFound, ClientResources.DataEncryptionKeyNotFound, inner: ex);
            }

            InMemoryRawDek inMemoryRawDek = await this.ClientContext.DekCache.GetOrAddRawDekAsync(dekProperties, this.UnwrapAsync, cancellationToken);
            return (dekProperties, inMemoryRawDek);
        }

        internal async Task<(DataEncryptionKeyProperties, InMemoryRawDek)> FetchUnwrappedByRidAsync(string rid, CancellationToken cancellationToken)
        {
            string dekRidSelfLink = PathsHelper.GeneratePath(ResourceType.ClientEncryptionKey, rid, isFeed: false);
            // Server self links end with / but client generate links don't - match them.
            if (!dekRidSelfLink.EndsWith("/"))
            {
                dekRidSelfLink += "/";
            }

            DataEncryptionKeyProperties dekProperties = null;
            try
            {
                dekProperties = await this.ClientContext.DekCache.GetOrAddByRidSelfLinkAsync(
                    dekRidSelfLink,
                    this.Database.Id,
                    this.ReadResourceByRidSelfLinkAsync,
                    this.LinkUri,
                    cancellationToken);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new CosmosException(HttpStatusCode.NotFound, ClientResources.DataEncryptionKeyNotFound, inner: ex);
            }

            InMemoryRawDek inMemoryRawDek = await this.ClientContext.DekCache.GetOrAddRawDekAsync(dekProperties, this.UnwrapAsync, cancellationToken);
            return (dekProperties, inMemoryRawDek);
        }

        internal virtual EncryptionAlgorithm GetEncryptionAlgorithm(byte[] rawDek, CosmosEncryptionAlgorithm encryptionAlgorithmId)
        {
            Debug.Assert(encryptionAlgorithmId == CosmosEncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_256_RANDOMIZED, "Unexpected encryption algorithm id");
            AeadAes256CbcHmac256EncryptionKey key = new AeadAes256CbcHmac256EncryptionKey(rawDek, AeadAes256CbcHmac256Algorithm.AlgorithmNameConstant);
            return new AeadAes256CbcHmac256Algorithm(key, EncryptionType.Randomized, algorithmVersion: 1);
        }

        internal virtual byte[] GenerateKey(CosmosEncryptionAlgorithm encryptionAlgorithmId)
        {
            Debug.Assert(encryptionAlgorithmId == CosmosEncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_256_RANDOMIZED, "Unexpected encryption algorithm id");
            byte[] rawDek = new byte[32];
            SecurityUtility.GenerateRandomBytes(rawDek);
            return rawDek;
        }

        internal async Task<(byte[], EncryptionKeyWrapMetadata, InMemoryRawDek)> WrapAsync(
            byte[] key,
            CosmosEncryptionAlgorithm encryptionAlgorithmId,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            EncryptionSettings encryptionSettings = this.ClientContext.ClientOptions.EncryptionSettings;
            if (encryptionSettings == null)
            {
                throw new ArgumentException(ClientResources.EncryptionSettingsNotConfigured);
            }

            EncryptionKeyWrapResult keyWrapResponse = await encryptionSettings.EncryptionKeyWrapProvider.WrapKeyAsync(key, metadata, cancellationToken);

            // Verify
            DataEncryptionKeyProperties tempDekProperties = new DataEncryptionKeyProperties(this.Id, encryptionAlgorithmId, keyWrapResponse.WrappedDataEncryptionKey, keyWrapResponse.EncryptionKeyWrapMetadata);
            InMemoryRawDek roundTripResponse = await this.UnwrapAsync(tempDekProperties, cancellationToken);
            if (!roundTripResponse.RawDek.SequenceEqual(key))
            {
                throw new CosmosException(HttpStatusCode.BadRequest, ClientResources.KeyWrappingDidNotRoundtrip);
            }

            return (keyWrapResponse.WrappedDataEncryptionKey, keyWrapResponse.EncryptionKeyWrapMetadata, roundTripResponse);
        }

        internal async Task<InMemoryRawDek> UnwrapAsync(
            DataEncryptionKeyProperties dekProperties,
            CancellationToken cancellationToken)
        {
            EncryptionSettings encryptionSettings = this.ClientContext.ClientOptions.EncryptionSettings;
            if (encryptionSettings == null)
            {
                throw new ArgumentException(ClientResources.EncryptionSettingsNotConfigured);
            }

            EncryptionKeyUnwrapResult unwrapResult = await encryptionSettings.EncryptionKeyWrapProvider.UnwrapKeyAsync(
                    dekProperties.WrappedDataEncryptionKey,
                    dekProperties.EncryptionKeyWrapMetadata,
                    cancellationToken);

            EncryptionAlgorithm encryptionAlgorithm = this.GetEncryptionAlgorithm(unwrapResult.DataEncryptionKey, dekProperties.EncryptionAlgorithmId);

            return new InMemoryRawDek(unwrapResult.DataEncryptionKey, encryptionAlgorithm, unwrapResult.ClientCacheTimeToLive);
        }

        private async Task<DataEncryptionKeyProperties> ReadResourceByRidSelfLinkAsync(
            string ridSelfLink,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<ResponseMessage> responseMessage = this.ClientContext.ProcessResourceOperationStreamAsync(
             resourceUri: new Uri(ridSelfLink, UriKind.Relative),
             resourceType: ResourceType.ClientEncryptionKey,
             operationType: OperationType.Read,
             cosmosContainerCore: null,
             partitionKey: null,
             streamPayload: null,
             requestOptions: null,
             requestEnricher: null,
             diagnosticsScope: null,
             cancellationToken: cancellationToken);

            DataEncryptionKeyResponse response = await this.ClientContext.ResponseFactory.CreateDataEncryptionKeyResponseAsync(this, responseMessage);
            return response;
        }

        private async Task<DataEncryptionKeyProperties> ReadResourceAsync(CancellationToken cancellationToken)
        {
            return (await this.ReadInternalAsync(null, cancellationToken)).Resource;
        }

        private async Task<DataEncryptionKeyResponse> ReadInternalAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Task<ResponseMessage> responseMessage = this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            DataEncryptionKeyResponse response = await this.ClientContext.ResponseFactory.CreateDataEncryptionKeyResponseAsync(this, responseMessage);
            return response;
        }

        private Task<ResponseMessage> ProcessStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
             resourceUri: this.LinkUri,
             resourceType: ResourceType.ClientEncryptionKey,
             operationType: operationType,
             cosmosContainerCore: null,
             partitionKey: null,
             streamPayload: streamPayload,
             requestOptions: requestOptions,
             requestEnricher: null,
             diagnosticsScope: null,
             cancellationToken: cancellationToken);
        }
    }
}
