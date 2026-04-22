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
    }
}
