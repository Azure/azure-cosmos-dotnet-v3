//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    /// <summary>
    /// Algorithms for use with client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public static class MdeEncryptionAlgorithm
    {
        /// <summary>
        /// Authenticated Encryption algorithm supported by Microsoft Data Encryption Cryptography library
        /// </summary>
        public const string MdeAEAes256CbcHmacSha256 = "AEAD_AES_256_CBC_HMAC_SHA256";
    }
}
