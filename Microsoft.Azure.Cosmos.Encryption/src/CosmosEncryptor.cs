//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides the default implementation for client-side encryption for Cosmos DB.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public sealed class CosmosEncryptor : Encryptor, IDisposable
    {
        private bool isDisposed = false;

        /// <summary>
        /// Container for data encryption keys.
        /// </summary>
        public DataEncryptionKeyProvider DataEncryptionKeyProvider { get; }

        /// <summary>
        /// Initializes a new instance of Cosmos Encryptor.
        /// </summary>
        /// <param name="dataEncryptionKeyProvider"></param>
        public CosmosEncryptor(DataEncryptionKeyProvider dataEncryptionKeyProvider)
        {
            this.DataEncryptionKeyProvider = dataEncryptionKeyProvider;
        }

        /// <inheritdoc/>
        public override async Task<byte[]> DecryptAsync(
            byte[] cipherText,
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            CancellationToken cancellationToken = default)
        {
            this.ThrowIfDisposed();

            DataEncryptionKey dek = await this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync(
                dataEncryptionKeyId,
                encryptionAlgorithm,
                cancellationToken);

            if (dek == null)
            {
                throw new InvalidOperationException($"Null {nameof(DataEncryptionKey)} returned from {nameof(DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync)}.");
            }

            return dek.DecryptData(cipherText);
        }

        /// <inheritdoc/>
        public override async Task<byte[]> EncryptAsync(
            byte[] plainText,
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            CancellationToken cancellationToken = default)
        {
            this.ThrowIfDisposed();

            DataEncryptionKey dek = await this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync(
                dataEncryptionKeyId,
                encryptionAlgorithm,
                cancellationToken);

            if (dek == null)
            {
                throw new InvalidOperationException($"Null {nameof(DataEncryptionKey)} returned from {nameof(DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync)}.");
            }

            return dek.EncryptData(plainText);
        }

        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(nameof(CosmosEncryptor));
            }
        }

        private void Dispose(bool disposing)
        {
            if (disposing && !this.isDisposed)
            {
                if (this.DataEncryptionKeyProvider is IDisposable dataEncryptionKeyProvider)
                {
                    dataEncryptionKeyProvider.Dispose();
                }

                this.isDisposed = true;
            }
        }

        /// <summary>
        /// Dispose of unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
        }
    }
}
