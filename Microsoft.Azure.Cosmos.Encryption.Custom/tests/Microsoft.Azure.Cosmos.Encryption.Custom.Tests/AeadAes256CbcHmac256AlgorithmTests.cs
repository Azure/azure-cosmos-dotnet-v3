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

        [TestMethod]
        public void DecryptData_TamperedAuthenticationTag_RejectedAtEveryBytePosition()
        {
            // The authentication tag is verified with SecurityUtility.CompareBytes, which is
            // constant-time. A single flipped bit at ANY tag position — including the last byte — must
            // be rejected. This guards the MAC check against a regression to an early-exit comparison
            // that could stop before the final byte.
            byte[] plainText = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
            byte[] cipher = algorithm.EncryptData(plainText);

            // Cipher layout: [version:1][authTag:32][iv:16][ciphertext]. The tag occupies the 32 bytes
            // immediately after the version byte (KeySizeInBytes for a 256-bit key).
            const int tagOffset = 1;
            const int tagLength = 32;

            for (int i = 0; i < tagLength; i++)
            {
                byte[] tampered = (byte[])cipher.Clone();
                tampered[tagOffset + i] ^= 0xFF;

                ArgumentException ex = Assert.ThrowsException<ArgumentException>(
                    () => algorithm.DecryptData(tampered),
                    $"A flipped authentication-tag byte at position {i} must be rejected.");
                StringAssert.Contains(ex.Message, "authentication tag");
            }
        }

        [TestMethod]
        public void DecryptData_TamperedCipherText_Rejected()
        {
            // Flipping a ciphertext byte changes the recomputed tag, so the MAC check must fail.
            byte[] plainText = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
            byte[] cipher = algorithm.EncryptData(plainText);

            byte[] tampered = (byte[])cipher.Clone();
            tampered[tampered.Length - 1] ^= 0xFF;

            Assert.ThrowsException<ArgumentException>(() => algorithm.DecryptData(tampered));
        }
    }
}
