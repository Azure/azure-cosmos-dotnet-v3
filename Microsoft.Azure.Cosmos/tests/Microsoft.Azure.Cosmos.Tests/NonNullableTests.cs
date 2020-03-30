//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NonNullableTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_Null()
        {
            NonNullable<object> nonNullable = new NonNullable<object>(null);
        }

        [TestMethod]
        public void Constructor_NonNull()
        {
            object nonNull = new object();
            NonNullable<object> nonNullable = new NonNullable<object>(nonNull);
            Assert.AreEqual(nonNull, nonNullable.Reference);
        }

        [TestMethod]
        public void ImplicitConversion_ValueToNonNullable()
        {
            void Blah(NonNullable<object> foo)
            {
            }

            Blah(new object());
        }

        [TestMethod]
        public void ImplicitConversion_NonNullableToValue()
        {
            void Blah(object foo)
            {
            }

            Blah(new NonNullable<object>(new object()));
        }

        [TestMethod]
        public void TestEquals()
        {
            object reference = new object();
            NonNullable<object> nonNullable = new NonNullable<object>(reference);
            Assert.AreEqual(nonNullable, reference);
        }

        [TestMethod]
        public void TestGetHashCode()
        {
            object reference = new object();
            NonNullable<object> nonNullable = new NonNullable<object>(reference);
            Assert.AreEqual(nonNullable.GetHashCode(), reference.GetHashCode());
        }

        [TestMethod]
        public void TestToString()
        {
            object reference = new object();
            NonNullable<object> nonNullable = new NonNullable<object>(reference);
            Assert.AreEqual(nonNullable.ToString(), reference.ToString());
        }
    }
}
