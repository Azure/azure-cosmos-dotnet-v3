﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;

    internal static class EncryptionExceptionFactory
    {
        internal static ArgumentException InvalidKeySize(string algorithmName, int actualKeylength, int expectedLength)
        {
            return new ArgumentException(
                $"Invalid key size for {algorithmName}; actual: {actualKeylength}, expected: {expectedLength}",
                "dataEncryptionKey");
        }

        internal static ArgumentException InvalidCipherTextSize(int actualSize, int minimumSize)
        {
            return new ArgumentException(
                $"Invalid cipher text size; actual: {actualSize}, minimum expected: {minimumSize}.",
                "cipherText");
        }

        internal static ArgumentException InvalidAlgorithmVersion(byte actual, byte expected)
        {
            return new ArgumentException(
                $"Invalid encryption algorithm version; actual: {actual.ToString(@"X2")}, expected: {expected.ToString(@"X2")}.",
                "cipherText");
        }

        internal static ArgumentException InvalidAuthenticationTag()
        {
            return new ArgumentException(
                "Invalid authentication tag in cipher text.",
                "cipherText");
        }

        internal static Exception EncryptionKeyNotFoundException(
            string message,
            Exception innerException)
        {
            return new ArgumentException(
                message,
                innerException);
        }
    }
}
