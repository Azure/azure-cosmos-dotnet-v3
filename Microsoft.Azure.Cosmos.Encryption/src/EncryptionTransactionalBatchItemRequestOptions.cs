//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <summary>
    /// The <see cref="TransactionalBatchItemRequestOptions"/> that allows to specify options for encryption / decryption.
    /// </summary>
    public class EncryptionTransactionalBatchItemRequestOptions : TransactionalBatchItemRequestOptions
    {
        /// <summary>
        /// Options to be provided for encryption of data.
        /// </summary>
        public EncryptionOptions EncryptionOption { get; set; }

        /// <summary>
        /// Delegate method that will be invoked (if configured) in case of decryption failure.
        /// </summary>
        /// <remarks>
        /// If DecryptionResultHandler is not configured, we throw exception.
        /// If DecryptionResultHandler is configured, we invoke the delegate method and return the encrypted document as is (without decryption) in case of failure. 
        /// </remarks>
        public Action<DecryptionResult> DecryptionResultHandler { get; set; }
    }
}
