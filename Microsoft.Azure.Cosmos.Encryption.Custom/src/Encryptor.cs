//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstraction for performing client-side encryption.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public abstract class Encryptor
    {
        /// <summary>
        /// Retrieve Data Encryption Key.
        /// </summary>
        /// <param name="dataEncryptionKeyId">Identifier of the data encryption key.</param>
        /// <param name="encryptionAlgorithm">Identifier of the encryption algorithm.</param>
        /// <param name="cancellationToken">Token for cancellation.</param>
        /// <returns>Data Encryption Key</returns>
        public abstract Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default);

        /// <summary>
        /// Encrypts the plainText using the key and algorithm provided.
        /// </summary>
        /// <param name="plainText">Plain text.</param>
        /// <param name="dataEncryptionKeyId">Identifier of the data encryption key.</param>
        /// <param name="encryptionAlgorithm">Identifier for the encryption algorithm.</param>
        /// <param name="cancellationToken">Token for cancellation.</param>
        /// <returns>Cipher text.</returns>
        public abstract Task<byte[]> EncryptAsync(
            byte[] plainText,
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypts the cipherText using the key and algorithm provided.
        /// </summary>
        /// <param name="cipherText">Ciphertext to be decrypted.</param>
        /// <param name="dataEncryptionKeyId">Identifier of the data encryption key.</param>
        /// <param name="encryptionAlgorithm">Identifier for the encryption algorithm.</param>
        /// <param name="cancellationToken">Token for cancellation.</param>
        /// <returns>Plain text.</returns>
        public abstract Task<byte[]> DecryptAsync(
            byte[] cipherText,
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            CancellationToken cancellationToken = default);
    }
}
