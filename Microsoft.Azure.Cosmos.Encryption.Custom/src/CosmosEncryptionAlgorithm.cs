//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Algorithms for use with client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public static class CosmosEncryptionAlgorithm
    {
        /// <summary>
        /// Authenticated Encryption algorithm based on https://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05
        /// </summary>
        [Obsolete("Please use MdeAeadAes256CbcHmac256Randomized.")]
        public const string AEAes256CbcHmacSha256Randomized = "AEAes256CbcHmacSha256Randomized";

        /// <summary>
        /// MDE(Microsoft.Data.Encryption) Randomized AEAD_AES_256_CBC_HMAC_SHA256 Algorithm.
        /// As described <see href="http://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05">here</see>.
        /// </summary>
        public const string MdeAeadAes256CbcHmac256Randomized = "MdeAeadAes256CbcHmac256Randomized";

        /// <summary>
        /// Verify if the Encryption Algorithm is supported by Cosmos.
        /// </summary>
        /// <param name="encryptionAlgorithm"> Encryption Algorithm. </param>
        /// <returns> Returns True if the Algorithm is supported. </returns>
        internal static bool VerifyIfSupportedAlgorithm(string encryptionAlgorithm)
        {
            if (!string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized) &&
                !string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized))
            {
                return false;
            }

            return true;
        }
    }
}
