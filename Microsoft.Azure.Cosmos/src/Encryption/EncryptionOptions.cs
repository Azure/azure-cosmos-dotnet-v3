//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;

    /// <summary>
    /// Options around encryption / decryption of data.
    /// See <see href="tbd"/> for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        class EncryptionOptions
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
        /// Example of a path specification: /sensitive
        /// </summary>
        /// <remarks> 
        /// Paths that are not found in the item are ignored. 
        /// Paths should not overlap, eg. passing both /a and /a/b is not valid. 
        /// Array index specifications are not honored. 
        /// </remarks> 
        public List<string> PathsToEncrypt { get; set; }
    }
}