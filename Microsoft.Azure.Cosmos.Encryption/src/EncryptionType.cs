//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    /// <summary>
<<<<<<<< HEAD:Microsoft.Azure.Cosmos.Encryption/src/MdeSupport/EncryptionType.cs
    /// Represents the encryption algorithms supported for data encryption.
    /// </summary>
    /// <summary>
    /// The type of data encryption.
    /// </summary>
    /// <remarks>
    /// The two encryption types are Deterministic and Randomized.
    /// Deterministic encryption always generates the same encrypted value for any given plain text value.
    /// Randomized encryption uses a method that encrypts data in a less predictable manner. Randomized encryption is more secure.
========
    /// Encryption types supported for data encryption.
    /// </summary>
    /// <remarks>
    /// See <see href="https://aka.ms/CosmosClientEncryption">client-side encryption documentation</see> for more details.
>>>>>>>> master:Microsoft.Azure.Cosmos.Encryption/src/EncryptionType.cs
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
