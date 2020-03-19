//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.DataEncryptionKeyProvider
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DataEncryptionKeyContainerCore : DataEncryptionKeyContainer
    {
        internal CosmosDataEncryptionKeyProvider DekProvider { get; }

        public DataEncryptionKeyContainerCore(CosmosDataEncryptionKeyProvider dekProvider)
        {
            this.DekProvider = dekProvider;
        }

        public override DataEncryptionKey GetDataEncryptionKey(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return new DataEncryptionKeyInlineCore(
                new DataEncryptionKeyCore(
                    this.DekProvider,
                    id));
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
                CosmosEncryptionAlgorithm encryptionAlgorithmId,
                EncryptionKeyWrapMetadata encryptionKeyWrapMetadata,
                ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (encryptionAlgorithmId != CosmosEncryptionAlgorithm.AE_AES_256_CBC_HMAC_SHA_256_RANDOMIZED)
            {
                throw new ArgumentException(string.Format("Unsupported Encryption Algorithm {0}", encryptionAlgorithmId), nameof(encryptionAlgorithmId));
            }

            if (encryptionKeyWrapMetadata == null)
            {
                throw new ArgumentNullException(nameof(encryptionKeyWrapMetadata));
            }

            DataEncryptionKeyCore newDek = (DataEncryptionKeyInlineCore)this.GetDataEncryptionKey(id);

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);

            byte[] rawDek = newDek.GenerateKey(encryptionAlgorithmId);

            (byte[] wrappedDek, EncryptionKeyWrapMetadata updatedMetadata, InMemoryRawDek inMemoryRawDek) = await newDek.WrapAsync(
                rawDek,
                encryptionAlgorithmId,
                encryptionKeyWrapMetadata,
                diagnosticsContext,
                cancellationToken);

            DataEncryptionKeyProperties dekProperties = new DataEncryptionKeyProperties(id, encryptionAlgorithmId, wrappedDek, updatedMetadata);

            ItemResponse<DataEncryptionKeyProperties> dekResponse = await this.DekProvider.Container.CreateItemAsync(dekProperties, new PartitionKey(dekProperties.Id), cancellationToken: cancellationToken);
            this.DekProvider.DekCache.SetDekProperties(id, dekResponse.Resource);
            this.DekProvider.DekCache.SetRawDek(dekResponse.Resource.SelfLink, inMemoryRawDek);
            return dekResponse;
        }
    }
}
