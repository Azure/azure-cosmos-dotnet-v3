//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <summary>
    /// Algorithms for use with client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public static class CosmosEncryptionAlgorithm
    {
        /// <summary>
        /// Authenticated Encryption algorithm based on https://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05
        /// </summary>
        public const string AEAes256CbcHmacSha256Randomized = "AEAes256CbcHmacSha256Randomized";

        /// <summary>
        /// MDE Randomized Encryption Algorithm
        /// </summary>
        public const string MdeAEAes256CbcHmacSha256Randomized = "MdeAEAes256CbcHmacSha256Randomized";

        /// <summary>
        /// Verify If the Encryption Algorithm is supported by Cosmos.
        /// </summary>
        /// <param name="encryptionAlgorithm"> Encryption Algorithm. </param>
        /// <returns> Returns True if the Algorithm is supported. </returns>
        internal static bool VerifyIfSupportedAlgorithm(string encryptionAlgorithm)
        {
            if (!string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized) &&
                 !string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAEAes256CbcHmacSha256Randomized))
            {
                return false;
            }

            return true;
        }
    }
}
