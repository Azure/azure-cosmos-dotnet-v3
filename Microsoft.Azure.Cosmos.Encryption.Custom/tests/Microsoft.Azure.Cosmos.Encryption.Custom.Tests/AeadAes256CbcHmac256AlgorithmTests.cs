//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Linq;

    [TestClass]
    public class AeadAes256CbcHmac256AlgorithmTests
    {
        private static readonly byte[] RootKey = new byte[32];

        private static AeadAes256CbcHmac256EncryptionKey key;
        private static AeadAes256CbcHmac256Algorithm algorithm;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _ = testContext;

            AeadAes256CbcHmac256AlgorithmTests.key = new AeadAes256CbcHmac256EncryptionKey(RootKey, "AEAes256CbcHmacSha256Randomized");
            AeadAes256CbcHmac256AlgorithmTests.algorithm = new AeadAes256CbcHmac256Algorithm(AeadAes256CbcHmac256AlgorithmTests.key, EncryptionType.Randomized, algorithmVersion: 1);
        }

        [TestMethod]
        public void EncryptUsingBufferDecryptsSuccessfully()
        {
            byte[] plainTextBytes = new byte[4] { 0, 1, 2, 3 } ;

            int cipherTextLength = algorithm.GetEncryptByteCount(plainTextBytes.Length);
            byte[] cipherTextBytes = new byte[cipherTextLength];

            int encrypted = algorithm.EncryptData(plainTextBytes, 0, plainTextBytes.Length, cipherTextBytes, 0);
            Assert.AreEqual(encrypted, cipherTextLength);

            byte[] decrypted = algorithm.DecryptData(cipherTextBytes);

            Assert.IsTrue(plainTextBytes.SequenceEqual(decrypted));
        }

        [TestMethod]
        public void DecryptUsingBufferDecryptsSuccessfully()
        {
            byte[] plainTextBytes = new byte[4] { 0, 1, 2, 3 };
            byte[] encrypted = algorithm.EncryptData(plainTextBytes);

            int plainTextMaxLength = algorithm.GetDecryptByteCount(encrypted.Length);
            byte[] decrypted = new byte[plainTextMaxLength];

            int decryptedBytes = algorithm.DecryptData(encrypted, 0, encrypted.Length, decrypted, 0);

            Assert.AreEqual(plainTextBytes.Length, decryptedBytes);
            Assert.IsTrue(plainTextBytes.SequenceEqual(decrypted.AsSpan(0, decryptedBytes).ToArray()));
        }
    }
}
