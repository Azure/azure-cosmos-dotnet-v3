//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;

    /// <summary>
    /// Result from a <see cref="EncryptionKeyWrapProvider"/> on unwrapping a wrapped data encryption key.
    /// </summary>
    public class EncryptionKeyUnwrapResult
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
            this.DataEncryptionKey = dataEncryptionKey ?? throw new ArgumentNullException(nameof(dataEncryptionKey));

            if (clientCacheTimeToLive < TimeSpan.Zero)
            {
                throw new ArgumentException("Expected non-negative timespan", nameof(clientCacheTimeToLive));
            }

            this.ClientCacheTimeToLive = clientCacheTimeToLive;
        }

        /// <summary>
        /// Gets raw form of the data encryption key.
        /// </summary>
        public byte[] DataEncryptionKey { get; }

        /// <summary>
        /// Gets amount of time after which the raw data encryption key must not be used
        /// without invoking the <see cref="EncryptionKeyWrapProvider.UnwrapKeyAsync"/> again.
        /// </summary>
        public TimeSpan ClientCacheTimeToLive { get; }
    }
}
