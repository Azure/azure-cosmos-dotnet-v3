//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;

    public class EncryptionItemRequestOptions : ItemRequestOptions
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
        public string EncryptionAlgorithm { get; set; }

        /// <summary>
        /// For the request payload, list of JSON paths to encrypt.
        /// Only top level paths are supported.
        /// Example of a path specification: /sensitive
        /// </summary>
        public List<string> PathsToEncrypt { get; set; }

        /// <summary>
        /// Delegate method that will be invoked (if configured) in case of decryption failure.
        /// </summary>
        /// <remarks>
        /// If DecryptionErrorHandler is not configured, we throw exception.
        /// If DecryptionErrorHandler is configured, we invoke the delegate method and return the encrypted document as is (without decryption) in case of failure. 
        /// </remarks>
        public Action<DecryptionErrorDetails> DecryptionErrorHandler { get; set; }
        
    }
}
