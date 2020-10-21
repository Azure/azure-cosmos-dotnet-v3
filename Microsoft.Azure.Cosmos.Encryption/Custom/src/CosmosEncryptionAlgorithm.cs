//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    /// <summary>
    /// Algorithms for use with client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public static class CosmosEncryptionAlgorithm
    {
        /// <summary>
        /// Authenticated Encryption algorithm based on https://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05
        /// </summary>
        public const string AEAes256CbcHmacSha256Randomized = "AEAes256CbcHmacSha256Randomized";
    }
}
