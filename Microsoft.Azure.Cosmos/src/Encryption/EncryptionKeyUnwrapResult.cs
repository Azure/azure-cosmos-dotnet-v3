//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Result from a <see cref="EncryptionKeyWrapProvider"/> on unwrapping a wrapped data encryption key.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        class EncryptionKeyUnwrapResult
    {
        /// <summary>
        /// Initializes a new instance of the result of unwrapping a wrapped data encryption key.
        /// </summary>
        /// <param name="dataEncryptionKey">
        /// Raw form of data encryption key.
        /// The byte array passed in must not be modified after this call by the <see cref="EncryptionKeyWrapProvider"/>.
        /// </param>
        /// <param name="clientCacheTimeToLive">
        /// Amount of time after which the raw data encryption key must not be used
        /// without invoking the <see cref="EncryptionKeyWrapProvider.UnwrapKeyAsync"/> again.
        /// </param>
        public EncryptionKeyUnwrapResult(byte[] dataEncryptionKey, TimeSpan clientCacheTimeToLive)
        {
            this.DataEncryptionKeyBytes = dataEncryptionKey ?? throw new ArgumentNullException(nameof(dataEncryptionKey));

            if (clientCacheTimeToLive < TimeSpan.Zero)
            {
                throw new ArgumentException("Expected non-negative timespan", nameof(clientCacheTimeToLive));
            }

            this.ClientCacheTimeToLive = clientCacheTimeToLive;
        }

        /// <summary>
        /// Raw form of the data encryption key.
        /// </summary>
        internal ReadOnlyMemory<byte> DataEncryptionKey => this.DataEncryptionKeyBytes;

        /// <summary>
        /// Amount of time after which the raw data encryption key must not be used
        /// without invoking the <see cref="EncryptionKeyWrapProvider.UnwrapKeyAsync"/> again.
        /// </summary>
        public TimeSpan ClientCacheTimeToLive { get; }

        internal byte[] DataEncryptionKeyBytes { get; }
    }
}
