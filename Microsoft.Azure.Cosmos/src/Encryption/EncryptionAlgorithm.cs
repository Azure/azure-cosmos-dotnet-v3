//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Abstract base class for all encryption algorithms.
    /// </summary>
    internal abstract class EncryptionAlgorithm
    {
        internal abstract string AlgorithmName { get; }

        /// <summary>
        /// Encrypts the plainText with a data encryption key.
        /// </summary>
        /// <param name="plainText">Plain text value to be encrypted.</param>
        /// <returns>Encrypted value.</returns>
        internal abstract byte[] EncryptData(byte[] plainText);

        /// <summary>
        /// Decrypts the cipherText with a data encryption key.
        /// </summary>
        /// <param name="cipherText">Ciphertext value to be decrypted.</param>
        /// <returns>Plain text.</returns>
        internal abstract byte[] DecryptData(byte[] cipherText);
    }
}
