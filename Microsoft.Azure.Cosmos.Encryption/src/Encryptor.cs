//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstraction for performing client-side encryption.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public abstract class Encryptor : IDisposable
    {
        /// <summary>
        /// Encrypts the plainText using the key and algorithm provided.
        /// </summary>
        /// <param name="plainText"></param>
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

        /// <summary>
        /// Disposes the disposable members.
        /// </summary>
        /// <param name="disposing">Indicates whether to dispose managed resources or not.</param>
        protected abstract void Dispose(bool disposing);

        /// <summary>
        /// Disposes the current <see cref="Encryptor"/>.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
        }
    }
}
