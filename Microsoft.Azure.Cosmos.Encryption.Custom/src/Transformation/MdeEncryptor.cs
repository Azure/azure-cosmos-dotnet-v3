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

            // Return the actual number of bytes produced (including the type marker at index 0)
            return (encryptedText, 1 + encryptedLength);
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

        // Span-based caller-supplied destination variant to enable scratch buffer reuse.
        // Returns number of plaintext bytes written. Destination MUST be sized with GetDecryptByteCount(cipherLen-1).
        internal virtual int DecryptInto(DataEncryptionKey encryptionKey, ReadOnlySpan<byte> cipherTextWithMarker, Span<byte> destination)
        {
            if (cipherTextWithMarker.Length < 1)
            {
                throw new ArgumentException("Ciphertext too short (missing type marker)", nameof(cipherTextWithMarker));
            }

            int cipherLen = cipherTextWithMarker.Length - 1;
            if (cipherLen == 0)
            {
                return 0;
            }

            // TEMP implementation: copy into temp array to leverage existing decrypt API.
            byte[] temp = new byte[cipherLen];
            cipherTextWithMarker.Slice(1).CopyTo(temp);

            // Destination must be sized using GetDecryptByteCount by caller.
            int written = encryptionKey.DecryptData(temp, 0, cipherLen, destination.ToArray(), 0);
            return written;
        }

        // Ownership-explicit variant: return IMemoryOwner<byte> for deterministic lifetime management without DEBUG probes
        internal virtual PooledByteOwner DecryptOwned(DataEncryptionKey encryptionKey, byte[] cipherText, int cipherTextLength, ArrayPoolManager arrayPoolManager)
        {
            (byte[] bytes, int len) = this.Decrypt(encryptionKey, cipherText, cipherTextLength, arrayPoolManager);
            return new PooledByteOwner(arrayPoolManager, bytes, len);
        }
    }
}
