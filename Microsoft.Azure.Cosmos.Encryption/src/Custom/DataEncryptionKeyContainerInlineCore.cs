//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DataEncryptionKeyContainerInlineCore : DataEncryptionKeyContainer
    {
        private readonly DataEncryptionKeyContainerCore dataEncryptionKeyContainerCore;

        public DataEncryptionKeyContainerInlineCore(DataEncryptionKeyContainerCore dataEncryptionKeyContainerCore)
        {
            this.dataEncryptionKeyContainerCore = dataEncryptionKeyContainerCore ?? throw new ArgumentNullException(nameof(dataEncryptionKeyContainerCore));
        }

        public override FeedIterator<T> GetDataEncryptionKeyQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.dataEncryptionKeyContainerCore.GetDataEncryptionKeyQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator<T> GetDataEncryptionKeyQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.dataEncryptionKeyContainerCore.GetDataEncryptionKeyQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override Task<ItemResponse<DataEncryptionKeyProperties>> CreateDataEncryptionKeyAsync(
            string id,
            string encryptionAlgorithm,
            EncryptionKeyWrapMetadata encryptionKeyWrapMetadata,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() =>
                this.dataEncryptionKeyContainerCore.CreateDataEncryptionKeyAsync(id, encryptionAlgorithm, encryptionKeyWrapMetadata, requestOptions, cancellationToken));
        }

        /// <inheritdoc/>
        public override Task<ItemResponse<DataEncryptionKeyProperties>> ReadDataEncryptionKeyAsync(
            string id,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return TaskHelper.RunInlineIfNeededAsync(() =>
                this.dataEncryptionKeyContainerCore.ReadDataEncryptionKeyAsync(id, requestOptions, cancellationToken));
        }

        /// <inheritdoc/>
        public override Task<ItemResponse<DataEncryptionKeyProperties>> RewrapDataEncryptionKeyAsync(
           string id,
           EncryptionKeyWrapMetadata newWrapMetadata,
           string encryptionAlgorithm = null,
           ItemRequestOptions requestOptions = null,
           CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (newWrapMetadata == null)
            {
                throw new ArgumentNullException(nameof(newWrapMetadata));
            }

            return TaskHelper.RunInlineIfNeededAsync(() =>
                this.dataEncryptionKeyContainerCore.RewrapDataEncryptionKeyAsync(id, newWrapMetadata, encryptionAlgorithm, requestOptions, cancellationToken));
        }
    }
}
