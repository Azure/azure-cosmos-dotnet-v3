//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography;

#pragma warning disable SYSLIB0021 // Type or member is obsolete

    /// <summary>
    /// This class implements authenticated encryption algorithm with associated data as described in
    /// http://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05 - specifically this implements
    /// AEAD_AES_256_CBC_HMAC_SHA256 algorithm.
    /// This (and AeadAes256CbcHmac256EncryptionKey) implementation for Cosmos DB is same as the existing
    /// SQL client implementation with StyleCop related changes - also, we restrict to randomized encryption to start with.
    /// </summary>
    internal class AeadAes256CbcHmac256Algorithm : DataEncryptionKey
    {
        internal const string AlgorithmNameConstant = @"AEAD_AES_256_CBC_HMAC_SHA256";

        /// <summary>
        /// Key size in bytes
        /// </summary>
        private const int KeySizeInBytes = AeadAes256CbcHmac256EncryptionKey.KeySize / 8;

        /// <summary>
        /// Authentication tag size in bytes
        /// </summary>
        private const int AuthenticationTagSizeInBytes = KeySizeInBytes;

        /// <summary>
        /// Block size in bytes. AES uses 16 byte blocks.
        /// </summary>
        private const int BlockSizeInBytes = 16;

        /// <summary>
        /// Size of Initialization Vector in bytes.
        /// </summary>
        private const int IvSizeInBytes = 16;

        /// <summary>
        /// Minimum Length of cipherText without authentication tag. This value is 1 (version byte) + 16 (IV) + 16 (minimum of 1 block of cipher Text)
        /// </summary>
        private const int MinimumCipherTextLengthInBytesNoAuthenticationTag = sizeof(byte) + BlockSizeInBytes + BlockSizeInBytes;

        /// <summary>
        /// Minimum Length of cipherText. This value is 1 (version byte) + 32 (authentication tag) + 16 (IV) + 16 (minimum of 1 block of cipher Text)
        /// </summary>
        private const int MinimumCipherTextLengthInBytesWithAuthenticationTag = MinimumCipherTextLengthInBytesNoAuthenticationTag + KeySizeInBytes;

        /// <summary>
        /// Cipher Mode. For this algorithm, we only use CBC mode.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1303:Const field names should begin with upper-case letter", Justification = "This code is borrowed and we may want to pull bug fixes if they happen on the original.")]
        private const CipherMode cipherMode = CipherMode.CBC;

        /// <summary>
        /// Padding mode. This algorithm uses PKCS7.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1303:Const field names should begin with upper-case letter", Justification = "This code is borrowed and we may want to pull bug fixes if they happen on the original.")]
        private const PaddingMode paddingMode = PaddingMode.PKCS7;

        /// <summary>
        /// Variable indicating whether this algorithm should work in Deterministic mode or Randomized mode.
        /// For deterministic encryption, we derive an IV from the plaintext data.
        /// For randomized encryption, we generate a cryptographically random IV.
        /// </summary>
        private readonly bool isDeterministic;

        /// <summary>
        /// Algorithm Version.
        /// </summary>
        private readonly byte algorithmVersion;

        /// <summary>
        /// Data Encryption Key. This has a root key and three derived keys.
        /// </summary>
        private readonly AeadAes256CbcHmac256EncryptionKey dataEncryptionKey;

        /// <summary>
        /// The pool of crypto providers to use for encrypt/decrypt operations.
        /// </summary>
        private readonly ConcurrentQueue<AesCryptoServiceProvider> cryptoProviderPool;

        /// <summary>
        /// Byte array with algorithm version used for authentication tag computation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1311:Static readonly fields should begin with upper-case letter", Justification = "This code is borrowed and we may want to pull bug fixes if they happen on the original.")]
        private static readonly byte[] version = new byte[] { 0x01 };

        /// <summary>
        /// Byte array with algorithm version size used for authentication tag computation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1311:Static readonly fields should begin with upper-case letter", Justification = "This code is borrowed and we may want to pull bug fixes if they happen on the original.")]
        private static readonly byte[] versionSize = new byte[] { sizeof(byte) };

        public override byte[] RawKey => this.dataEncryptionKey.RootKey;

#pragma warning disable CS0618 // Type or member is obsolete
        public override string EncryptionAlgorithm => CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized;
#pragma warning restore CS0618 // Type or member is obsolete

        /// <summary>
        /// Initializes a new instance of AeadAes256CbcHmac256Algorithm algorithm with a given key and encryption type
        /// </summary>
        /// <param name="encryptionKey">
        /// Root encryption key from which three other keys will be derived
        /// </param>
        /// <param name="encryptionType">Encryption Type, accepted values are Deterministic and Randomized.
        /// For Deterministic encryption, a synthetic IV will be genenrated during encryption
        /// For Randomized encryption, a random IV will be generated during encryption.
        /// </param>
        /// <param name="algorithmVersion">
        /// Algorithm version
        /// </param>
        internal AeadAes256CbcHmac256Algorithm(AeadAes256CbcHmac256EncryptionKey encryptionKey, EncryptionType encryptionType, byte algorithmVersion)
        {
            this.dataEncryptionKey = encryptionKey;
            this.algorithmVersion = algorithmVersion;

            version[0] = algorithmVersion;

            Debug.Assert(encryptionKey != null, "Null encryption key detected in AeadAes256CbcHmac256 algorithm");
            Debug.Assert(algorithmVersion == 0x01, "Unknown algorithm version passed to AeadAes256CbcHmac256");

            // Validate encryption type for this algorithm
            // This algorithm can only provide randomized or deterministic encryption types.
            // Right now, we support only randomized encryption for Cosmos DB client side encryption.
            Debug.Assert(encryptionType == EncryptionType.Randomized, "Invalid Encryption Type detected in AeadAes256CbcHmac256Algorithm");
            this.isDeterministic = false;

            this.cryptoProviderPool = new ConcurrentQueue<AesCryptoServiceProvider>();
        }

        /// <summary>
        /// Encryption Algorithm
        /// cell_iv = HMAC_SHA-2-256(iv_key, cell_data) truncated to 128 bits
        /// cell_ciphertext = AES-CBC-256(enc_key, cell_iv, cell_data) with PKCS7 padding.
        /// cell_tag = HMAC_SHA-2-256(mac_key, versionbyte + cell_iv + cell_ciphertext + versionbyte_length)
        /// cell_blob = versionbyte + cell_tag + cell_iv + cell_ciphertext
        /// </summary>
        /// <param name="plainText">Plaintext data to be encrypted</param>
        /// <returns>Returns the ciphertext corresponding to the plaintext.</returns>
        public override byte[] EncryptData(byte[] plainText)
        {
            return this.EncryptData(plainText, hasAuthenticationTag: true);
        }

        /// <summary>
        /// Encryption Algorithm
        /// cell_iv = HMAC_SHA-2-256(iv_key, cell_data) truncated to 128 bits
        /// cell_ciphertext = AES-CBC-256(enc_key, cell_iv, cell_data) with PKCS7 padding.
        /// cell_tag = HMAC_SHA-2-256(mac_key, versionbyte + cell_iv + cell_ciphertext + versionbyte_length)
        /// cell_blob = versionbyte + cell_tag + cell_iv + cell_ciphertext
        /// </summary>
        /// <returns>Returns the ciphertext corresponding to the plaintext.</returns>
        public override int EncryptData(byte[] plainText, int plainTextOffset, int plainTextLength, byte[] output, int outputOffset)
        {
            byte[] buffer = this.EncryptData(plainText.AsSpan(plainTextOffset, plainTextLength).ToArray());

            if (buffer.Length > output.Length - outputOffset)
            {
                throw new ArgumentOutOfRangeException($"Output buffer is shorter than required {buffer.Length} bytes.");
            }

            buffer.CopyTo(output, outputOffset);
            return buffer.Length;
        }

        /// <summary>
        /// Encryption Algorithm
        /// cell_iv = HMAC_SHA-2-256(iv_key, cell_data) truncated to 128 bits
        /// cell_ciphertext = AES-CBC-256(enc_key, cell_iv, cell_data) with PKCS7 padding.
        /// (optional) cell_tag = HMAC_SHA-2-256(mac_key, versionbyte + cell_iv + cell_ciphertext + versionbyte_length)
        /// cell_blob = versionbyte + [cell_tag] + cell_iv + cell_ciphertext
        /// </summary>
        /// <param name="plainText">Plaintext data to be encrypted</param>
        /// <param name="hasAuthenticationTag">Does the algorithm require authentication tag.</param>
        /// <returns>Returns the ciphertext corresponding to the plaintext.</returns>
        protected byte[] EncryptData(byte[] plainText, bool hasAuthenticationTag)
        {
            // Empty values get encrypted and decrypted properly for both Deterministic and Randomized encryptions.
            Debug.Assert(plainText != null);

            byte[] iv = new byte[BlockSizeInBytes];

            // Prepare IV
            // Should be 1 single block (16 bytes)
            if (this.isDeterministic)
            {
                SecurityUtility.GetHMACWithSHA256(plainText, this.dataEncryptionKey.IVKey, iv);
            }
            else
            {
                SecurityUtility.GenerateRandomBytes(iv);
            }

            int numBlocks = (plainText.Length / BlockSizeInBytes) + 1;

            // Final blob we return = version + HMAC + iv + cipherText
            const int hmacStartIndex = 1;
            int authenticationTagLen = hasAuthenticationTag ? AuthenticationTagSizeInBytes : 0;
            int ivStartIndex = hmacStartIndex + authenticationTagLen;
            int cipherStartIndex = ivStartIndex + BlockSizeInBytes; // this is where hmac starts.

            int outputBufSize = this.GetEncryptByteCount(plainText.Length) - (hasAuthenticationTag ? 0 : authenticationTagLen);
            byte[] outBuffer = new byte[outputBufSize];

            // Store the version and IV rightaway
            outBuffer[0] = this.algorithmVersion;
            Buffer.BlockCopy(iv, 0, outBuffer, ivStartIndex, iv.Length);

            // Try to get a provider from the pool.
            // If no provider is available, create a new one.
            if (!this.cryptoProviderPool.TryDequeue(out AesCryptoServiceProvider aesAlg))
            {
                aesAlg = new AesCryptoServiceProvider();

                try
                {
                    // Set various algorithm properties
                    aesAlg.Key = this.dataEncryptionKey.EncryptionKey;
                    aesAlg.Mode = cipherMode;
                    aesAlg.Padding = paddingMode;
                }
                catch (Exception)
                {
                    aesAlg?.Dispose();

                    throw;
                }
            }

            try
            {
                // Always set the IV since it changes from cell to cell.
                aesAlg.IV = iv;

                // Compute CipherText and authentication tag in a single pass
                using (ICryptoTransform encryptor = aesAlg.CreateEncryptor())
                {
                    Debug.Assert(encryptor.CanTransformMultipleBlocks, "AES Encryptor can transform multiple blocks");
                    int count = 0;
                    int cipherIndex = cipherStartIndex; // this is where cipherText starts
                    if (numBlocks > 1)
                    {
                        count = (numBlocks - 1) * BlockSizeInBytes;
                        cipherIndex += encryptor.TransformBlock(plainText, 0, count, outBuffer, cipherIndex);
                    }

                    byte[] buffTmp = encryptor.TransformFinalBlock(plainText, count, plainText.Length - count); // done encrypting
                    Buffer.BlockCopy(buffTmp, 0, outBuffer, cipherIndex, buffTmp.Length);
                    cipherIndex += buffTmp.Length;
                }

                if (hasAuthenticationTag)
                {
                    using (HMACSHA256 hmac = new (this.dataEncryptionKey.MACKey))
                    {
                        Debug.Assert(hmac.CanTransformMultipleBlocks, "HMAC can't transform multiple blocks");
                        hmac.TransformBlock(version, 0, version.Length, version, 0);
                        hmac.TransformBlock(iv, 0, iv.Length, iv, 0);

                        // Compute HMAC on final block
                        hmac.TransformBlock(outBuffer, cipherStartIndex, numBlocks * BlockSizeInBytes, outBuffer, cipherStartIndex);
                        hmac.TransformFinalBlock(versionSize, 0, versionSize.Length);
                        byte[] hash = hmac.Hash;
                        Debug.Assert(hash.Length >= authenticationTagLen, "Unexpected hash size");
                        Buffer.BlockCopy(hash, 0, outBuffer, hmacStartIndex, authenticationTagLen);
                    }
                }
            }
            finally
            {
                // Return the provider to the pool.
                this.cryptoProviderPool.Enqueue(aesAlg);
            }

            return outBuffer;
        }

        /// <summary>
        /// Decryption steps
        /// 1. Validate version byte
        /// 2. Validate Authentication tag
        /// 3. Decrypt the message
        /// </summary>
        public override byte[] DecryptData(byte[] cipherText)
        {
            return this.DecryptData(cipherText, hasAuthenticationTag: true);
        }

        /// <summary>
        /// Decryption steps
        /// 1. Validate version byte
        /// 2. (optional) Validate Authentication tag
        /// 3. Decrypt the message
        /// </summary>
        protected byte[] DecryptData(byte[] cipherText, bool hasAuthenticationTag)
        {
            Debug.Assert(cipherText != null);

            byte[] iv = new byte[BlockSizeInBytes];

            int minimumCipherTextLength = hasAuthenticationTag ? MinimumCipherTextLengthInBytesWithAuthenticationTag : MinimumCipherTextLengthInBytesNoAuthenticationTag;
            if (cipherText.Length < minimumCipherTextLength)
            {
                throw EncryptionExceptionFactory.InvalidCipherTextSize(cipherText.Length, minimumCipherTextLength);
            }

            // Validate the version byte
            int startIndex = 0;
            if (cipherText[startIndex] != this.algorithmVersion)
            {
                // Cipher text was computed with a different algorithm version than this.
                throw EncryptionExceptionFactory.InvalidAlgorithmVersion(cipherText[startIndex], this.algorithmVersion);
            }

            startIndex += 1;
            int authenticationTagOffset = 0;

            // Read authentication tag
            if (hasAuthenticationTag)
            {
                authenticationTagOffset = startIndex;
                startIndex += KeySizeInBytes; // authentication tag size is KeySizeInBytes
            }

            // Read cell IV
            Buffer.BlockCopy(cipherText, startIndex, iv, 0, iv.Length);
            startIndex += iv.Length;

            // Read encrypted text
            int cipherTextOffset = startIndex;
            int cipherTextCount = cipherText.Length - startIndex;

            if (hasAuthenticationTag)
            {
                // Compute authentication tag
                byte[] authenticationTag = this.PrepareAuthenticationTag(iv, cipherText, cipherTextOffset, cipherTextCount);
                if (!SecurityUtility.CompareBytes(authenticationTag, cipherText, authenticationTagOffset, authenticationTag.Length))
                {
                    // Potentially tampered data, throw an exception
                    throw EncryptionExceptionFactory.InvalidAuthenticationTag();
                }
            }

            // Decrypt the text and return
            return this.DecryptData(iv, cipherText, cipherTextOffset, cipherTextCount);
        }

        /// <summary>
        /// Decrypts plain text data using AES in CBC mode
        /// </summary>
        private byte[] DecryptData(byte[] iv, byte[] cipherText, int offset, int count)
        {
            Debug.Assert((iv != null) && (cipherText != null));
            Debug.Assert(offset > -1 && count > -1);
            Debug.Assert((count + offset) <= cipherText.Length);

            byte[] plainText;

            // Try to get a provider from the pool.
            // If no provider is available, create a new one.
            if (!this.cryptoProviderPool.TryDequeue(out AesCryptoServiceProvider aesAlg))
            {
                aesAlg = new AesCryptoServiceProvider();

                try
                {
                    // Set various algorithm properties
                    aesAlg.Key = this.dataEncryptionKey.EncryptionKey;
                    aesAlg.Mode = cipherMode;
                    aesAlg.Padding = paddingMode;
                }
                catch (Exception)
                {
                    aesAlg?.Dispose();

                    throw;
                }
            }

            try
            {
                // Always set the IV since it changes from cell to cell.
                aesAlg.IV = iv;

                // Create the streams used for decryption.
                using (MemoryStream msDecrypt = new ())
                {
                    // Create an encryptor to perform the stream transform.
                    using (ICryptoTransform decryptor = aesAlg.CreateDecryptor())
                    {
                        using (CryptoStream csDecrypt = new (msDecrypt, decryptor, CryptoStreamMode.Write))
                        {
                            // Decrypt the secret message and get the plain text data
                            csDecrypt.Write(cipherText, offset, count);
                            csDecrypt.FlushFinalBlock();
                            plainText = msDecrypt.ToArray();
                        }
                    }
                }
            }
            finally
            {
                // Return the provider to the pool.
                this.cryptoProviderPool.Enqueue(aesAlg);
            }

            return plainText;
        }

        /// <summary>
        /// Prepares an authentication tag.
        /// Authentication Tag = HMAC_SHA-2-256(mac_key, versionbyte + cell_iv + cell_ciphertext + versionbyte_length)
        /// </summary>
        private byte[] PrepareAuthenticationTag(byte[] iv, byte[] cipherText, int offset, int length)
        {
            Debug.Assert(cipherText != null);

            byte[] computedHash;
            byte[] authenticationTag = new byte[KeySizeInBytes];

            // Raw Tag Length:
            //              1 for the version byte
            //              1 block for IV (16 bytes)
            //              cipherText.Length
            //              1 byte for version byte length
            using (HMACSHA256 hmac = new (this.dataEncryptionKey.MACKey))
            {
                int retVal = 0;
                retVal = hmac.TransformBlock(version, 0, version.Length, version, 0);
                Debug.Assert(retVal == version.Length);
                retVal = hmac.TransformBlock(iv, 0, iv.Length, iv, 0);
                Debug.Assert(retVal == iv.Length);
                retVal = hmac.TransformBlock(cipherText, offset, length, cipherText, offset);
                Debug.Assert(retVal == length);
                hmac.TransformFinalBlock(versionSize, 0, versionSize.Length);
                computedHash = hmac.Hash;
            }

            Debug.Assert(computedHash.Length >= authenticationTag.Length);
            Buffer.BlockCopy(computedHash, 0, authenticationTag, 0, authenticationTag.Length);
            return authenticationTag;
        }

        public override int DecryptData(byte[] cipherText, int cipherTextOffset, int cipherTextLength, byte[] output, int outputOffset)
        {
            byte[] buffer = this.DecryptData(cipherText.AsSpan(cipherTextOffset, cipherTextLength).ToArray(), true);

            if (buffer.Length > output.Length - outputOffset)
            {
                throw new ArgumentOutOfRangeException($"Output buffer is shorter than required {buffer.Length} bytes");
            }

            buffer.CopyTo(output, outputOffset);
            return buffer.Length;
        }

        public override int GetEncryptByteCount(int plainTextLength)
        {
            // Output buffer size = size of VersionByte + Authentication Tag + IV + cipher Text blocks.
            return sizeof(byte) + AuthenticationTagSizeInBytes + IvSizeInBytes + GetCipherTextLength(plainTextLength);
        }

        public override int GetDecryptByteCount(int cipherTextLength)
        {
            int value = cipherTextLength - (sizeof(byte) + AuthenticationTagSizeInBytes + IvSizeInBytes);
            if (value < BlockSizeInBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(cipherTextLength), $"Cipher text length is too short.");
            }

            return value;
        }

        private static int GetCipherTextLength(int inputSize)
        {
            return ((inputSize / BlockSizeInBytes) + 1) * BlockSizeInBytes;
        }
    }

#pragma warning restore SYSLIB0021 // Type or member is obsolete
}