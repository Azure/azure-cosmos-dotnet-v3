//------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Direct
{
    using System;
    using System.Numerics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using DirectUInt128 = Microsoft.Azure.Documents.UInt128;

    [TestClass]
    public class DirectUInt128Tests
    {
        [TestMethod]
        public void AdditionMatchesBigIntegerModulo128Bits()
        {
            BigInteger modulus = BigInteger.One << 128;
            BigInteger[] boundaryValues =
            {
                BigInteger.Zero,
                BigInteger.One,
                (BigInteger.One << 64) - 1,
                BigInteger.One << 64,
                modulus - 1,
            };

            foreach (BigInteger left in boundaryValues)
            {
                foreach (BigInteger right in boundaryValues)
                {
                    VerifyAddition(left, right, modulus);
                }
            }

            Random random = new Random(42);
            for (int i = 0; i < 10; i++)
            {
                byte[] left = new byte[16];
                byte[] right = new byte[16];
                random.NextBytes(left);
                random.NextBytes(right);
                VerifyAddition(
                    new BigInteger(left, isUnsigned: true),
                    new BigInteger(right, isUnsigned: true),
                    modulus);
            }
        }

        private static void VerifyAddition(BigInteger left, BigInteger right, BigInteger modulus)
        {
            byte[] leftBytes = new byte[16];
            byte[] rightBytes = new byte[16];
            Assert.IsTrue(left.TryWriteBytes(leftBytes, out _, isUnsigned: true));
            Assert.IsTrue(right.TryWriteBytes(rightBytes, out _, isUnsigned: true));

            DirectUInt128 sum = DirectUInt128.FromByteArray(leftBytes) + DirectUInt128.FromByteArray(rightBytes);
            BigInteger actual = new BigInteger(DirectUInt128.ToByteArray(sum), isUnsigned: true);
            Assert.AreEqual((left + right) % modulus, actual);
        }
    }
}
