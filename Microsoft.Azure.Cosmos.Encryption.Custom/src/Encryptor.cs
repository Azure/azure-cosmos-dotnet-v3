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
        /// Encrypts the plainText using the key and algorithm provided.
        /// </summary>
        /// <param name="plainText">Plain text.</param>
        /// <param name="plainTextOffset">Offset in the plainText array at which to begin using data from.</param>
        /// <param name="plainTextLength">Number of bytes in the plainText array to use as input.</param>
        /// <param name="output">Output buffer to write the encrypted data to.</param>
        /// <param name="outputOffset">Offset in the output array at which to begin writing data to.</param>
        /// <param name="dataEncryptionKeyId">Identifier of the data encryption key.</param>
        /// <param name="encryptionAlgorithm">Identifier for the encryption algorithm.</param>
        /// <param name="cancellationToken">Token for cancellation.</param>
        /// <returns>Cipher text.</returns>
        public abstract Task<int> EncryptAsync(
            byte[] plainText,
            int plainTextOffset,
            int plainTextLength,
            byte[] output,
            int outputOffset,
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieve Data Encryption Key.
        /// </summary>
        /// <param name="dataEncryptionKeyId">Identifier of the data encryption key.</param>
        /// <param name="encryptionAlgorithm">Identifier of the encryption algorithm.</param>
        /// <param name="cancellationToken">Token for cancellation.</param>
        /// <returns>Data Encryption Key</returns>
        public abstract Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculate size of input after encryption.
        /// </summary>
        /// <param name="plainTextLength">Input data size.</param>
        /// <param name="dataEncryptionKeyId">Identifier of the data encryption key.</param>
        /// <param name="encryptionAlgorithm">Identifier for the encryption algorithm.</param>
        /// <param name="dataEncryptionKey">Data Encryption Key used.</param>
        /// <param name="cancellationToken">Token for cancellation.</param>
        /// <returns>Size of input when encrypted.</returns>
        public abstract Task<int> GetEncryptBytesCountAsync(
            int plainTextLength,
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

        /// <summary>
        /// Decrypts the cipherText using the key and algorithm provided.
        /// </summary>
        /// <param name="cipherText">Ciphertext to be decrypted.</param>
        /// <param name="cipherTextOffset">Offset in the cipherText array at which to begin using data from.</param>
        /// <param name="cipherTextLength">Number of bytes in the cipherText array to use as input.</param>
        /// <param name="output">Output buffer to write the decrypted data to.</param>
        /// <param name="outputOffset">Offset in the output array at which to begin writing data to.</param>
        /// <param name="dataEncryptionKeyId">Identifier of the data encryption key.</param>
        /// <param name="encryptionAlgorithm">Identifier for the encryption algorithm.</param>
        /// <param name="cancellationToken">Token for cancellation.</param>
        /// <returns>Plain text.</returns>
        public abstract Task<int> DecryptAsync(
            byte[] cipherText,
            int cipherTextOffset,
            int cipherTextLength,
            byte[] output,
            int outputOffset,
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculate upper bound size of the input after decryption.
        /// </summary>
        /// <param name="cipherTextLength">Input data size.</param>
        /// <param name="dataEncryptionKeyId">Identifier of the data encryption key.</param>
        /// <param name="encryptionAlgorithm">Identifier for the encryption algorithm.</param>
        /// <param name="cancellationToken">Token for cancellation.</param>
        /// <returns>Upper bound size of the input when decrypted.</returns>
        public abstract Task<int> GetDecryptBytesCountAsync(
            int cipherTextLength,
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            CancellationToken cancellationToken = default);
    }
}
