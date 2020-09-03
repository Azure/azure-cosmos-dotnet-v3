// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.Collections.Generic;

    /// <summary>
    /// Provides information about decryption operation.
    /// </summary>
    public class DecryptionInfo
    {
        /// <summary>
        /// Gets the list of JSON paths decrypted.
        /// </summary>
        public IReadOnlyList<string> PathsDecrypted { get; }

        /// <summary>
        /// Gets the DataEncryptionKey id used for decryption.
        /// </summary>
        public string DataEncryptionKeyId { get; }

        /// <summary>
        /// For customer to use for mocking
        /// </summary>
        protected DecryptionInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DecryptionInfo"/> class.
        /// </summary>
        /// <param name="pathsDecrypted">List of paths that were decrypted.</param>
        /// <param name="dataEncryptionKeyId">DataEncryptionKey id used for decryption.</param>
        internal DecryptionInfo(
            List<string> pathsDecrypted,
            string dataEncryptionKeyId)
        {
            this.PathsDecrypted = pathsDecrypted;
            this.DataEncryptionKeyId = dataEncryptionKeyId;
        }
    }
}
