//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    /// <summary>
    /// The <see cref="ItemRequestOptions"/> that allows to specify options for encryption / decryption.
    /// </summary>
    public sealed class EncryptionItemRequestOptions : ItemRequestOptions
    {
        /// <summary>
        /// Gets or sets options to be provided for encryption of data.
        /// </summary>
        public EncryptionOptions EncryptionOptions { get; set; }
    }
}
