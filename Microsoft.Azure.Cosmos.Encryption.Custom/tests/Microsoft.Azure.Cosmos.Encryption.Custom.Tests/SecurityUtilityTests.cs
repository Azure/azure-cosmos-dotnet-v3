//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System.Linq;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SecurityUtilityTests
    {
        [TestMethod]
        public void CompareBytes_EqualBuffers_ReturnsTrue()
        {
            byte[] a = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
            byte[] b = (byte[])a.Clone();

            Assert.IsTrue(SecurityUtility.CompareBytes(a, b, 0, a.Length));
        }

        [TestMethod]
        public void CompareBytes_SingleByteDifference_AtEveryPosition_ReturnsFalse()
        {
            // A constant-time comparison must still reject a mismatch at ANY position — including the
            // very last byte. An early-exit implementation is trivially correct here; this test guards
            // against a future "optimization" that stops accumulating before the end.
            byte[] a = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

            for (int pos = 0; pos < a.Length; pos++)
            {
                byte[] b = (byte[])a.Clone();
                b[pos] ^= 0xFF;

                Assert.IsFalse(
                    SecurityUtility.CompareBytes(a, b, 0, a.Length),
                    $"A difference at position {pos} must be rejected.");
            }
        }

        [TestMethod]
        public void CompareBytes_Buffer1ShorterThanLength_ReturnsFalse_EvenWhenPrefixMatches()
        {
            // Footgun closure: requesting more bytes than buffer1 contains must fail, even though the
            // common prefix is equal. Previously the loop clamped to the shorter length and could
            // report a prefix match as a full match.
            byte[] a = new byte[] { 1, 2, 3 };
            byte[] b = new byte[] { 1, 2, 3, 4, 5 };

            Assert.IsFalse(SecurityUtility.CompareBytes(a, b, 0, b.Length));
        }

        [TestMethod]
        public void CompareBytes_Buffer2RangeTooShort_ReturnsFalse()
        {
            byte[] a = new byte[] { 1, 2, 3, 4 };
            byte[] b = new byte[] { 0, 1, 2 };

            // buffer2 only has 2 bytes available from index 1, but 4 were requested.
            Assert.IsFalse(SecurityUtility.CompareBytes(a, b, 1, 4));
        }

        [TestMethod]
        public void CompareBytes_WithOffset_ComparesCorrectSlice()
        {
            byte[] a = new byte[] { 10, 20, 30, 40 };
            byte[] b = new byte[] { 99, 10, 20, 30, 40, 99 };

            Assert.IsTrue(SecurityUtility.CompareBytes(a, b, 1, 4));
            Assert.IsFalse(SecurityUtility.CompareBytes(a, b, 0, 4));
        }

        [TestMethod]
        public void CompareBytes_NullBuffer_ReturnsFalse()
        {
            Assert.IsFalse(SecurityUtility.CompareBytes(null, new byte[4], 0, 4));
            Assert.IsFalse(SecurityUtility.CompareBytes(new byte[4], null, 0, 4));
        }
    }
}
