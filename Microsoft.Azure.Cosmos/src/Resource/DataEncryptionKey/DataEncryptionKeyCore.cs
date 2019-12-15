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
    using Newtonsoft.Json.Linq;

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
            this.ClientContext.DekCache.Remove(this.LinkUri);
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
            this.ClientContext.DekCache.AddOrUpdate(new InMemoryDekProperties(response.Resource));
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

            InMemoryDekProperties inMemoryDekProperties = await this.FetchUnwrappedAsync(cancellationToken);

            (DataEncryptionKeyProperties newDekProperties, TimeSpan cacheTtl) = await this.WrapAsync(inMemoryDekProperties.RawDek, newWrapMetadata);

            if (requestOptions == null)
            {
                requestOptions = new RequestOptions();
            }

            requestOptions.IfMatchEtag = inMemoryDekProperties.ETag;

            Task<ResponseMessage> responseMessage = this.ProcessStreamAsync(
                this.ClientContext.PropertiesSerializer.ToStream(newDekProperties),
                OperationType.Replace,
                requestOptions,
                cancellationToken);

            DataEncryptionKeyResponse response = await this.ClientContext.ResponseFactory.CreateDataEncryptionKeyResponseAsync(this, responseMessage);
            this.ClientContext.DekCache.AddOrUpdate(new InMemoryDekProperties(response.Resource, inMemoryDekProperties.RawDek, cacheTtl));
            return response;
        }

        internal async Task<InMemoryDekProperties> FetchUnwrappedAsync(CancellationToken cancellationToken)
        {
            InMemoryDekProperties inMemoryDekProperties = this.ClientContext.DekCache.Get(this.LinkUri);

            if (inMemoryDekProperties == null || inMemoryDekProperties.RawDek == null)
            {
                DataEncryptionKeyProperties dekProperties = null;
                if (inMemoryDekProperties == null)
                {
                    dekProperties = await this.ReadAsync(cancellationToken: cancellationToken);
                }
                else
                {
                    dekProperties = inMemoryDekProperties;
                }

                KeyUnwrapResponse unwrapResponse = await this.ClientContext.ClientOptions.KeyWrapProvider.UnwrapKeyAsync(
                                dekProperties.WrappedDataEncryptionKey,
                                dekProperties.KeyWrapMetadata);

                inMemoryDekProperties = new InMemoryDekProperties(dekProperties, unwrapResponse.DataEncryptionKey, unwrapResponse.ClientCacheTimeToLive);
                this.ClientContext.DekCache.AddOrUpdate(inMemoryDekProperties);
            }

            return inMemoryDekProperties;
        }

        internal static byte[] GenerateKey()
        {
            byte[] rawDek = new byte[32];
            RNGCryptoServiceProvider cryptoServiceProvider = new RNGCryptoServiceProvider();
            cryptoServiceProvider.GetBytes(rawDek);
            return rawDek;
        }

        internal byte[] Encrypt(byte[] input)
        {
            using (AesManaged myAes = new AesManaged())
            {
                using (SHA256Managed sha256 = new SHA256Managed())
                {
                    using (ICryptoTransform transform = myAes.CreateEncryptor())
                    {
                        transform.TransformFinalBlock(input, 0, input.Length);
                    }
                }
            }

            return null; // todo
        }

        internal async Task<(DataEncryptionKeyProperties, TimeSpan)> WrapAsync(byte[] key, KeyWrapMetadata metadata)
        {
            KeyWrapProvider keyWrapProvider = this.ClientContext.ClientOptions.KeyWrapProvider;
            KeyWrapResponse keyWrapResponse = await keyWrapProvider.WrapKeyAsync(key, metadata);
            DataEncryptionKeyProperties dekProperties = new DataEncryptionKeyProperties(this.Id, keyWrapResponse.WrappedDataEncryptionKey, keyWrapResponse.KeyWrapMetadata);

            // Verify
            KeyUnwrapResponse roundTripResponse = await keyWrapProvider.UnwrapKeyAsync(keyWrapResponse.WrappedDataEncryptionKey, keyWrapResponse.KeyWrapMetadata);
            if (!roundTripResponse.DataEncryptionKey.SequenceEqual(key))
            {
                throw new CosmosException(System.Net.HttpStatusCode.BadRequest, ClientResources.KeyWrappingDidNotRoundtrip);
            }

            return (dekProperties, roundTripResponse.ClientCacheTimeToLive);
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
    }
}
