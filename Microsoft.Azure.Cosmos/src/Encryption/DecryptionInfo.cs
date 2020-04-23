//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Information regarding decryption processing
    /// </summary>
    public class DecryptionInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DecryptionInfo"/> class.
        /// </summary>
        /// <param name="hasDecryptionFailed"></param>
        /// <param name="decryptedPaths"></param>
        /// <param name="message"></param>
        public DecryptionInfo(
            bool hasDecryptionFailed,
            List<string> decryptedPaths,
            string message = null)
        {
            this.HasDecryptionFailed = hasDecryptionFailed;
            this.DecyptedPaths = decryptedPaths;
            this.ExceptionMessage = message;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DecryptionInfo"/> class.
        /// </summary>
        /// <param name="streamPriorToDecryption"></param>
        /// <param name="message"></param>
        public DecryptionInfo(
            Stream streamPriorToDecryption,
            string message)
        {
            this.HasDecryptionFailed = true;
            this.ExceptionMessage = message;
            this.StreamPriorToDecryption = streamPriorToDecryption;
        }

        /// <summary>
        /// Indicates if decryption failed.
        /// </summary>
        public bool HasDecryptionFailed { get; }

        /// <summary>
        /// List of JSON paths that were decrypted.
        /// </summary>
        public List<string> DecyptedPaths { get; }

        /// <summary>
        /// Exception message encountered during attempted decryption.
        /// Populated only if hasDecryptionFailed is set to true.
        /// </summary>
        public string ExceptionMessage { get; }

        /// <summary>
        /// Stream content without decryption
        /// </summary>
        public Stream StreamPriorToDecryption { get; }
    }
}