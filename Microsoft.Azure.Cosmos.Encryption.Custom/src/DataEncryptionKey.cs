//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;

    /// <summary>
    /// Abstraction for a data encryption key for use in client-side encryption.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public abstract class DataEncryptionKey
    {
        private bool? providesEncryptByteCount;

        /// <summary>
        /// Gets raw key bytes of the data encryption key.
        /// </summary>
        public abstract byte[] RawKey { get; }

        /// <summary>
        /// Gets a value indicating whether this instance overrides <see cref="GetEncryptByteCount"/>
        /// and therefore supports the SDK's buffer-based encryption path. Implementations written
        /// against earlier package versions (array-based members only) are routed through
        /// <see cref="EncryptData(byte[])"/>/<see cref="DecryptData(byte[])"/> instead.
        /// </summary>
        internal bool ProvidesEncryptByteCount()
        {
            this.providesEncryptByteCount ??= this.GetType()
                .GetMethod(nameof(this.GetEncryptByteCount), new[] { typeof(int) })?
                .DeclaringType != typeof(DataEncryptionKey);

            return this.providesEncryptByteCount.Value;
        }

        /// <summary>
        /// Gets Encryption algorithm to be used with this data encryption key.
        /// </summary>
        public abstract string EncryptionAlgorithm { get; }

        /// <summary>
        /// Encrypts the plainText with a data encryption key.
        /// </summary>
        /// <param name="plainText">Plain text value to be encrypted.</param>
        /// <returns>Encrypted value.</returns>
        public abstract byte[] EncryptData(byte[] plainText);

        /// <summary>
        /// Encrypts the plainText with a data encryption key.
        /// </summary>
        /// <param name="plainText">Plain text value to be encrypted.</param>
        /// <param name="plainTextOffset">Offset in the plainText array at which to begin using data from.</param>
        /// <param name="plainTextLength">Number of bytes in the plainText array to use as input.</param>
        /// <param name="output">Output buffer to write the encrypted data to.</param>
        /// <param name="outputOffset">Offset in the output array at which to begin writing data to.</param>
        /// <returns>Number of encrypted bytes written to <paramref name="output"/>.</returns>
        /// <remarks>
        /// The default implementation delegates to <see cref="EncryptData(byte[])"/>, copying the
        /// input and output. Override for an allocation-free implementation.
        /// </remarks>
        public virtual int EncryptData(byte[] plainText, int plainTextOffset, int plainTextLength, byte[] output, int outputOffset)
        {
            ArgumentValidation.ThrowIfNull(plainText);
            ArgumentValidation.ThrowIfNull(output);

            byte[] input = new byte[plainTextLength];
            Buffer.BlockCopy(plainText, plainTextOffset, input, 0, plainTextLength);

            byte[] cipherText = this.EncryptData(input) ?? throw new InvalidOperationException($"{nameof(this.EncryptData)} returned null cipherText.");
            Buffer.BlockCopy(cipherText, 0, output, outputOffset, cipherText.Length);
            return cipherText.Length;
        }

        /// <summary>
        /// Calculate size of input after encryption.
        /// </summary>
        /// <param name="plainTextLength">Input data size.</param>
        /// <returns>Size of input when encrypted.</returns>
        /// <remarks>
        /// The default implementation throws <see cref="NotSupportedException"/>; the exact
        /// ciphertext size cannot be predicted for an arbitrary algorithm. Override this member
        /// (together with the buffer-based <see cref="EncryptData(byte[], int, int, byte[], int)"/>)
        /// to enable the SDK's buffer-based encryption paths.
        /// </remarks>
        public virtual int GetEncryptByteCount(int plainTextLength)
        {
            throw new NotSupportedException($"This {nameof(DataEncryptionKey)} implementation does not support buffer-based encryption. Override {nameof(this.GetEncryptByteCount)} to enable it.");
        }

        /// <summary>
        /// Decrypts the cipherText with a data encryption key.
        /// </summary>
        /// <param name="cipherText">Ciphertext value to be decrypted.</param>
        /// <returns>Plain text.</returns>
        public abstract byte[] DecryptData(byte[] cipherText);

        /// <summary>
        /// Decrypts the cipherText with a data encryption key.
        /// </summary>
        /// <param name="cipherText">Ciphertext value to be decrypted.</param>
        /// <param name="cipherTextOffset">Offset in the cipherText array at which to begin using data from.</param>
        /// <param name="cipherTextLength">Number of bytes in the cipherText array to use as input.</param>
        /// <param name="output">Output buffer to write the decrypted data to.</param>
        /// <param name="outputOffset">Offset in the output array at which to begin writing data to.</param>
        /// <returns>Number of decrypted bytes written to <paramref name="output"/>.</returns>
        /// <remarks>
        /// The default implementation delegates to <see cref="DecryptData(byte[])"/>, copying the
        /// input and output. Override for an allocation-free implementation.
        /// </remarks>
        public virtual int DecryptData(byte[] cipherText, int cipherTextOffset, int cipherTextLength, byte[] output, int outputOffset)
        {
            ArgumentValidation.ThrowIfNull(cipherText);
            ArgumentValidation.ThrowIfNull(output);

            byte[] input = new byte[cipherTextLength];
            Buffer.BlockCopy(cipherText, cipherTextOffset, input, 0, cipherTextLength);

            byte[] plainText = this.DecryptData(input) ?? throw new InvalidOperationException($"{nameof(this.DecryptData)} returned null plainText.");
            Buffer.BlockCopy(plainText, 0, output, outputOffset, plainText.Length);
            return plainText.Length;
        }

        /// <summary>
        /// Calculate upper bound size of the input after decryption.
        /// </summary>
        /// <param name="cipherTextLength">Input data size.</param>
        /// <returns>Upper bound size of the input when decrypted.</returns>
        /// <remarks>
        /// The default implementation returns <paramref name="cipherTextLength"/>, a safe upper
        /// bound for non-compressing ciphers (plaintext never exceeds ciphertext). Override for an
        /// exact bound.
        /// </remarks>
        public virtual int GetDecryptByteCount(int cipherTextLength)
        {
            return cipherTextLength;
        }

        /// <summary>
        /// Generates raw data encryption key bytes suitable for use with the provided encryption algorithm.
        /// </summary>
        /// <param name="encryptionAlgorithm">Encryption algorithm the returned key is intended to be used with.</param>
        /// <returns>New instance of data encryption key.</returns>
        public static byte[] Generate(string encryptionAlgorithm)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (!string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized))
            {
                throw new ArgumentException($"Encryption algorithm not supported: {encryptionAlgorithm}. Supported Algorithm is '{CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized}'", nameof(encryptionAlgorithm));
            }
