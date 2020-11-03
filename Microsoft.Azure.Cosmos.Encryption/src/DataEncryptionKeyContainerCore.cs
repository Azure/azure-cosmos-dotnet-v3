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
    using Microsoft.Data.Encryption.Cryptography;

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

            if (!CosmosEncryptionAlgorithm.VerifyIfSupportedAlgorithm(encryptionAlgorithm))
            {
                throw new ArgumentException(string.Format("Unsupported Encryption Algorithm {0}", encryptionAlgorithm), nameof(encryptionAlgorithm));
            }

            if (encryptionKeyWrapMetadata == null)
            {
                throw new ArgumentNullException(nameof(encryptionKeyWrapMetadata));
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);

            byte[] wrappedDek = null;
            EncryptionKeyWrapMetadata updatedMetadata = null;
            InMemoryRawDek inMemoryRawDek = null;

            if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized))
            {
                (wrappedDek, updatedMetadata, inMemoryRawDek) = await this.GenerateAndWrapRawDekForLegacyEncAlgoAsync(
                id,
                encryptionAlgorithm,
                encryptionKeyWrapMetadata,
                diagnosticsContext,
                cancellationToken);
            }
            else if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized))
            {
                (wrappedDek, updatedMetadata) = this.GenerateAndWrapPdekForMdeEncAlgo(encryptionKeyWrapMetadata);
            }

            DataEncryptionKeyProperties dekProperties = new DataEncryptionKeyProperties(
                    id,
                    encryptionAlgorithm,
                    wrappedDek,
                    updatedMetadata,
                    DateTime.UtcNow);

            ItemResponse<DataEncryptionKeyProperties> dekResponse = await this.DekProvider.Container.CreateItemAsync(
                dekProperties,
                new PartitionKey(dekProperties.Id),
                cancellationToken: cancellationToken);

            this.DekProvider.DekCache.SetDekProperties(id, dekResponse.Resource);

            if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized))
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
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            ItemResponse<DataEncryptionKeyProperties> response = await this.ReadInternalAsync(
                id,
                requestOptions,
                diagnosticsContext: diagnosticsContext,
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

            byte[] rawkey = null;

            if (string.Equals(dekProperties.EncryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized))
            {
                InMemoryRawDek inMemoryRawDek = await this.FetchUnwrappedAsync(
                    dekProperties,
                    dekProperties.EncryptionAlgorithm,
                    diagnosticsContext,
                    cancellationToken);

                rawkey = inMemoryRawDek.DataEncryptionKey.RawKey;
            }
            else if (string.Equals(dekProperties.EncryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized))
            {
                EncryptionKeyUnwrapResult encryptionKeyUnwrapResult = await this.DekProvider.MdeKeyWrapProvider.UnwrapKeyAsync(
                    dekProperties.WrappedDataEncryptionKey,
                    dekProperties.EncryptionKeyWrapMetadata,
                    cancellationToken);

                rawkey = encryptionKeyUnwrapResult.DataEncryptionKey;
            }

            (byte[] wrappedDek, EncryptionKeyWrapMetadata updatedMetadata, InMemoryRawDek updatedRawDek) = await this.WrapAsync(
                id,
                rawkey,
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

            if (string.Equals(dekProperties.EncryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized))
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
            string encryptionAlgorithm,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            try
            {
                if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized))
                {
                    return await this.UnwrapAsync(
                        dekProperties,
                        encryptionAlgorithm,
                        diagnosticsContext,
                        cancellationToken);
                }

                return await this.DekProvider.DekCache.GetOrAddRawDekAsync(
                    dekProperties,
                    this.UnwrapAsync,
                    encryptionAlgorithm,
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
            EncryptionKeyWrapResult keyWrapResponse = null;
            using (diagnosticsContext.CreateScope("WrapDataEncryptionKey"))
            {
                keyWrapResponse = string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized) && this.DekProvider.EncryptionKeyWrapProvider != null
                    ? await this.DekProvider.EncryptionKeyWrapProvider.WrapKeyAsync(key, metadata, cancellationToken)
                    : string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized) && this.DekProvider.MdeKeyWrapProvider != null
                        ? await this.DekProvider.MdeKeyWrapProvider.WrapKeyAsync(key, metadata, cancellationToken)
                        : throw new ArgumentException(string.Format(
                            "Unsupported encryption algorithm {0}." +
                            " Please initialize the Encryptor or CosmosDataEncryptionKeyProvider with an implementation of the EncryptionKeyStoreProvider / EncryptionKeyWrapProvider.",
                            encryptionAlgorithm));
            }

            // Verify
            DataEncryptionKeyProperties tempDekProperties = new DataEncryptionKeyProperties(
                id,
                encryptionAlgorithm,
                keyWrapResponse.WrappedDataEncryptionKey,
                keyWrapResponse.EncryptionKeyWrapMetadata,
                DateTime.UtcNow);

            byte[] rawKey = null;
            InMemoryRawDek roundTripResponse = null;
            if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized))
            {
                roundTripResponse = await this.UnwrapAsync(tempDekProperties, encryptionAlgorithm, diagnosticsContext, cancellationToken);
                rawKey = roundTripResponse.DataEncryptionKey.RawKey;
            }
            else if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized))
            {
                (DataEncryptionKey dek, EncryptionKeyUnwrapResult unwrapResult) = await this.UnWrapDekAndInitMdeEncryptionAlgorithmAsync(
                    tempDekProperties,
                    diagnosticsContext,
                    cancellationToken);

                rawKey = unwrapResult.DataEncryptionKey;
                roundTripResponse = new InMemoryRawDek(dek, unwrapResult.ClientCacheTimeToLive);
            }

            if (!rawKey.SequenceEqual(key))
            {
                throw new InvalidOperationException("The key wrapping provider configured was unable to unwrap the wrapped key correctly.");
            }

            return (keyWrapResponse.WrappedDataEncryptionKey, keyWrapResponse.EncryptionKeyWrapMetadata, roundTripResponse);
        }

        internal async Task<InMemoryRawDek> UnwrapAsync(
            DataEncryptionKeyProperties dekProperties,
            string encryptionAlgorithm,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            DataEncryptionKey dek;
            EncryptionKeyUnwrapResult unwrapResult;

            if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized)
                && this.DekProvider.EncryptionKeyWrapProvider != null)
            {
                (dek, unwrapResult) = await this.UnWrapDekAndInitLegacyEncryptionAlgorithmAsync(
                encryptionAlgorithm,
                dekProperties,
                diagnosticsContext,
                cancellationToken);

                return new InMemoryRawDek(dek, unwrapResult.ClientCacheTimeToLive);
            }
            else if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)
                && this.DekProvider.MdeKeyWrapProvider != null)
            {
                dek = this.InitMdeEncryptionAlgorithm(dekProperties);

                // TTL is not used since DEK is not cached.
                return new InMemoryRawDek(dek, TimeSpan.FromMilliseconds(0));
            }
            else
            {
                throw new ArgumentException(string.Format(
                            "Unsupported encryption algorithm {0}." +
                            " Please initialize the Encryptor or CosmosDataEncryptionKeyProvider with an implementation of the EncryptionKeyStoreProvider / EncryptionKeyWrapProvider.",
                            dekProperties.EncryptionAlgorithm));
            }
        }

        private async Task<(byte[], EncryptionKeyWrapMetadata, InMemoryRawDek)> GenerateAndWrapRawDekForLegacyEncAlgoAsync(
            string id,
            string encryptionAlgorithm,
            EncryptionKeyWrapMetadata encryptionKeyWrapMetadata,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (this.DekProvider.EncryptionKeyWrapProvider == null)
            {
                throw new InvalidOperationException($"For use of '{CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized}' algorithm, " +
                    "Encryptor or CosmosDataEncryptionKeyProvider needs to be initialized with EncryptionKeyWrapProvider.");
            }

            byte[] rawDek = DataEncryptionKey.Generate(encryptionAlgorithm);

            return await this.WrapAsync(
                id,
                rawDek,
                encryptionAlgorithm,
                encryptionKeyWrapMetadata,
                diagnosticsContext,
                cancellationToken);
        }

        private (byte[], EncryptionKeyWrapMetadata) GenerateAndWrapPdekForMdeEncAlgo(EncryptionKeyWrapMetadata encryptionKeyWrapMetadata)
        {
            if (this.DekProvider.MdeKeyWrapProvider == null)
            {
                throw new InvalidOperationException($"For use of '{CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized}' algorithm, " +
                    "Encryptor or CosmosDataEncryptionKeyProvider needs to be initialized with EncryptionKeyStoreProvider.");
            }

            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                encryptionKeyWrapMetadata.Name,
                encryptionKeyWrapMetadata.Value,
                this.DekProvider.MdeKeyWrapProvider.EncryptionKeyStoreProvider);

            ProtectedDataEncryptionKey protectedDataEncryptionKey = new ProtectedDataEncryptionKey(
                encryptionKeyWrapMetadata.Name,
                keyEncryptionKey);

            byte[] wrappedDek = protectedDataEncryptionKey.EncryptedValue;
            EncryptionKeyWrapMetadata updatedMetadata = encryptionKeyWrapMetadata;
            return (wrappedDek, updatedMetadata);
        }

        private async Task<(DataEncryptionKey, EncryptionKeyUnwrapResult)> UnWrapDekAndInitLegacyEncryptionAlgorithmAsync(
            string encryptionAlgorithm,
            DataEncryptionKeyProperties dekProperties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            EncryptionKeyUnwrapResult unwrapResult = null;

            if (this.DekProvider.EncryptionKeyWrapProvider == null)
            {
                throw new InvalidOperationException($"For use of '{CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized}' algorithm, " +
                    "Encryptor or CosmosDataEncryptionKeyProvider needs to be initialized with EncryptionKeyWrapProvider.");
            }

            using (diagnosticsContext.CreateScope("UnwrapDataEncryptionKey"))
            {
                unwrapResult = await this.DekProvider.EncryptionKeyWrapProvider.UnwrapKeyAsync(
                                dekProperties.WrappedDataEncryptionKey,
                                dekProperties.EncryptionKeyWrapMetadata,
                                cancellationToken);
            }

            DataEncryptionKey dek = DataEncryptionKey.Create(
                unwrapResult.DataEncryptionKey,
                encryptionAlgorithm);

            return (dek, unwrapResult);
        }

        private async Task<(DataEncryptionKey, EncryptionKeyUnwrapResult)> UnWrapDekAndInitMdeEncryptionAlgorithmAsync(
            DataEncryptionKeyProperties dekProperties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            EncryptionKeyUnwrapResult unwrapResult;
            if (this.DekProvider.MdeKeyWrapProvider == null)
            {
                throw new InvalidOperationException($"For use of '{CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized}' algorithm, " +
                    "Encryptor or CosmosDataEncryptionKeyProvider needs to be initialized with EncryptionKeyStoreProvider.");
            }

            using (diagnosticsContext.CreateScope("UnwrapDataEncryptionKey"))
            {
                unwrapResult = await this.DekProvider.MdeKeyWrapProvider.UnwrapKeyAsync(
                                    dekProperties.WrappedDataEncryptionKey,
                                    dekProperties.EncryptionKeyWrapMetadata,
                                    cancellationToken);
            }

            DataEncryptionKey dek = new MdeEncryptionAlgorithm(
                dekProperties,
                Data.Encryption.Cryptography.EncryptionType.Randomized,
                this.DekProvider.MdeKeyWrapProvider.EncryptionKeyStoreProvider,
                this.DekProvider.PdekCacheTimeToLive);

            return (dek, unwrapResult);
        }

        private DataEncryptionKey InitMdeEncryptionAlgorithm(DataEncryptionKeyProperties dekProperties)
        {
            if (this.DekProvider.MdeKeyWrapProvider == null)
            {
                throw new InvalidOperationException($"For use of '{CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized}' algorithm, " +
                    "Encryptor or CosmosDataEncryptionKeyProvider needs to be initialized with EncryptionKeyStoreProvider.");
            }

            return new MdeEncryptionAlgorithm(
                dekProperties,
                Data.Encryption.Cryptography.EncryptionType.Randomized,
                this.DekProvider.MdeKeyWrapProvider.EncryptionKeyStoreProvider,
                this.DekProvider.PdekCacheTimeToLive);
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
            using (diagnosticsContext.CreateScope("ReadInternalAsync"))
            {
                return await this.DekProvider.Container.ReadItemAsync<DataEncryptionKeyProperties>(
                    id,
                    new PartitionKey(id),
                    requestOptions,
                    cancellationToken);
            }
        }
    }
}
