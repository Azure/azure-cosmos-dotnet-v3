//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    internal static class Constants
    {
        public const string DocumentsResourcePropertyName = "Documents";

        /// <summary>
        /// MDE(Microsoft.Data.Encryption) Randomized AEAD_AES_256_CBC_HMAC_SHA256 Algorithm.
        /// As described <see href="http://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05">here</see>.
        /// </summary>
        public const string MdeAeadAes256CbcHmac256Randomized = "MdeAeadAes256CbcHmac256Randomized";
    }
}