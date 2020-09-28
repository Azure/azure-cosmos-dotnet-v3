// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <summary>
    /// Encryption exception
    /// </summary>
    public class EncryptionException : Exception
    {
        /// <summary>
        /// Gets the Data Encryption Key Id used.
        /// </summary>
        public string DataEncryptionKeyId { get; }

        /// <summary>
        /// Gets the raw encrypted content as string.
        /// </summary>
        public string EncryptedContent { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptionException"/> class.
        /// </summary>
        /// <param name="dataEncryptionKeyId">DataEncryptionKey id</param>
        /// <param name="encryptedContent">Encrypted content</param>
        /// <param name="innerException">The inner exception</param>
        public EncryptionException(
            string dataEncryptionKeyId,
            string encryptedContent,
            Exception innerException)
            : base(innerException.Message, innerException)
        {
            this.DataEncryptionKeyId = dataEncryptionKeyId ?? throw new ArgumentNullException(dataEncryptionKeyId);
            this.EncryptedContent = encryptedContent ?? throw new ArgumentNullException(encryptedContent);
        }
    }
}
