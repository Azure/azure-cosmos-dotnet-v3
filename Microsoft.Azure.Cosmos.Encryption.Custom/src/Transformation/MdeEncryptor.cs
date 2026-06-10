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
            // DataEncryptionKey implementations written against the original abstract surface
            // (array-based EncryptData only) cannot pre-compute the ciphertext size; route
            // them through the array-based API.
            if (!encryptionKey.ProvidesEncryptByteCount())
            {
                return EncryptLegacy(encryptionKey, typeMarker, plainText, plainTextLength);
            }

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
            if (!encryptionKey.ProvidesEncryptByteCount())
            {
                byte[] legacyEncryptedText = EncryptLegacy(encryptionKey, typeMarker, plainText, plainTextLength);
                return (legacyEncryptedText, legacyEncryptedText.Length);
            }

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

        private static byte[] EncryptLegacy(DataEncryptionKey encryptionKey, TypeMarker typeMarker, byte[] plainText, int plainTextLength)
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
            int plainTextLength = encryptionKey.GetDecryptByteCount(cipherTextLength - 1);

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
    }
}
