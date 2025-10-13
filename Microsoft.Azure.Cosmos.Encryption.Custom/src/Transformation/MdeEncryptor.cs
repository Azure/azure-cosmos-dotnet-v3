// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;

    internal class MdeEncryptor
    {
        internal virtual byte[] Encrypt(DataEncryptionKey encryptionKey, TypeMarker typeMarker, byte[] plainText, int plainTextLength)
        {
            int encryptedTextLength = encryptionKey.GetEncryptByteCount(plainTextLength) + 1;

            byte[] encryptedText = new byte[encryptedTextLength];

            encryptedText[0] = (byte)typeMarker;

            int encryptedLength = encryptionKey.EncryptData(
                plainText,
                plainTextOffset: 0,
                plainTextLength,
                encryptedText,
                outputOffset: 1);

            if (encryptedLength < 0)
            {
                throw new InvalidOperationException($"{nameof(DataEncryptionKey)} returned null cipherText from {nameof(DataEncryptionKey.EncryptData)}.");
            }

            return encryptedText;
        }

        internal virtual (byte[], int) Encrypt(DataEncryptionKey encryptionKey, TypeMarker typeMarker, byte[] plainText, int plainTextLength, ArrayPoolManager arrayPoolManager)
        {
            int encryptedTextLength = encryptionKey.GetEncryptByteCount(plainTextLength) + 1;

            byte[] encryptedText = arrayPoolManager.Rent(encryptedTextLength);

            encryptedText[0] = (byte)typeMarker;

            int encryptedLength = encryptionKey.EncryptData(
                plainText,
                plainTextOffset: 0,
                plainTextLength,
                encryptedText,
                outputOffset: 1);

            if (encryptedLength < 0)
            {
                throw new InvalidOperationException($"{nameof(DataEncryptionKey)} returned null cipherText from {nameof(DataEncryptionKey.EncryptData)}.");
            }

            return (encryptedText, encryptedTextLength);
        }

        internal virtual int Encrypt(DataEncryptionKey encryptionKey, TypeMarker typeMarker, byte[] plainText, int plainTextLength, byte[] outputBuffer)
        {
            int encryptedTextLength = encryptionKey.GetEncryptByteCount(plainTextLength) + 1;

            if (outputBuffer.Length < encryptedTextLength)
            {
                throw new ArgumentException($"Output buffer length {outputBuffer.Length} is less than required encrypted text length {encryptedTextLength}.");
            }

            outputBuffer[0] = (byte)typeMarker;

            int encryptedLength = encryptionKey.EncryptData(
                plainText,
                plainTextOffset: 0,
                plainTextLength,
                outputBuffer,
                outputOffset: 1);

            if (encryptedLength < 0)
            {
                throw new InvalidOperationException($"{nameof(DataEncryptionKey)} returned null cipherText from {nameof(DataEncryptionKey.EncryptData)}.");
            }

            return encryptedTextLength;
        }

        internal virtual (byte[] plainText, int plainTextLength) Decrypt(DataEncryptionKey encryptionKey, byte[] cipherText, int cipherTextLength, ArrayPoolManager arrayPoolManager)
        {
            int plainTextLength = this.GetDecryptedByteCount(encryptionKey, cipherTextLength);

            byte[] plainText = arrayPoolManager.Rent(plainTextLength);

            int decryptedLength = encryptionKey.DecryptData(
                cipherText,
                cipherTextOffset: 1,
                cipherTextLength: cipherTextLength - 1,
                plainText,
                outputOffset: 0);

            if (decryptedLength < 0)
            {
                throw new InvalidOperationException($"{nameof(DataEncryptionKey)} returned null plainText from {nameof(DataEncryptionKey.DecryptData)}.");
            }

            return (plainText, decryptedLength);
        }

        internal virtual int Decrypt(DataEncryptionKey encryptionKey, byte[] cipherText, int cipherTextLength, byte[] plainText)
        {
            int plainTextLength = encryptionKey.GetDecryptByteCount(cipherTextLength - 1);

            if (plainText.Length < plainTextLength)
            {
                throw new ArgumentException($"Plain text buffer length {plainText.Length} is less than required plain text length {plainTextLength}.");
            }

            int decryptedLength = encryptionKey.DecryptData(
                cipherText,
                cipherTextOffset: 1,
                cipherTextLength: cipherTextLength - 1,
                plainText,
                outputOffset: 0);

            if (decryptedLength < 0)
            {
                throw new InvalidOperationException($"{nameof(DataEncryptionKey)} returned null plainText from {nameof(DataEncryptionKey.DecryptData)}.");
            }

            return decryptedLength;
        }

        internal virtual int GetDecryptedByteCount(DataEncryptionKey encryptionKey, int cipherTextLength)
        {
            return encryptionKey.GetDecryptByteCount(cipherTextLength - 1);
        }
    }
}
