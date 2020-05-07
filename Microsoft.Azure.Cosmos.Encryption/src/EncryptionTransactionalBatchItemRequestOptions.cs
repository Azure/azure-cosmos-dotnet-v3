//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The <see cref="TransactionalBatchItemRequestOptions"/> that allows to specify options for encryption / decryption.
    /// </summary>
    public class EncryptionTransactionalBatchItemRequestOptions : TransactionalBatchItemRequestOptions
    {
        /// <summary>
        /// Identifier of the data encryption key to be used for encrypting the data in the request payload.
        /// The data encryption key must be suitable for use with the <see cref="EncryptionAlgorithm"/> provided.
        /// </summary>
        /// <remarks>
        /// The <see cref="Encryptor"/> configured on the client is used to retrieve the actual data encryption key.
        /// </remarks>
        public string DataEncryptionKeyId { get; set; }

        /// <summary>
        /// Algorithm to be used for encrypting the data in the request payload.
        /// </summary>
        /// <remarks>
        /// Authenticated Encryption algorithm based on https://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05
        /// is only supported and is represented by "AEAes256CbcHmacSha256Randomized" value.
        /// </remarks>
        public string EncryptionAlgorithm { get; set; }

        /// <summary>
        /// For the request payload, list of JSON paths to encrypt.
        /// Only top level paths are supported.
        /// Example of a path specification: /sensitive
        /// </summary>
        public IEnumerable<string> PathsToEncrypt { get; set; }

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
