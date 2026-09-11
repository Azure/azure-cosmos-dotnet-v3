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
            if (encryptionKey is not IDataEncryptionKeyBuffer bufferEncryptionKey)
            {
                return EncryptWithPublicArray(encryptionKey, typeMarker, plainText, plainTextLength);
            }

            int encryptByteCount = bufferEncryptionKey.GetEncryptByteCount(plainTextLength);
            if (encryptByteCount < 0)
            {
                throw new InvalidOperationException($"{nameof(IDataEncryptionKeyBuffer.GetEncryptByteCount)} returned a negative length.");
            }

            int encryptedTextLength = checked(encryptByteCount + 1);

            byte[] encryptedText = new byte[encryptedTextLength];

            encryptedText[0] = (byte)typeMarker;

            int encryptedLength = bufferEncryptionKey.EncryptData(
                plainText,
                plainTextOffset: 0,
                plainTextLength,
                encryptedText,
                outputOffset: 1);

            if (encryptedLength < 0)
            {
                throw new InvalidOperationException($"{nameof(DataEncryptionKey)} returned null cipherText from {nameof(DataEncryptionKey.EncryptData)}.");
            }

            if (encryptedLength > encryptedTextLength - 1)
            {
                throw new InvalidOperationException($"{nameof(DataEncryptionKey)} wrote more cipherText than {nameof(IDataEncryptionKeyBuffer.GetEncryptByteCount)} predicted.");
            }

            int actualLength = encryptedLength + 1;
            if (actualLength == encryptedText.Length)
            {
                return encryptedText;
            }

            byte[] exactEncryptedText = new byte[actualLength];
            Buffer.BlockCopy(encryptedText, 0, exactEncryptedText, 0, actualLength);
            return exactEncryptedText;
        }

        internal virtual (byte[], int) Encrypt(DataEncryptionKey encryptionKey, TypeMarker typeMarker, byte[] plainText, int plainTextLength, ArrayPoolManager arrayPoolManager)
        {
            if (encryptionKey is not IDataEncryptionKeyBuffer bufferEncryptionKey)
            {
                byte[] arrayEncryptedText = EncryptWithPublicArray(encryptionKey, typeMarker, plainText, plainTextLength);
                return (arrayEncryptedText, arrayEncryptedText.Length);
            }

            int encryptByteCount = bufferEncryptionKey.GetEncryptByteCount(plainTextLength);
            if (encryptByteCount < 0)
            {
                throw new InvalidOperationException($"{nameof(IDataEncryptionKeyBuffer.GetEncryptByteCount)} returned a negative length.");
            }

            int encryptedTextLength = checked(encryptByteCount + 1);

            byte[] encryptedText = arrayPoolManager.Rent(encryptedTextLength);

            encryptedText[0] = (byte)typeMarker;

            int encryptedLength = bufferEncryptionKey.EncryptData(
                plainText,
                plainTextOffset: 0,
                plainTextLength,
                encryptedText,
                outputOffset: 1);

            if (encryptedLength < 0)
            {
                throw new InvalidOperationException($"{nameof(DataEncryptionKey)} returned null cipherText from {nameof(DataEncryptionKey.EncryptData)}.");
            }

            if (encryptedLength > encryptedTextLength - 1)
            {
                throw new InvalidOperationException($"{nameof(DataEncryptionKey)} wrote more cipherText than {nameof(IDataEncryptionKeyBuffer.GetEncryptByteCount)} predicted.");
            }

            return (encryptedText, encryptedLength + 1);
        }

        private static byte[] EncryptWithPublicArray(DataEncryptionKey encryptionKey, TypeMarker typeMarker, byte[] plainText, int plainTextLength)
        {
            byte[] exactPlainText = new byte[plainTextLength];
            Buffer.BlockCopy(plainText, 0, exactPlainText, 0, plainTextLength);

            byte[] cipherText = encryptionKey.EncryptData(exactPlainText)
                ?? throw new InvalidOperationException($"{nameof(DataEncryptionKey)} returned null cipherText from {nameof(DataEncryptionKey.EncryptData)}.");

            byte[] encryptedText = new byte[cipherText.Length + 1];
            encryptedText[0] = (byte)typeMarker;
            Buffer.BlockCopy(cipherText, 0, encryptedText, 1, cipherText.Length);
            return encryptedText;
        }

        internal virtual (byte[] plainText, int plainTextLength) Decrypt(DataEncryptionKey encryptionKey, byte[] cipherText, int cipherTextLength, ArrayPoolManager arrayPoolManager)
        {
            if (encryptionKey is not IDataEncryptionKeyBuffer bufferEncryptionKey)
            {
                return DecryptWithPublicArray(encryptionKey, cipherText, cipherTextLength);
            }

            int plainTextLength = bufferEncryptionKey.GetDecryptByteCount(cipherTextLength - 1);
            if (plainTextLength < 0)
            {
                throw new InvalidOperationException($"{nameof(IDataEncryptionKeyBuffer.GetDecryptByteCount)} returned a negative length.");
            }

            byte[] plainText = arrayPoolManager.Rent(plainTextLength);

            int decryptedLength = bufferEncryptionKey.DecryptData(
                cipherText,
                cipherTextOffset: 1,
                cipherTextLength: cipherTextLength - 1,
                plainText,
                outputOffset: 0);

            if (decryptedLength < 0)
            {
                throw new InvalidOperationException($"{nameof(DataEncryptionKey)} returned null plainText from {nameof(DataEncryptionKey.DecryptData)}.");
            }

            if (decryptedLength > plainTextLength)
            {
                throw new InvalidOperationException($"{nameof(DataEncryptionKey)} wrote more plainText than {nameof(IDataEncryptionKeyBuffer.GetDecryptByteCount)} predicted.");
            }

            return (plainText, decryptedLength);
        }

        private static (byte[] plainText, int plainTextLength) DecryptWithPublicArray(
            DataEncryptionKey encryptionKey,
            byte[] cipherText,
            int cipherTextLength)
        {
            byte[] exactCipherText = new byte[cipherTextLength - 1];
            Buffer.BlockCopy(cipherText, 1, exactCipherText, 0, exactCipherText.Length);
            byte[] plainText = encryptionKey.DecryptData(exactCipherText)
                ?? throw new InvalidOperationException($"{nameof(DataEncryptionKey)} returned null plainText from {nameof(DataEncryptionKey.DecryptData)}.");
            return (plainText, plainText.Length);
        }
    }
}
