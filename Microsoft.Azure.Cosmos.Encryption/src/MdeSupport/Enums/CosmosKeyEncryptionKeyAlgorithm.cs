//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    /// <summary>
    /// Represents the encryption algorithms supported for key encryption.
    /// </summary>
    public static class CosmosKeyEncryptionKeyAlgorithm
    {
        /// <summary>
        /// RSA public key cryptography algorithm with Optimal Asymmetric Encryption Padding (OAEP) padding.
        /// </summary>
        public const string RsaOaep = "RSA_OAEP";
    }
}
