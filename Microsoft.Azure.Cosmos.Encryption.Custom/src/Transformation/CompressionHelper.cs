// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;

    internal static class CompressionHelper
    {
        public static int GetBase64Length(int byteCount)
        {
            if (byteCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteCount));
            }

            return checked((byteCount + 2) / 3 * 4);
        }

        public static int GetStoredBase64Length(
            int plaintextLength,
            Func<int, int> getEncryptByteCount)
        {
            if (plaintextLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(plaintextLength));
            }

            if (getEncryptByteCount == null)
            {
                throw new ArgumentNullException(nameof(getEncryptByteCount));
            }

            int encryptedLength = getEncryptByteCount(plaintextLength);
            if (encryptedLength < 0)
            {
                throw new InvalidOperationException("Encrypted byte count cannot be negative.");
            }

            return GetBase64Length(checked(1 + encryptedLength));
        }

        public static bool IsCompressedEnvelopeStrictlySmaller(
            int rawLength,
            int compressedLength,
            Func<int, int> getEncryptByteCount)
        {
            if (rawLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rawLength));
            }

            if (compressedLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(compressedLength));
            }

            int rawPlaintextLength = EnvelopeHeaderConstants.HeaderSize + rawLength;
            int compressedPlaintextLength = EnvelopeHeaderConstants.HeaderSize +
                UleB128.GetEncodedLength((uint)rawLength) +
                UleB128.GetEncodedLength((uint)compressedLength) +
                compressedLength;

            return GetStoredBase64Length(compressedPlaintextLength, getEncryptByteCount) <
                GetStoredBase64Length(rawPlaintextLength, getEncryptByteCount);
        }
    }
}
#endif
