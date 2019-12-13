//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
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
            this.LinkUri = clientContext.CreateLink(
                parentLink: database.LinkUri.OriginalString,
                uriPathSegment: "keys", // todo: Paths.EncryptionKeysPathSegment,
                id: keyId);

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
        public override async Task<DataEncryptionKeyResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<ResponseMessage> responseMessage = this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            DataEncryptionKeyResponse response = await this.ClientContext.ResponseFactory.CreateDataEncryptionKeyResponseAsync(this, responseMessage);
            this.ClientContext.DekCache.Remove(response.Resource); // todo: do we really have a resource returned here?
            return response;
        }

        /// <inheritdoc/>
        public override async Task<DataEncryptionKeyResponse> ReadAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<ResponseMessage> responseMessage = this.ReadStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            DataEncryptionKeyResponse response = await this.ClientContext.ResponseFactory.CreateDataEncryptionKeyResponseAsync(this, responseMessage);
            this.ClientContext.DekCache.AddOrUpdate(new CachedDekProperties(response.Resource));
            return response;
        }

        public Task<ResponseMessage> ReadStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task<DataEncryptionKeyResponse> RewrapAsync(
           KeyWrapMetadata newWrapMetadata,
           RequestOptions requestOptions = null,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            if (newWrapMetadata == null)
            {
                throw new ArgumentNullException(nameof(newWrapMetadata));
            }

            CachedDekProperties dekProperties = this.ClientContext.DekCache.Get(this.Id);
            if (dekProperties == null)
            {
                dekProperties = new CachedDekProperties(await this.ReadAsync(cancellationToken: cancellationToken));

                // The cache is expected to return a non-null value since the ReadAsync just added it there
                // and this is just a fallback to actual read response. This optimization avoids repeated unwrapping
                // if the operation fails saving to the storage and needs to be retried.
                CachedDekProperties dekProperties2 = this.ClientContext.DekCache.Get(this.Id);
                if (dekProperties2 != null)
                {
                    dekProperties = dekProperties2;
                }
            }

            if (dekProperties.RawDek == null)
            {
                dekProperties.RawDek = await this.ClientContext.ClientOptions.KeyWrapProvider.UnwrapKeyAsync(
                    dekProperties.WrappedDataEncryptionKey,
                    dekProperties.KeyWrapMetadata);
            }

            DataEncryptionKeyProperties newDekProperties = new DataEncryptionKeyProperties(
                dekProperties.Id,
                await DataEncryptionKeyCore.WrapKeyAsync(dekProperties.RawDek, newWrapMetadata, this.ClientContext),
                newWrapMetadata,
                dekProperties.ClientCacheTimeToLive);

            if (requestOptions == null)
            {
                requestOptions = new RequestOptions();
            }

            requestOptions.IfMatchEtag = dekProperties.ETag;

            Task<ResponseMessage> responseMessage = this.ProcessStreamAsync(
                this.ClientContext.PropertiesSerializer.ToStream(newDekProperties),
                OperationType.Replace,
                requestOptions,
                cancellationToken);

            DataEncryptionKeyResponse response = await this.ClientContext.ResponseFactory.CreateDataEncryptionKeyResponseAsync(this, responseMessage);
            this.ClientContext.DekCache.AddOrUpdate(new CachedDekProperties(response.Resource, dekProperties.RawDek));
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
             resourceType: ResourceType.Key, // todo
             operationType: operationType,
             cosmosContainerCore: null,
             partitionKey: null,
             streamPayload: streamPayload,
             requestOptions: requestOptions,
             requestEnricher: null,
             cancellationToken: cancellationToken);
        }

        internal static byte[] GenerateKey()
        {
            byte[] rawDek = new byte[32];
            RNGCryptoServiceProvider cryptoServiceProvider = new RNGCryptoServiceProvider();
            cryptoServiceProvider.GetBytes(rawDek);
            return rawDek;
        }

        internal static async Task<byte[]> WrapKeyAsync(byte[] key, KeyWrapMetadata metadata, CosmosClientContext clientContext)
        {
            IKeyWrapProvider keyWrapProvider = clientContext.ClientOptions.KeyWrapProvider;
            byte[] wrappedKey = await keyWrapProvider.WrapKeyAsync(key, metadata);

            // Verify
            byte[] roundTrippedKey = await keyWrapProvider.UnwrapKeyAsync(wrappedKey, metadata);
            if (!key.SequenceEqual(roundTrippedKey))
            {
                throw new CosmosException(System.Net.HttpStatusCode.BadRequest, ClientResources.KeyWrappingDidNotRoundtrip);
            }

            return wrappedKey;
        }
    }
}
