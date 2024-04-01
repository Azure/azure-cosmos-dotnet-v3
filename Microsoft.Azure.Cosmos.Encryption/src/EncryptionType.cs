//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    /// <summary>
    /// Encryption types supported for data encryption.
    /// </summary>
    /// <remarks>
    /// See <see href="https://aka.ms/CosmosClientEncryption">client-side encryption documentation</see> for more details.
    /// </remarks>
    public static class EncryptionType
    {
        /// <summary>
        /// Deterministic encryption always generates the same encrypted value for a given plain text value.
        /// </summary>
        public const string Deterministic = "Deterministic";

        /// <summary>
        /// Randomized encryption uses a method that encrypts data in a less predictable manner than Deterministic.
        /// </summary>
        public const string Randomized = "Randomized";
    }
}
