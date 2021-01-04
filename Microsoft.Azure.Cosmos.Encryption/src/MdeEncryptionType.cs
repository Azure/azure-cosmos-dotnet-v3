//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    /// <summary>
    /// Algorithms for use with client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public static class MdeEncryptionType
    {
        /// <summary>
        /// Randomized encryption uses a method that encrypts data in a less predictable manner. Randomized encryption is more secure.
        /// </summary>
        public const string Randomized = "Randomized";

        /// <summary>
        /// Deterministic encryption always generates the same encrypted value for any given plain text value.
        /// </summary>
        public const string Deterministic = "Deterministic";
    }
}
