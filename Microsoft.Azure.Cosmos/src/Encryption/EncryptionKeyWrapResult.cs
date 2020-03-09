//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Result from a <see cref="EncryptionKeyWrapProvider"/> on wrapping a data encryption key.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        class EncryptionKeyWrapResult
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
            this.WrappedDataEncryptionKeyBytes = wrappedDataEncryptionKey ?? throw new ArgumentNullException(nameof(wrappedDataEncryptionKey));
            this.EncryptionKeyWrapMetadata = encryptionKeyWrapMetadata ?? throw new ArgumentNullException(nameof(encryptionKeyWrapMetadata));
        }

        /// <summary>
        /// Wrapped form of the data encryption key.
        /// </summary>
        public ReadOnlyMemory<byte> WrappedDataEncryptionKey => this.WrappedDataEncryptionKeyBytes;

        /// <summary>
        /// Metadata that can be used by the wrap provider to unwrap the key.
        /// </summary>
        public EncryptionKeyWrapMetadata EncryptionKeyWrapMetadata { get; }

        internal byte[] WrappedDataEncryptionKeyBytes { get; }
    }
}
