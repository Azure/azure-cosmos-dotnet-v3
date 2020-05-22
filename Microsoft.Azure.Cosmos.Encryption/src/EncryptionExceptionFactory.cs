//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    internal static class EncryptionExceptionFactory
    {
        internal static Exception InvalidKeySize(string algorithmName, int actualKeylength, int expectedLength)
        {
            return new ArgumentException(
                $"Invalid key size for {algorithmName}; actual: {actualKeylength}, expected: {expectedLength}",
                "dataEncryptionKey");
        }

        internal static Exception InvalidCipherTextSize(int actualSize, int minimumSize)
        {
            return new ArgumentException(
                $"Invalid cipher text size; actual: {actualSize}, minimum expected: {minimumSize}.",
                "cipherText");
        }

        internal static Exception InvalidAlgorithmVersion(byte actual, byte expected)
        {
            return new ArgumentException(
                $"Invalid encryption algorithm version; actual: {actual.ToString(@"X2")}, expected: {expected.ToString(@"X2")}.",
                "cipherText");
        }

        internal static Exception InvalidAuthenticationTag()
        {
            return new ArgumentException(
                "Invalid authentication tag in cipher text.",
                "cipherText");
        }

        internal static Exception EncryptionKeyNotFoundException(string encryptionKeyId)
        {
            return new ArgumentException($"Data Encryption Key with id: '{encryptionKeyId}' not found.");
        }
    }
}
