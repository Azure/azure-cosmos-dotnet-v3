//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal static class EncryptionExceptions
    {
        internal static Exception InvalidKeySize(string algorithmName, int actualKeylength, int expectedLength)
        {
            return new ArgumentException(
                string.Format(
                    "Invalid key size for {0}; actual: {1}, expected: {2}",
                    algorithmName,
                    actualKeylength,
                    expectedLength),
                "dataEncryptionKey");
        }

        internal static Exception InvalidCipherTextSize(int actualSize, int minimumSize)
        {
            return new ArgumentException(
                string.Format(
                    "Invalid cipher text size; actual: {0}, minimum expected: {1}.",
                    actualSize,
                    minimumSize),
                "cipherText");
        }

        internal static Exception InvalidAlgorithmVersion(byte actual, byte expected)
        {
            return new ArgumentException(
                string.Format(
                    "Invalid encryption algorithm version; actual: {0}, expected: {1}.",
                    actual.ToString(@"X2"),
                    expected.ToString(@"X2")),
                "cipherText");
        }

        internal static Exception InvalidAuthenticationTag()
        {
            return new ArgumentException(
                "Invalid authentication tag in cipher text.",
                "cipherText");
        }
    }
}
