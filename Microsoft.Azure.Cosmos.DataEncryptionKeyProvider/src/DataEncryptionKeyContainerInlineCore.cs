//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.DataEncryptionKeyProvider
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DataEncryptionKeyContainerInlineCore : DataEncryptionKeyContainer
    {
        private readonly DataEncryptionKeyContainerCore dataEncryptionKeyContainerCore;

        public DataEncryptionKeyContainerInlineCore(DataEncryptionKeyContainerCore dataEncryptionKeyContainerCore)
        {
            if (dataEncryptionKeyContainerCore == null)
            {
                throw new ArgumentNullException(nameof(dataEncryptionKeyContainerCore));
            }

            this.dataEncryptionKeyContainerCore = dataEncryptionKeyContainerCore;
        }

        public override DataEncryptionKey GetDataEncryptionKey(string id)
        {
            return this.dataEncryptionKeyContainerCore.GetDataEncryptionKey(id);
        }

        public override FeedIterator<DataEncryptionKeyProperties> GetDataEncryptionKeyIterator(
            string startId = null,
            string endId = null,
            bool isDescending = false,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.dataEncryptionKeyContainerCore.GetDataEncryptionKeyIterator(startId, endId, isDescending, continuationToken, requestOptions);
        }

        public override Task<ItemResponse<DataEncryptionKeyProperties>> CreateDataEncryptionKeyAsync(
            string id,
            CosmosEncryptionAlgorithm encryptionAlgorithm,
            EncryptionKeyWrapMetadata encryptionKeyWrapMetadata,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.dataEncryptionKeyContainerCore.CreateDataEncryptionKeyAsync(id, encryptionAlgorithm, encryptionKeyWrapMetadata, requestOptions, cancellationToken));
        }
    }
}
