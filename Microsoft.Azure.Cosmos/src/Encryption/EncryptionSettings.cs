//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Settings around encryption of data.
    /// See <see href="tbd"/> for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    class EncryptionSettings
    {
        /// <summary>
        /// Encryption key wrap provider that will be used to wrap and unwrap data encryption keys.
        /// </summary>
        public EncryptionKeyWrapProvider EncryptionKeyWrapProvider { get; }

        /// <summary>
        /// Creates a new instance of encryption settings.
        /// </summary>
        /// <param name="encryptionKeyWrapProvider"> Encryption key wrap provider that will be used to wrap and unwrap data encryption keys.</param>
        public EncryptionSettings(EncryptionKeyWrapProvider encryptionKeyWrapProvider)
        {
            this.EncryptionKeyWrapProvider = encryptionKeyWrapProvider ?? throw new ArgumentNullException(nameof(encryptionKeyWrapProvider));
        }
    }
}
