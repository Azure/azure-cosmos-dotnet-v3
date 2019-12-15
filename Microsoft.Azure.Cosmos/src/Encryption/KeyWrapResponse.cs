//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Response from a <see cref="KeyWrapProvider"/> on wrapping a data encryption key.
    /// </summary>
    public class KeyWrapResponse
    {
        /// <summary>
        /// Initializes a new instance of the response of wrapping a data encryption key.
        /// </summary>
        /// <param name="wrappedDataEncryptionKey">Wrapped form of data encryption key.</param>
        /// <param name="keyWrapMetadata">Metadata that can be used by the wrap provider to unwrap the key.</param>
        public KeyWrapResponse(byte[] wrappedDataEncryptionKey, KeyWrapMetadata keyWrapMetadata)
        {
            this.WrappedDataEncryptionKey = wrappedDataEncryptionKey;
            this.KeyWrapMetadata = keyWrapMetadata;
        }

        /// <summary>
        /// Wrapped form of the data encryption key.
        /// </summary>
        public byte[] WrappedDataEncryptionKey { get; }

        /// <summary>
        /// Metadata that can be used by the wrap provider to unwrap the key.
        /// </summary>
        public KeyWrapMetadata KeyWrapMetadata { get; }
    }
}
