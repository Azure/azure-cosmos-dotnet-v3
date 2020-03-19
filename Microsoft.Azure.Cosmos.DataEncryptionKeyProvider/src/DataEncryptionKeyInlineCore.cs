//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.DataEncryptionKeyProvider
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This class acts as a wrapper over <see cref="DataEncryptionKeyCore"/> for environments that use SynchronizationContext.
    /// </summary>
    internal class DataEncryptionKeyInlineCore : DataEncryptionKey
    {
        private readonly DataEncryptionKeyCore dataEncryptionKey;

        internal DataEncryptionKeyInlineCore(DataEncryptionKeyCore dataEncryptionKey)
        {
            if (dataEncryptionKey == null)
            {
                throw new ArgumentException(nameof(dataEncryptionKey));
            }

            this.dataEncryptionKey = dataEncryptionKey;
        }

        /// <inheritdoc/>
        public override string Id => this.dataEncryptionKey.Id;

        /// <inheritdoc/>
        public override Task<ItemResponse<DataEncryptionKeyProperties>> ReadAsync(
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return TaskHelper.RunInlineIfNeededAsync(() =>
                this.dataEncryptionKey.ReadAsync(requestOptions, cancellationToken));
        }

        /// <inheritdoc/>
        public override Task<ItemResponse<DataEncryptionKeyProperties>> RewrapAsync(
           EncryptionKeyWrapMetadata newWrapMetadata,
           ItemRequestOptions requestOptions = null,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            if (newWrapMetadata == null)
            {
                throw new ArgumentNullException(nameof(newWrapMetadata));
            }

            return TaskHelper.RunInlineIfNeededAsync(() =>
                this.dataEncryptionKey.RewrapAsync(newWrapMetadata, requestOptions, cancellationToken));
        }

        public static implicit operator DataEncryptionKeyCore(DataEncryptionKeyInlineCore dekInlineCore) => dekInlineCore.dataEncryptionKey;
    }
}
