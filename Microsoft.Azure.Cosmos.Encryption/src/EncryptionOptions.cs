//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Options for encryption of data.
    /// </summary>
    public class EncryptionOptions
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
        /// is the only one supported and is represented by "AEAes256CbcHmacSha256Randomized" value.
        /// </remarks>
        public string EncryptionAlgorithm { get; set; }

        /// <summary>
        /// Serializer for DataType
        /// </summary>
        public ISerializer Serializer { get; set; }

        /// <summary>
        /// Gets or Sets the DataType for the Property
        /// </summary>
        public Type PropertyDataType { get; set; }

        /// <summary>
        /// Gets or sets for the request payload, list of JSON paths to encrypt.
        /// Only top level paths are supported.
        /// Example of a path specification: /sensitive
        /// </summary>
        public IEnumerable<string> PathsToEncrypt { get; set; }
    }
}