#pragma warning restore CS0618 // Type or member is obsolete

            byte[] rawKey = new byte[32];
            SecurityUtility.GenerateRandomBytes(rawKey);
            return rawKey;
        }

        /// <summary>
        /// Creates a new instance of data encryption key given the raw key bytes
        /// suitable for use with the provided encryption algorithm.
        /// </summary>
        /// <param name="rawKey">Raw key bytes.</param>
        /// <param name="encryptionAlgorithm">Encryption algorithm the returned key is intended to be used with.</param>
        /// <returns>New instance of data encryption key.</returns>
        public static DataEncryptionKey Create(
            byte[] rawKey,
            string encryptionAlgorithm)
        {
            ArgumentValidation.ThrowIfNull(rawKey);

#pragma warning disable CS0618 // Type or member is obsolete
            if (!string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized))
            {
                throw new ArgumentException($"Encryption algorithm not supported: {encryptionAlgorithm}. Supported Algorithm is '{CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized}'", nameof(encryptionAlgorithm));
            }
#pragma warning restore CS0618 // Type or member is obsolete

            AeadAes256CbcHmac256EncryptionKey aeKey = new (rawKey, AeadAes256CbcHmac256Algorithm.AlgorithmNameConstant);
            return new AeadAes256CbcHmac256Algorithm(aeKey, EncryptionType.Randomized, algorithmVersion: 1);
        }
    }
}