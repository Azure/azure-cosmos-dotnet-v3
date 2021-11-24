//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    /// <summary>
    /// Represents the encryption algorithms supported for data encryption.
    /// </summary>
    /// <summary>
    /// The type of data encryption.
    /// </summary>
    /// <remarks>
    /// The three encryption types are Plaintext Deterministic and Randomized. Plaintext unencrypted data.
    /// Deterministic encryption always generates the same encrypted value for any given plain text value.
    /// Randomized encryption uses a method that encrypts data in a less predictable manner. Randomized encryption is more secure.
    /// </remarks>
    public static class CosmosEncryptionType
    {
        /// <summary>
        /// Deterministic encryption always generates the same encrypted value for any given plain text value.
        /// </summary>
        public const string Deterministic = "Deterministic";

        /// <summary>
        /// Randomized encryption uses a method that encrypts data in a less predictable manner. Randomized encryption is more secure.
        /// </summary>
        public const string Randomized = "Randomized";
    }
}
