//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Utils
{
    using System;

    internal static class TestKeyGenerator
    {
        // Used for unit tests only so we don't need crypto-strong keys
        private static readonly Random ByteGenerator = new Random();

        public static string GenerateAuthKey()
        {
            return TestKeyGenerator.GenerateTestKey(keySize: 64);
        }

        public static string GenerateResourceToken()
        {
            return TestKeyGenerator.GenerateTestKey(keySize: ByteGenerator.Next(20, 64));
        }

        public static string GenerateTestKey(int keySize)
        {
            byte[] hashKey = new byte[keySize];
            ByteGenerator.NextBytes(hashKey);
            return Convert.ToBase64String(hashKey);
        }
    }
}