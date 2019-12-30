//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Options around encryption / decryption of data.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        class EncryptionOptions
    {
        /// <summary>
        /// Reference to encryption key to be used for encryption / decrytion of data.
        /// The key must already be created using <see cref="Database.CreateDataEncryptionKeyAsync"/>
        /// before using it in encryption options.
        /// </summary>
        public DataEncryptionKey DataEncryptionKey { get; set; }
    }
}