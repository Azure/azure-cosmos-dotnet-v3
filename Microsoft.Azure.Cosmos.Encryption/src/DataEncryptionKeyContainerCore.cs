//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DataEncryptionKeyContainerCore : DataEncryptionKeyContainer
    {
        internal CosmosDataEncryptionKeyProvider DekProvider { get; }

        public DataEncryptionKeyContainerCore(CosmosDataEncryptionKeyProvider dekProvider)
        {
            this.DekProvider = dekProvider;
        }

        public override FeedIterator<DataEncryptionKeyProperties> GetDataEncryptionKeyIterator(
                string startId = null,
                string endId = null,
                bool isDescending = false,
                string continuationToken = null,
                QueryRequestOptions requestOptions = null)
        {
            return null; // todo
            //if (!(this.GetDataEncryptionKeyStreamIterator(
            //    startId,
            //    endId,
            //    isDescending,
            //    continuationToken,
            //    requestOptions) is FeedIteratorInternal dekStreamIterator))
            //{
            //    throw new InvalidOperationException($"Expected FeedIteratorInternal.");
            //}

            //return new FeedIteratorCore<DataEncryptionKeyProperties>(
            //    dekStreamIterator,
            //    (responseMessage) =>
            //    {
            //        FeedResponse<DataEncryptionKeyProperties> results = this.ClientContext.ResponseFactory.CreateQueryFeedResponse<DataEncryptionKeyProperties>(responseMessage, ResourceType.ClientEncryptionKey);
            //        foreach (DataEncryptionKeyProperties result in results)
            //        {
            //            Uri dekUri = DataEncryptionKeyCore.CreateLinkUri(this.ClientContext, this, result.Id);
            //            this.ClientContext.DekCache.Set(this.Id, dekUri, result);
            //        }

            //        return results;
            //    });
        }

        internal FeedIterator GetDataEncryptionKeyStreamIterator(
            string startId = null,
            string endId = null,
            bool isDescending = false,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return null; // todo
            //if (startId != null || endId != null)
            //{
            //    if (requestOptions == null)
            //    {
            //        requestOptions = new QueryRequestOptions();
            //    }

            //    requestOptions.StartId = startId;
            //    requestOptions.EndId = endId;
            //    requestOptions.EnumerationDirection = isDescending ? EnumerationDirection.Reverse : EnumerationDirection.Forward;
            //}

            //return FeedIteratorCore.CreateForNonPartitionedResource(
            //   clientContext: this.ClientContext,
            //   resourceLink: this.LinkUri,
            //   resourceType: ResourceType.ClientEncryptionKey,
            //   queryDefinition: null,
            //   continuationToken: continuationToken,
            //   options: requestOptions);
        }

        public override async Task<ItemResponse<DataEncryptionKeyProperties>> CreateDataEncryptionKeyAsync(
                string id,
                string encryptionAlgorithm,
                EncryptionKeyWrapMetadata encryptionKeyWrapMetadata,
                ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (encryptionAlgorithm != CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized)
            {
                throw new ArgumentException(string.Format("Unsupported Encryption Algorithm {0}", encryptionAlgorithm), nameof(encryptionAlgorithm));
            }

            if (encryptionKeyWrapMetadata == null)
            {
                throw new ArgumentNullException(nameof(encryptionKeyWrapMetadata));
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);

            byte[] rawDek = DataEncryptionKey.Generate(encryptionAlgorithm);

            (byte[] wrappedDek, EncryptionKeyWrapMetadata updatedMetadata, InMemoryRawDek inMemoryRawDek) = await this.WrapAsync(
                id,
                rawDek,
                encryptionAlgorithm,
                encryptionKeyWrapMetadata,
                diagnosticsContext,
                cancellationToken);

            DataEncryptionKeyProperties dekProperties = new DataEncryptionKeyProperties(id, encryptionAlgorithm, wrappedDek, updatedMetadata, DateTime.UtcNow);

            ItemResponse<DataEncryptionKeyProperties> dekResponse = await this.DekProvider.Container.CreateItemAsync(dekProperties, new PartitionKey(dekProperties.Id), cancellationToken: cancellationToken);
            this.DekProvider.DekCache.SetDekProperties(id, dekResponse.Resource);
            this.DekProvider.DekCache.SetRawDek(id, inMemoryRawDek);
            return dekResponse;
        }


        /// <inheritdoc/>
        public override async Task<ItemResponse<DataEncryptionKeyProperties>> ReadDataEncryptionKeyAsync(
            string id,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ItemResponse<DataEncryptionKeyProperties> response = await this.ReadInternalAsync(
                id,
                requestOptions,
                diagnosticsContext: null,
                cancellationToken: cancellationToken);

            this.DekProvider.DekCache.SetDekProperties(id, response.Resource);
            return response;
        }

        /// <inheritdoc/>
        public override async Task<ItemResponse<DataEncryptionKeyProperties>> RewrapDataEncryptionKeyAsync(
           string id,
           EncryptionKeyWrapMetadata newWrapMetadata,
           ItemRequestOptions requestOptions = null,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            if (newWrapMetadata == null)
            {
                throw new ArgumentNullException(nameof(newWrapMetadata));
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);

            (DataEncryptionKeyProperties dekProperties, InMemoryRawDek inMemoryRawDek) = await this.FetchUnwrappedAsync(
                id,
                diagnosticsContext,
                cancellationToken);

            (byte[] wrappedDek, EncryptionKeyWrapMetadata updatedMetadata, InMemoryRawDek updatedRawDek) = await this.WrapAsync(
                    id,
                    inMemoryRawDek.DataEncryptionKey.RawKey,
                    dekProperties.EncryptionAlgorithm,
                    newWrapMetadata,
                    diagnosticsContext,
                    cancellationToken);

            if (requestOptions == null)
            {
                requestOptions = new ItemRequestOptions();
            }

            requestOptions.IfMatchEtag = dekProperties.ETag;

            DataEncryptionKeyProperties newDekProperties = new DataEncryptionKeyProperties(dekProperties);
            newDekProperties.WrappedDataEncryptionKey = wrappedDek;
            newDekProperties.EncryptionKeyWrapMetadata = updatedMetadata;

            ItemResponse<DataEncryptionKeyProperties> response = await this.DekProvider.Container.ReplaceItemAsync(
                newDekProperties,
                newDekProperties.Id,
                new PartitionKey(newDekProperties.Id),
                requestOptions,
                cancellationToken);

            Debug.Assert(response.Resource != null);

            this.DekProvider.DekCache.SetDekProperties(id, response.Resource);
            this.DekProvider.DekCache.SetRawDek(id, updatedRawDek);
            return response;
        }

        internal async Task<(DataEncryptionKeyProperties, InMemoryRawDek)> FetchUnwrappedAsync(
            string id,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            DataEncryptionKeyProperties dekProperties = await this.DekProvider.DekCache.GetOrAddDekPropertiesAsync(
                    id,
                    this.ReadResourceAsync,
                    diagnosticsContext,
                    cancellationToken);

            InMemoryRawDek inMemoryRawDek = await this.DekProvider.DekCache.GetOrAddRawDekAsync(
                dekProperties,
                this.UnwrapAsync,
                diagnosticsContext,
                cancellationToken);

            return (dekProperties, inMemoryRawDek);
        }

        internal async Task<(byte[], EncryptionKeyWrapMetadata, InMemoryRawDek)> WrapAsync(
            string id,
            byte[] key,
            string encryptionAlgorithm,
            EncryptionKeyWrapMetadata metadata,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            EncryptionKeyWrapResult keyWrapResponse;
            using (diagnosticsContext.CreateScope("WrapDataEncryptionKey"))
            {
                keyWrapResponse = await this.DekProvider.EncryptionKeyWrapProvider.WrapKeyAsync(key, metadata, cancellationToken);
            }

            // Verify
            DataEncryptionKeyProperties tempDekProperties = new DataEncryptionKeyProperties(id, encryptionAlgorithm, keyWrapResponse.WrappedDataEncryptionKey, keyWrapResponse.EncryptionKeyWrapMetadata, DateTime.UtcNow);
            InMemoryRawDek roundTripResponse = await this.UnwrapAsync(tempDekProperties, diagnosticsContext, cancellationToken);
            if (!roundTripResponse.DataEncryptionKey.RawKey.SequenceEqual(key))
            {
                throw new InvalidOperationException("The key wrapping provider configured was unable to unwrap the wrapped key correctly.");
            }

            return (keyWrapResponse.WrappedDataEncryptionKey, keyWrapResponse.EncryptionKeyWrapMetadata, roundTripResponse);
        }

        internal async Task<InMemoryRawDek> UnwrapAsync(
            DataEncryptionKeyProperties dekProperties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            EncryptionKeyUnwrapResult unwrapResult;
            using (diagnosticsContext.CreateScope("UnwrapDataEncryptionKey"))
            {
                unwrapResult = await this.DekProvider.EncryptionKeyWrapProvider.UnwrapKeyAsync(
                        dekProperties.WrappedDataEncryptionKey,
                        dekProperties.EncryptionKeyWrapMetadata,
                        cancellationToken);
            }

            DataEncryptionKey dek = DataEncryptionKey.Create(unwrapResult.DataEncryptionKey, dekProperties.EncryptionAlgorithm);

            return new InMemoryRawDek(dek, unwrapResult.ClientCacheTimeToLive);
        }

        private async Task<DataEncryptionKeyProperties> ReadResourceAsync(
            string id,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            using (diagnosticsContext.CreateScope("ReadDataEncryptionKey"))
            {
                return await this.ReadInternalAsync(
                    id: id,
                    requestOptions: null,
                    diagnosticsContext: diagnosticsContext,
                    cancellationToken: cancellationToken);
            }
        }

        private async Task<ItemResponse<DataEncryptionKeyProperties>> ReadInternalAsync(
            string id,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return await this.DekProvider.Container.ReadItemAsync<DataEncryptionKeyProperties>(
                id,
                new PartitionKey(id),
                cancellationToken: cancellationToken);
        }
    }
}
