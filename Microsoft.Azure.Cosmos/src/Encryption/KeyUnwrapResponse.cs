//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Response from a <see cref="KeyWrapProvider"/> on unwrapping a wrapped data encryption key.
    /// </summary>
    public class KeyUnwrapResponse
    {
        /// <summary>
        /// Initializes a new instance of the response of unwrapping a wrapped data encryption key.
        /// </summary>
        /// <param name="dataEncryptionKey">Raw form of data encryption key.</param>
        /// <param name="clientCacheTimeToLive">
        /// Amount of time after which the raw data encryption key must not be used
        /// without invoking the <see cref="KeyWrapProvider.UnwrapKeyAsync"/> again.
        /// </param>
        public KeyUnwrapResponse(byte[] dataEncryptionKey, TimeSpan clientCacheTimeToLive)
        {
            this.DataEncryptionKey = dataEncryptionKey;
            this.ClientCacheTimeToLive = clientCacheTimeToLive;
        }

        /// <summary>
        /// Raw form of the data encryption key.
        /// </summary>
        public byte[] DataEncryptionKey { get; }

        /// <summary>
        /// Amount of time after which the raw data encryption key must not be used
        /// without invoking the <see cref="KeyWrapProvider.UnwrapKeyAsync"/> again.
        /// </summary>
        public TimeSpan ClientCacheTimeToLive { get; }
    }
}
