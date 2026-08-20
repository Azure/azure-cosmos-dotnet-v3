// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;

    /// <summary>
    /// Encryption exception
    /// </summary>
    public sealed class EncryptionException : Exception
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
            // This exception wraps a real decrypt failure (innerException). A corrupt document can
            // legitimately have a missing/null DEK id (e.g. an _ei block without _en). Coalesce to
            // empty rather than throwing here: throwing ArgumentNullException from the constructor
            // would discard innerException and surface a confusing ArgumentNullException instead of
            // the real error. This matches the stream-mode DecryptableItem path, which already
            // coalesces these values.
            this.DataEncryptionKeyId = dataEncryptionKeyId ?? string.Empty;
            this.EncryptedContent = encryptedContent ?? string.Empty;
        }
    }
}
