//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.Collections.Generic;

    /// <summary>
    /// Options for encryption of data.
    /// </summary>
    public sealed class EncryptionOptions
    {
        /// <summary>
        /// Gets or sets identifier of the data encryption key to be used for encrypting the data in the request payload.
        /// The data encryption key must be suitable for use with the <see cref="EncryptionAlgorithm"/> provided.
        /// </summary>
        /// <remarks>
        /// The <see cref="Encryptor"/> configured on the client is used to retrieve the actual data encryption key.
        /// </remarks>
        public string DataEncryptionKeyId { get; set; }

        /// <summary>
        /// Gets or sets algorithm to be used for encrypting the data in the request payload.
        /// </summary>
        /// <remarks>
        /// Authenticated Encryption algorithm based on https://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05
        /// is the only one supported and is represented by "AEAes256CbcHmacSha256Randomized" value.
        /// </remarks>
        public string EncryptionAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets list of JSON paths to encrypt on the payload.
        /// Only top level paths are supported.
        /// Example of a path specification: /sensitive
        /// </summary>
        public IEnumerable<string> PathsToEncrypt { get; set; }
    }
}