//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    /// <summary>
    /// Represents the encryption algorithms supported for data encryption.
    /// </summary>
    public static class CosmosDataEncryptionKeyAlgorithm
    {
        /// <summary>
        /// Represents the authenticated encryption algorithm with associated data as described in
        /// http://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05.
        /// </summary>
        public const string AeadAes256CbcHmacSha256 = "AEAD_AES_256_CBC_HMAC_SHA256";
    }
}
