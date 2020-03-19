//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.DataEncryptionKeyProvider
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DataEncryptionKeyCore : DataEncryptionKey
    {
        internal DataEncryptionKeyCore(
            CosmosDataEncryptionKeyProvider dekProvider,
            string dekId)
        {
            this.Id = dekId;
            this.DekProvider = dekProvider;
        }

        /// <inheritdoc/>
        public override string Id { get; }

        internal CosmosDataEncryptionKeyProvider DekProvider { get; }

        /// <inheritdoc/>
        public override async Task<ItemResponse<DataEncryptionKeyProperties>> ReadAsync(
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ItemResponse<DataEncryptionKeyProperties> response = await this.ReadInternalAsync(requestOptions, diagnosticsContext: null, cancellationToken: cancellationToken);
            this.DekProvider.DekCache.SetDekProperties(this.Id, response.Resource);
            return response;
        }

        /// <inheritdoc/>
        public override async Task<ItemResponse<DataEncryptionKeyProperties>> RewrapAsync(
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
                diagnosticsContext,
                cancellationToken);

            (byte[] wrappedDek, EncryptionKeyWrapMetadata updatedMetadata, InMemoryRawDek updatedRawDek) = await this.WrapAsync(
                    inMemoryRawDek.RawDek,
                    dekProperties.EncryptionAlgorithmId,
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

            this.DekProvider.DekCache.SetDekProperties(this.Id, response.Resource);
            this.DekProvider.DekCache.SetRawDek(this.Id, updatedRawDek);
            return response;
        }

        internal async Task<(DataEncryptionKeyProperties, InMemoryRawDek)> FetchUnwrappedAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            DataEncryptionKeyProperties dekProperties = await this.DekProvider.DekCache.GetOrAddDekPropertiesAsync(
                    this.Id,
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

        internal virtual EncryptionAlgorithm GetEncryptionAlgorithm(
            byte[] rawDek, 
            CosmosEncryptionAlgorithm encryptionAlgorithmId)
        {
            Debug.Assert(encryptionAlgorithmId == CosmosEncryptionAlgorithm.AE_AES_256_CBC_HMAC_SHA_256_RANDOMIZED, "Unexpected encryption algorithm id");
            AeadAes256CbcHmac256EncryptionKey key = new AeadAes256CbcHmac256EncryptionKey(rawDek, AeadAes256CbcHmac256Algorithm.AlgorithmNameConstant);
            return new AeadAes256CbcHmac256Algorithm(key, EncryptionType.Randomized, algorithmVersion: 1);
        }

        internal virtual byte[] GenerateKey(CosmosEncryptionAlgorithm encryptionAlgorithmId)
        {
            Debug.Assert(encryptionAlgorithmId == CosmosEncryptionAlgorithm.AE_AES_256_CBC_HMAC_SHA_256_RANDOMIZED, "Unexpected encryption algorithm id");
            byte[] rawDek = new byte[32];
            SecurityUtility.GenerateRandomBytes(rawDek);
            return rawDek;
        }

        internal async Task<(byte[], EncryptionKeyWrapMetadata, InMemoryRawDek)> WrapAsync(
            byte[] key,
            CosmosEncryptionAlgorithm encryptionAlgorithmId,
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
            DataEncryptionKeyProperties tempDekProperties = new DataEncryptionKeyProperties(this.Id, encryptionAlgorithmId, keyWrapResponse.WrappedDataEncryptionKey, keyWrapResponse.EncryptionKeyWrapMetadata);
            InMemoryRawDek roundTripResponse = await this.UnwrapAsync(tempDekProperties, diagnosticsContext, cancellationToken);
            if (!roundTripResponse.RawDek.SequenceEqual(key))
            {
                throw new InvalidOperationException(ClientResources.KeyWrappingDidNotRoundtrip);
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

            EncryptionAlgorithm encryptionAlgorithm = this.GetEncryptionAlgorithm(unwrapResult.DataEncryptionKey, dekProperties.EncryptionAlgorithmId);

            return new InMemoryRawDek(unwrapResult.DataEncryptionKey, encryptionAlgorithm, unwrapResult.ClientCacheTimeToLive);
        }

        private async Task<DataEncryptionKeyProperties> ReadResourceAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            using (diagnosticsContext.CreateScope("ReadDataEncryptionKey"))
            {
                return await this.ReadInternalAsync(
                    requestOptions: null,
                    diagnosticsContext: diagnosticsContext,
                    cancellationToken: cancellationToken);
            }
        }

        private async Task<ItemResponse<DataEncryptionKeyProperties>> ReadInternalAsync(
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return await this.DekProvider.Container.ReadItemAsync<DataEncryptionKeyProperties>(
                this.Id,
                new PartitionKey(this.Id),
                cancellationToken: cancellationToken);
        }
    }
}
