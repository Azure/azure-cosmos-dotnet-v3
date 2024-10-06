//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;

    /// <summary>
    /// Result from a <see cref="EncryptionKeyWrapProvider"/> on wrapping a data encryption key.
    /// </summary>
    public class EncryptionKeyWrapResult
    {
        /// <summary>
        /// Initializes a new instance of the result of wrapping a data encryption key.
        /// </summary>
        /// <param name="wrappedDataEncryptionKey">
        /// Wrapped form of data encryption key.
        /// The byte array passed in must not be modified after this call by the <see cref="EncryptionKeyWrapProvider"/>.
        /// </param>
        /// <param name="encryptionKeyWrapMetadata">Metadata that can be used by the wrap provider to unwrap the data encryption key.</param>
        public EncryptionKeyWrapResult(byte[] wrappedDataEncryptionKey, EncryptionKeyWrapMetadata encryptionKeyWrapMetadata)
        {
            this.WrappedDataEncryptionKey = wrappedDataEncryptionKey ?? throw new ArgumentNullException(nameof(wrappedDataEncryptionKey));
            this.EncryptionKeyWrapMetadata = encryptionKeyWrapMetadata ?? throw new ArgumentNullException(nameof(encryptionKeyWrapMetadata));
        }

        /// <summary>
        /// Gets wrapped form of the data encryption key.
        /// </summary>
        public byte[] WrappedDataEncryptionKey { get; }

        /// <summary>
        /// Gets metadata that can be used by the wrap provider to unwrap the key.
        /// </summary>
        public EncryptionKeyWrapMetadata EncryptionKeyWrapMetadata { get; }
    }
}
