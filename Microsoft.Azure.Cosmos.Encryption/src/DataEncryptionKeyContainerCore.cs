//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.AAP_PH.Cryptography;

    internal class DataEncryptionKeyContainerCore : DataEncryptionKeyContainer
    {
        internal CosmosDataEncryptionKeyProvider DekProvider { get; }

        public DataEncryptionKeyContainerCore(CosmosDataEncryptionKeyProvider dekProvider)
        {
            this.DekProvider = dekProvider;
        }

        public override FeedIterator<T> GetDataEncryptionKeyQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.DekProvider.Container.GetItemQueryIterator<T>(queryText, continuationToken, requestOptions);
        }

        public override FeedIterator<T> GetDataEncryptionKeyQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.DekProvider.Container.GetItemQueryIterator<T>(queryDefinition, continuationToken, requestOptions);
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

            if (encryptionAlgorithm != CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized &&
                encryptionAlgorithm != CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized)
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

            DataEncryptionKeyProperties dekProperties = new DataEncryptionKeyProperties(
                id,
                encryptionAlgorithm,
                wrappedDek,
                updatedMetadata,
                DateTime.UtcNow);

            ItemResponse<DataEncryptionKeyProperties> dekResponse = await this.DekProvider.Container.CreateItemAsync(dekProperties, new PartitionKey(dekProperties.Id), cancellationToken: cancellationToken);
            this.DekProvider.DekCache.SetDekProperties(id, dekResponse.Resource);

            if (encryptionAlgorithm == CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized)
            {
                this.DekProvider.DekCache.SetRawDek(id, inMemoryRawDek);
            }

            return dekResponse;
        }

        /// <inheritdoc/>
        public override async Task<ItemResponse<DataEncryptionKeyProperties>> ReadDataEncryptionKeyAsync(
            string id,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
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
           CancellationToken cancellationToken = default)
        {
            if (newWrapMetadata == null)
            {
                throw new ArgumentNullException(nameof(newWrapMetadata));
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);

            DataEncryptionKeyProperties dekProperties = await this.FetchDataEncryptionKeyPropertiesAsync(
                id,
                diagnosticsContext,
                cancellationToken);

            InMemoryRawDek inMemoryRawDek = await this.FetchUnwrappedAsync(
                dekProperties,
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

            DataEncryptionKeyProperties newDekProperties = new DataEncryptionKeyProperties(dekProperties)
            {
                WrappedDataEncryptionKey = wrappedDek,
                EncryptionKeyWrapMetadata = updatedMetadata,
            };

            ItemResponse<DataEncryptionKeyProperties> response;

            try
            {
                response = await this.DekProvider.Container.ReplaceItemAsync(
                    newDekProperties,
                    newDekProperties.Id,
                    new PartitionKey(newDekProperties.Id),
                    requestOptions,
                    cancellationToken);

                Debug.Assert(response.Resource != null);
            }
            catch (CosmosException ex)
            {
                if (!ex.StatusCode.Equals(HttpStatusCode.PreconditionFailed))
                {
                    throw;
                }

                // Handle if exception is due to etag mismatch. The scenario is as follows - say there are 2 clients A and B that both have the DEK properties cached.
                // From A, rewrap worked and the DEK is updated. Now from B, rewrap was attempted later based on the cached properties which will fail due to etag mismatch.
                // To address this, we do an explicit read, which reads the key from storage and updates the cached properties; and then attempt rewrap again.
                await this.ReadDataEncryptionKeyAsync(
                    newDekProperties.Id,
                    requestOptions,
                    cancellationToken);

                return await this.RewrapDataEncryptionKeyAsync(
                    id,
                    newWrapMetadata,
                    requestOptions,
                    cancellationToken);
            }

            this.DekProvider.DekCache.SetDekProperties(id, response.Resource);

            if (dekProperties.EncryptionAlgorithm == CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized)
            {
                this.DekProvider.DekCache.SetRawDek(id, updatedRawDek);
            }

            return response;
        }

        internal async Task<DataEncryptionKeyProperties> FetchDataEncryptionKeyPropertiesAsync(
            string id,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            try
            {
                DataEncryptionKeyProperties dekProperties = await this.DekProvider.DekCache.GetOrAddDekPropertiesAsync(
                    id,
                    this.ReadResourceAsync,
                    diagnosticsContext,
                    cancellationToken);

                return dekProperties;
            }
            catch (CosmosException exception)
            {
                throw EncryptionExceptionFactory.EncryptionKeyNotFoundException(
                    $"Failed to retrieve Data Encryption Key with id: '{id}'.",
                    exception);
            }
        }

        internal async Task<InMemoryRawDek> FetchUnwrappedAsync(
            DataEncryptionKeyProperties dekProperties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            try
            {
                if (dekProperties.EncryptionAlgorithm == CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized)
                {
                    return await this.UnwrapAsync(
                        dekProperties,
                        diagnosticsContext,
                        cancellationToken);
                }

                return await this.DekProvider.DekCache.GetOrAddRawDekAsync(
                    dekProperties,
                    this.UnwrapAsync,
                    diagnosticsContext,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                throw EncryptionExceptionFactory.EncryptionKeyNotFoundException(
                    $"Failed to unwrap Data Encryption Key with id: '{dekProperties.Id}'.",
                    exception);
            }
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

            DataEncryptionKey dek = null;

            if (dekProperties.EncryptionAlgorithm == CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized)
            {
                if (this.DekProvider.EncryptionKeyWrapProvider is AapKeyWrapProvider aapKeyWrapProvider)
                {
                    throw new InvalidOperationException($"For use of '{CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized}' algorithm, " +
                        $"{nameof(this.DekProvider)} needs to be initialized with {nameof(EncryptionKeyWrapProvider)}.");
                }

                dek = DataEncryptionKey.Create(
                    unwrapResult.DataEncryptionKey,
                    dekProperties.EncryptionAlgorithm);
            }
            else if (dekProperties.EncryptionAlgorithm == CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized)
            {
                if (!(this.DekProvider.EncryptionKeyWrapProvider is AapKeyWrapProvider aapKeyWrapProvider))
                {
                    throw new InvalidOperationException($"For use of '{CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized}' algorithm, " +
                        $"{nameof(this.DekProvider)} needs to be initialized with {nameof(EncryptionKeyStoreProvider)}.");
                }

                dek = new AapEncryptionAlgorithm(
                    dekProperties,
                    unwrapResult.DataEncryptionKey,
                    Data.AAP_PH.Cryptography.EncryptionType.Randomized,
                    aapKeyWrapProvider.EncryptionKeyStoreProvider);
            }

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
            ItemRequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return await this.DekProvider.Container.ReadItemAsync<DataEncryptionKeyProperties>(
                id,
                new PartitionKey(id),
                requestOptions,
                cancellationToken);
        }
    }
}
