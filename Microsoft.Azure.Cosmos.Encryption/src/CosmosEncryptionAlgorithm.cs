//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
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

        internal static bool VerifyIfKeyIsCompatible(string encOptions_encryptionAlgorithm, string dek_encryptionAlgorithm)
        {
            // List of DEKs compatible with AEAes256CbcHmacSha256Randomized Algorithm.
            IReadOnlyDictionary<string, bool> legacyEncAlgoSupportedDEK = new Dictionary<string, bool>
            {
                { CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized, true },
                { CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized, false },
            };

            // List of DEKs compatible with MdeAeadAes256CbcHmac256Randomized Algorithm.
            IReadOnlyDictionary<string, bool> mdeEncAlgoSupportedDEK = new Dictionary<string, bool>
            {
                { CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized, true },
                { CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized, true },
            };

            // unlikely
            if (string.IsNullOrEmpty(encOptions_encryptionAlgorithm) || string.IsNullOrEmpty(dek_encryptionAlgorithm))
            {
                return false;
            }

            if (string.Equals(encOptions_encryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized))
            {
                legacyEncAlgoSupportedDEK.TryGetValue(dek_encryptionAlgorithm, out bool ifsupported);
                return ifsupported;
            }

            if (string.Equals(encOptions_encryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized))
            {
                mdeEncAlgoSupportedDEK.TryGetValue(dek_encryptionAlgorithm, out bool ifsupported);
                return ifsupported;
            }

            // unsupported Encryption algorithm.
            return false;
        }
    }
}
