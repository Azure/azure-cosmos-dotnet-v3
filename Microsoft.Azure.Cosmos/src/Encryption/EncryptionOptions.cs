//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;

    /// <summary>
    /// Options around encryption / decryption of data.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        class EncryptionOptions
    {
        /// <summary>
        /// Reference to encryption key to be used for encryption of data in the request payload.
        /// The key must already be created using Database.CreateDataEncryptionKeyAsync
        /// before using it in encryption options.
        /// </summary>
        public DataEncryptionKey DataEncryptionKey { get; set; }

        /// <summary>
        /// For the request payload, list of JSON paths to encrypt.
        /// Only top level paths are supported.
        /// Example of a path specification: /sensitive
        /// </summary>
        public List<string> PathsToEncrypt { get; set; }
    }
}