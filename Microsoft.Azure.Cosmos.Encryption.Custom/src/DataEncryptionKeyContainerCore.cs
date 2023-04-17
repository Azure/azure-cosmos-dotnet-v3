//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Diagnostics;
    using System.IO;
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
            return new DataEncryptionKeyFeedIterator<T>(
                new DataEncryptionKeyFeedIterator(
                    this.DekProvider.Container.GetItemQueryStreamIterator(
                        queryText,
                        continuationToken,
                        requestOptions)),
                this.DekProvider.Container.Database.Client.ResponseFactory);
        }

        public override FeedIterator<T> GetDataEncryptionKeyQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new DataEncryptionKeyFeedIterator<T>(
                new DataEncryptionKeyFeedIterator(
                    this.DekProvider.Container.GetItemQueryStreamIterator(
                        queryDefinition,
                        continuationToken,
                        requestOptions)),
                this.DekProvider.Container.Database.Client.ResponseFactory);
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
                (wrappedDek, updatedMetadata) = this.GenerateAndWrapPdekForMdeEncAlgo(id, encryptionKeyWrapMetadata);
            }

            DataEncryptionKeyProperties dekProperties = new DataEncryptionKeyProperties(
                    id,
                    encryptionAlgorithm,
                    wrappedDek,
                    updatedMetadata,
                    DateTime.UtcNow);

            // Since T is not exposed, a user passing System.Text based custom serializers would result in a failure, since DataEncryptionKeyProperties is based
            // on Newtonsoft JSON serialization and is not compatible. So we just convert it to stream using cosmos's base serializer.
            Stream dekpropertiesStream = EncryptionProcessor.BaseSerializer.ToStream(dekProperties);

            ResponseMessage dekResponse = await this.DekProvider.Container.CreateItemStreamAsync(
                dekpropertiesStream,
                new PartitionKey(dekProperties.Id),
                cancellationToken: cancellationToken);

            if (!dekResponse.IsSuccessStatusCode)
            {
                string subStatusCode = dekResponse.Headers.GetValueOrDefault("x-ms-substatus") ?? "0";
                throw new EncryptionCosmosException(dekResponse.ErrorMessage, dekResponse.StatusCode, int.Parse(subStatusCode), dekResponse.Headers.ActivityId, dekResponse.Headers.RequestCharge, dekResponse.Diagnostics);
            }

            dekProperties = EncryptionProcessor.BaseSerializer.FromStream<DataEncryptionKeyProperties>(dekResponse.Content);

            this.DekProvider.DekCache.SetDekProperties(id, dekProperties);

            if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized))
            {
                this.DekProvider.DekCache.SetRawDek(id, inMemoryRawDek);
            }

            return new EncryptionItemResponse<DataEncryptionKeyProperties>(dekResponse, dekProperties);
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
           string encryptionAlgorithm = null,
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

            if (!string.IsNullOrEmpty(encryptionAlgorithm))
            {
                if (string.Equals(dekProperties.EncryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)
                    && string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized))
                {
                    throw new InvalidOperationException($"Rewrap operation with EncryptionAlgorithm '{encryptionAlgorithm}' is not supported on Data Encryption Keys" +
                        $" which are configured with '{CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized}'. ");
                }

                dekProperties.EncryptionAlgorithm = encryptionAlgorithm;
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

            ResponseMessage dekResponse;

            try
            {
                Stream newDekpropertiesStream = EncryptionProcessor.BaseSerializer.ToStream(newDekProperties);

                dekResponse = await this.DekProvider.Container.ReplaceItemStreamAsync(
                   newDekpropertiesStream,
                   newDekProperties.Id,
                   new PartitionKey(newDekProperties.Id),
                   requestOptions,
                   cancellationToken);

                if (!dekResponse.IsSuccessStatusCode)
                {
                    string subStatusCode = dekResponse.Headers.GetValueOrDefault("x-ms-substatus") ?? "0";
                    throw new EncryptionCosmosException(dekResponse.ErrorMessage, dekResponse.StatusCode, int.Parse(subStatusCode), dekResponse.Headers.ActivityId, dekResponse.Headers.RequestCharge, dekResponse.Diagnostics);
                }

                dekProperties = EncryptionProcessor.BaseSerializer.FromStream<DataEncryptionKeyProperties>(dekResponse.Content);
                Debug.Assert(dekProperties != null);
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
                    encryptionAlgorithm,
                    requestOptions,
                    cancellationToken);
            }

            this.DekProvider.DekCache.SetDekProperties(id, dekProperties);

            if (string.Equals(newDekProperties.EncryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized))
            {
                this.DekProvider.DekCache.SetRawDek(id, updatedRawDek);
            }

            return new EncryptionItemResponse<DataEncryptionKeyProperties>(dekResponse, dekProperties);
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

        /// <summary>
        /// For providing support to Encrypt/Decrypt items using Legacy DEK with MDE based algorithm.
        /// UnWrap using Legacy Key Provider and Init MDE Encryption Algorithm with Unwrapped Key.
        /// </summary>
        /// <param name="dekProperties"> DEK Properties </param>
        /// <param name="cancellationToken"> Cancellation Token </param>
        /// <returns> Data Encryption Key </returns>
        internal async Task<DataEncryptionKey> FetchUnWrappedMdeSupportedLegacyDekAsync(
            DataEncryptionKeyProperties dekProperties,
            CancellationToken cancellationToken)
        {
            if (this.DekProvider.EncryptionKeyWrapProvider == null)
            {
                throw new InvalidOperationException($"For use of '{CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized}' algorithm based DEK, " +
                    "Encryptor or CosmosDataEncryptionKeyProvider needs to be initialized with EncryptionKeyWrapProvider.");
            }

            EncryptionKeyUnwrapResult unwrapResult;
            try
            {
                // unwrap with original wrap provider
                unwrapResult = await this.DekProvider.EncryptionKeyWrapProvider.UnwrapKeyAsync(
                            dekProperties.WrappedDataEncryptionKey,
                            dekProperties.EncryptionKeyWrapMetadata,
                            cancellationToken);
            }
            catch (Exception exception)
            {
                throw EncryptionExceptionFactory.EncryptionKeyNotFoundException(
                    $"Failed to unwrap Data Encryption Key with id: '{dekProperties.Id}'.",
                    exception);
            }

            // Init PlainDataEncryptionKey and then Init MDE Algorithm using PlaintextDataEncryptionKey.
            // PlaintextDataEncryptionKey derives DataEncryptionkey to Init a Raw Root Key received via Legacy WrapProvider Unwrap result.
            PlaintextDataEncryptionKey plaintextDataEncryptionKey = new PlaintextDataEncryptionKey(
                dekProperties.Id,
                unwrapResult.DataEncryptionKey);

            return new MdeEncryptionAlgorithm(
                unwrapResult.DataEncryptionKey,
                plaintextDataEncryptionKey,
                Data.Encryption.Cryptography.EncryptionType.Randomized);
        }

        internal async Task<DataEncryptionKey> FetchUnWrappedLegacySupportedMdeDekAsync(
            DataEncryptionKeyProperties dekProperties,
            string encryptionAlgorithm,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            EncryptionKeyUnwrapResult unwrapResult;

            if (this.DekProvider.EncryptionKeyStoreProvider == null)
            {
                throw new InvalidOperationException($"For use of '{CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized}' algorithm based DEK, " +
                    "Encryptor or CosmosDataEncryptionKeyProvider needs to be initialized with EncryptionKeyStoreProvider.");
            }

            try
            {
                using (diagnosticsContext.CreateScope("UnwrapDataEncryptionKey"))
                {
                    unwrapResult = await this.UnWrapDekMdeEncAlgoAsync(
                       dekProperties,
                       diagnosticsContext,
                       cancellationToken);
                }

                return DataEncryptionKey.Create(
                    unwrapResult.DataEncryptionKey,
                    encryptionAlgorithm);
            }
            catch (Exception exception)
            {
                throw EncryptionExceptionFactory.EncryptionKeyNotFoundException(
                    $"Failed to unwrap Data Encryption Key with id: '{dekProperties.Id}'.",
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
                if (string.Equals(dekProperties.EncryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized))
                {

                    if (this.DekProvider.MdeKeyWrapProvider == null)
                    {
                        throw new InvalidOperationException($"For use of '{CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized}' algorithm based DEK, " +
                            "Encryptor or CosmosDataMdeKeyWrapProvider needs to be initialized with MdeKeyWrapProvider.");
                    }

                    EncryptionKeyUnwrapResult unwrapResult;
                    try
                    {
                        // unwrap with original wrap provider
                        unwrapResult = await this.DekProvider.MdeKeyWrapProvider.UnwrapKeyAsync(
                                    dekProperties.WrappedDataEncryptionKey,
                                    dekProperties.EncryptionKeyWrapMetadata,
                                    cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        throw EncryptionExceptionFactory.EncryptionKeyNotFoundException(
                            $"Failed to unwrap Data Encryption Key with id: '{dekProperties.Id}'.",
                            exception);
                    }

                    DataEncryptionKey dek = this.InitMdeEncryptionAlgorithm(unwrapResult.DataEncryptionKey, dekProperties);

                    // TTL is not used since DEK is not cached.
                    return new InMemoryRawDek(dek, TimeSpan.FromMilliseconds(0));
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0045:Convert to conditional expression", Justification = "Readability")]
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
                if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized) && this.DekProvider.EncryptionKeyWrapProvider != null)
                {
                    keyWrapResponse = await this.DekProvider.EncryptionKeyWrapProvider.WrapKeyAsync(key, metadata, cancellationToken);
                }
                else if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized) && this.DekProvider.MdeKeyWrapProvider != null)
                {
                    keyWrapResponse = await this.DekProvider.MdeKeyWrapProvider.WrapKeyAsync(key, metadata, cancellationToken);
                }
                else
                {
                    throw new ArgumentException(string.Format(
                            "Unsupported encryption algorithm {0}." +
                            " Please initialize the Encryptor or CosmosDataEncryptionKeyProvider with an implementation of the EncryptionKeyStoreProvider / EncryptionKeyWrapProvider.",
                            encryptionAlgorithm));
                }
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
                roundTripResponse = await this.UnwrapAsync(tempDekProperties, diagnosticsContext, cancellationToken);
                rawKey = roundTripResponse.DataEncryptionKey.RawKey;
            }
            else if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized))
            {
                EncryptionKeyUnwrapResult unwrapResult = await this.UnWrapDekMdeEncAlgoAsync(
                    tempDekProperties,
                    diagnosticsContext,
                    cancellationToken);

                rawKey = unwrapResult.DataEncryptionKey;
            }

            if (!rawKey.SequenceEqual(key))
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
                dekProperties.EncryptionAlgorithm);

            return new InMemoryRawDek(dek, unwrapResult.ClientCacheTimeToLive);
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

        private (byte[], EncryptionKeyWrapMetadata) GenerateAndWrapPdekForMdeEncAlgo(string id, EncryptionKeyWrapMetadata encryptionKeyWrapMetadata)
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
                id,
                keyEncryptionKey);

            byte[] wrappedDek = protectedDataEncryptionKey.EncryptedValue;
            EncryptionKeyWrapMetadata updatedMetadata = encryptionKeyWrapMetadata;
            return (wrappedDek, updatedMetadata);
        }

        private async Task<EncryptionKeyUnwrapResult> UnWrapDekMdeEncAlgoAsync(
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

            return unwrapResult;
        }

        internal DataEncryptionKey InitMdeEncryptionAlgorithm(byte[] unwrapKey, DataEncryptionKeyProperties dekProperties)
        {
            if (this.DekProvider.MdeKeyWrapProvider == null)
            {
                throw new InvalidOperationException($"For use of '{CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized}' algorithm, " +
                    "Encryptor or CosmosDataEncryptionKeyProvider needs to be initialized with EncryptionKeyStoreProvider.");
            }

            return new MdeEncryptionAlgorithm(
                unwrapKey,
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
                ResponseMessage response = await this.DekProvider.Container.ReadItemStreamAsync(
                    id,
                    new PartitionKey(id),
                    requestOptions,
                    cancellationToken: cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string subStatusCode = response.Headers.GetValueOrDefault("x-ms-substatus") ?? "0";
                    throw new EncryptionCosmosException(response.ErrorMessage, response.StatusCode, int.Parse(subStatusCode), response.Headers.ActivityId, response.Headers.RequestCharge, response.Diagnostics);
                }

                DataEncryptionKeyProperties dekProperties = EncryptionProcessor.BaseSerializer.FromStream<DataEncryptionKeyProperties>(response.Content);

                return new EncryptionItemResponse<DataEncryptionKeyProperties>(response, dekProperties);
            }
        }
    }
}
