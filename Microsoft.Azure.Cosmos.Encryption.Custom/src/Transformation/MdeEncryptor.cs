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

        internal virtual (byte[] plainText, int plainTextLength) Decrypt(DataEncryptionKey encryptionKey, byte[] cipherText, ArrayPoolManager arrayPoolManager)
        {
            int plainTextLength = encryptionKey.GetDecryptByteCount(cipherText.Length - 1);

            byte[] plainText = arrayPoolManager.Rent(plainTextLength);

            int decryptedLength = encryptionKey.DecryptData(
                cipherText,
                cipherTextOffset: 1,
                cipherTextLength: cipherText.Length - 1,
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
