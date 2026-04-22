//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Covers the <see cref="ArrayPoolManager"/> constructor overloads and the
    /// initial-capacity fallback that clamps non-positive hints to
    /// <c>DefaultRentCapacity</c>. The rent/dispose loop is exercised indirectly
    /// by the stream processor tests.
    /// </summary>
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
            // initialRentCapacity <= 0 must not throw and must still support Rent. This
            // exercises the defensive clamp in the constructor.
            using ArrayPoolManager mgr = new (initialRentCapacity: 0);
            byte[] b = mgr.Rent(4);
            Assert.IsNotNull(b);
        }

        [TestMethod]
        public void CtorWithNegativeCapacity_FallsBackToDefault()
        {
            // Same clamp, negative path.
            using ArrayPoolManager mgr = new (initialRentCapacity: -100);
            byte[] b = mgr.Rent(4);
            Assert.IsNotNull(b);
        }

        [TestMethod]
        public void GenericCtorWithZeroCapacity_FallsBackToDefault()
        {
            // Exercises the same fallback on the generic base type.
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
            mgr.Dispose(); // second call must not throw
        }
    }
}
