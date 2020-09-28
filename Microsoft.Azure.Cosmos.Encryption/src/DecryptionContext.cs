// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provides context about decryption operation.
    /// </summary>
    public sealed class DecryptionContext
    {
        /// <summary>
        /// Gets the list of <see cref="DecryptionInfo"/> corresponding to the DataEncryptionKey(s) used.
        /// </summary>
        public IReadOnlyList<DecryptionInfo> DecryptionInfoList { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DecryptionContext"/> class.
        /// </summary>
        /// <param name="decryptionInfoList">List of DecryptionInfo.</param>
        public DecryptionContext(
            IReadOnlyList<DecryptionInfo> decryptionInfoList)
        {
            this.DecryptionInfoList = decryptionInfoList ?? throw new ArgumentNullException(nameof(decryptionInfoList));
        }
    }
}
