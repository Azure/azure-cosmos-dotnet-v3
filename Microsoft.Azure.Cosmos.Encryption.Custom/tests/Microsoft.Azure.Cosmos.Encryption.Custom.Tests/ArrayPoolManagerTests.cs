//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ArrayPoolManagerTests
    {
        [TestMethod]
        public void DefaultCtor_AllowsRentAndDispose()
        {
            using ArrayPoolManager mgr = new ();
            byte[] b = mgr.Rent(16);
            Assert.IsNotNull(b);
            Assert.IsTrue(b.Length >= 16);
        }

        [TestMethod]
        public void CtorWithExplicitCapacity_AllowsRent()
        {
            using ArrayPoolManager mgr = new (initialRentCapacity: 32);
            byte[] b = mgr.Rent(8);
            Assert.IsNotNull(b);
        }

        [TestMethod]
        public void CtorWithZeroCapacity_FallsBackToDefault()
        {
            using ArrayPoolManager mgr = new (initialRentCapacity: 0);
            byte[] b = mgr.Rent(4);
            Assert.IsNotNull(b);
        }

        [TestMethod]
        public void CtorWithNegativeCapacity_FallsBackToDefault()
        {
            using ArrayPoolManager mgr = new (initialRentCapacity: -100);
            byte[] b = mgr.Rent(4);
            Assert.IsNotNull(b);
        }

        [TestMethod]
        public void GenericCtorWithZeroCapacity_FallsBackToDefault()
        {
            using ArrayPoolManager<int> mgr = new (initialRentCapacity: 0);
            int[] buffer = mgr.Rent(4);
            Assert.IsNotNull(buffer);
        }

        [TestMethod]
        public void Dispose_IsIdempotent()
        {
            ArrayPoolManager mgr = new ();
            _ = mgr.Rent(8);
            mgr.Dispose();
            mgr.Dispose();
        }

        [TestMethod]
        public void RentScratch_ReusesSameBufferUntilLargerLengthRequested()
        {
            using ArrayPoolManager mgr = new ();

            byte[] first = mgr.RentScratch(16);
            Assert.IsNotNull(first);
            Assert.AreSame(first, mgr.RentScratch(16), "Same length should reuse the scratch buffer.");
            Assert.AreSame(first, mgr.RentScratch(8), "Smaller length should reuse the scratch buffer.");
            Assert.AreSame(first, mgr.RentScratch(first.Length), "Exact-capacity length should reuse the scratch buffer.");

            byte[] grown = mgr.RentScratch(first.Length + 1);
            Assert.AreNotSame(first, grown, "A larger length should grow the scratch buffer.");
            Assert.IsTrue(grown.Length >= first.Length + 1);
            Assert.AreSame(grown, mgr.RentScratch(1), "After growth, subsequent smaller requests reuse the grown buffer.");
        }

        [TestMethod]
        public void RentScratch_ManySameSizeRequests_RentsUnderlyingBufferOnce()
        {
            using ArrayPoolManager mgr = new ();

            for (int i = 0; i < 100; i++)
            {
                _ = mgr.RentScratch(16);
            }

            Assert.AreEqual(1, mgr.RentedBufferCount, "Repeated same-size scratch requests must rent exactly one pool buffer.");
        }
    }
}
