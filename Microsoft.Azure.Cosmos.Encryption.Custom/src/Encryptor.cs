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
        private bool? providesDataEncryptionKeyAccess;

        /// <summary>
        /// Gets a value indicating whether this instance overrides <see cref="GetEncryptionKeyAsync"/>
        /// and can therefore hand the SDK a <see cref="DataEncryptionKey"/> directly. Implementations
        /// that do not (e.g. custom encryptors written against earlier package versions) are routed
        /// through their <see cref="EncryptAsync"/>/<see cref="DecryptAsync"/> overrides instead.
        /// </summary>
        internal bool ProvidesDataEncryptionKeyAccess()
        {
            this.providesDataEncryptionKeyAccess ??= this.GetType()
                .GetMethod(
                    nameof(this.GetEncryptionKeyAsync),
                    new[] { typeof(string), typeof(string), typeof(CancellationToken) })?
                .DeclaringType != typeof(Encryptor);

            return this.providesDataEncryptionKeyAccess.Value;
        }

        /// <summary>
        /// Retrieve Data Encryption Key.
        /// </summary>
        /// <param name="dataEncryptionKeyId">Identifier of the data encryption key.</param>
        /// <param name="encryptionAlgorithm">Identifier of the encryption algorithm.</param>
        /// <param name="cancellationToken">Token for cancellation.</param>
        /// <returns>Data Encryption Key</returns>
        /// <remarks>
        /// The default implementation throws <see cref="System.NotSupportedException"/>. Implementations that
        /// do not override this member are transparently routed through <see cref="EncryptAsync"/> and
        /// <see cref="DecryptAsync"/> instead, preserving the behavior of custom encryptors written
        /// against earlier versions of this package. Override this member to grant the SDK direct
        /// access to the <see cref="DataEncryptionKey"/>, enabling buffer-based (lower-allocation)
        /// encryption paths.
        /// </remarks>
        public virtual Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
        {
            throw new System.NotSupportedException($"This {nameof(Encryptor)} implementation does not provide direct {nameof(DataEncryptionKey)} access. Override {nameof(this.GetEncryptionKeyAsync)} to enable it.");
        }

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
