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
            void Blah(NonNullable<MyClass> foo)
            {
            }

            Blah(new MyClass());
        }

        [TestMethod]
        public void ImplicitConversion_NonNullableToValue()
        {
            void Blah(MyClass foo)
            {
            }

            NonNullable<MyClass> nonNullable = new NonNullable<MyClass>(new MyClass());
            ((MyClass)nonNullable).Foo();
            Blah(nonNullable);
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

        private sealed class MyClass
        {
            public void Foo()
            {
            }
        }
    }
}
