//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class StringSegmentTests
    {
        [Owner("visridha")]
        [TestMethod]
        public void StringSegmentLengthTest()
        {
            StringSegment segment = new StringSegment("foo");
            Assert.AreEqual(3, segment.Length);
            segment = new StringSegment(string.Empty);
            Assert.AreEqual(0, segment.Length);
            segment = new StringSegment(null);
            Assert.AreEqual(0, segment.Length);
            segment = new StringSegment("foobarbaz", 0, 2);
            Assert.AreEqual(2, segment.Length);
            segment = new StringSegment("foobarbaz", 1, 3);
            Assert.AreEqual(3, segment.Length);

            segment = new StringSegment("foobarbaz", 1, 0);
            Assert.AreEqual(0, segment.Length);
        }

        [Owner("visridha")]
        [TestMethod]
        public void StringSegmentIsNullOrEmpty()
        {
            StringSegment segment = new StringSegment("foo");
            Assert.IsFalse(segment.IsNullOrEmpty());
            segment = new StringSegment(string.Empty);
            Assert.IsTrue(segment.IsNullOrEmpty());
            segment = new StringSegment("foobarbaz", 1, 0);
            Assert.IsTrue(segment.IsNullOrEmpty());
            segment = new StringSegment(null);
            Assert.IsTrue(segment.IsNullOrEmpty());
        }

        [Owner("visridha")]
        [TestMethod]
        public void StringSegmentEquals()
        {
            StringSegment segment = new StringSegment("foo");
            Assert.IsFalse(segment.Equals("bar", StringComparison.Ordinal));
            Assert.IsFalse(segment.Equals("Foo", StringComparison.Ordinal));
            Assert.IsTrue(segment.Equals("Foo", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(segment.Equals("FoO", StringComparison.OrdinalIgnoreCase));

            segment = new StringSegment("SomeCharFoo");
            Assert.IsFalse(segment.Equals("bar", StringComparison.Ordinal));
            Assert.IsFalse(segment.Equals("Foo", StringComparison.Ordinal));
            Assert.IsFalse(segment.Equals("Foo", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(segment.Equals("FoO", StringComparison.OrdinalIgnoreCase));

            segment = new StringSegment("SomeCharFoo", 8, 3);
            Assert.IsFalse(segment.Equals("bar", StringComparison.Ordinal));
            Assert.IsFalse(segment.Equals("foo", StringComparison.Ordinal));
            Assert.IsTrue(segment.Equals("Foo", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(segment.Equals("FoO", StringComparison.OrdinalIgnoreCase));

            segment = new StringSegment("SomeCharFooWithSuffix", 8, 3);
            Assert.IsFalse(segment.Equals("bar", StringComparison.Ordinal));
            Assert.IsFalse(segment.Equals("foo", StringComparison.Ordinal));
            Assert.IsTrue(segment.Equals("Foo", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(segment.Equals("FoO", StringComparison.OrdinalIgnoreCase));

            segment = new StringSegment(null);
            Assert.IsFalse(segment.Equals("bar", StringComparison.Ordinal));
            Assert.IsFalse(segment.Equals("Foo", StringComparison.Ordinal));
            Assert.IsFalse(segment.Equals("Foo", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(segment.Equals("FoO", StringComparison.OrdinalIgnoreCase));

            segment = new StringSegment(string.Empty);
            Assert.IsFalse(segment.Equals("bar", StringComparison.Ordinal));
            Assert.IsFalse(segment.Equals("Foo", StringComparison.Ordinal));
            Assert.IsFalse(segment.Equals("Foo", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(segment.Equals("FoO", StringComparison.OrdinalIgnoreCase));
        }

        [Owner("visridha")]
        [TestMethod]
        public void StringSegmentCompare()
        {
            StringSegment segment = new StringSegment("foo");
            Assert.IsTrue(segment.Compare("bar", StringComparison.Ordinal) > 0);
            Assert.IsTrue(segment.Compare("Foo", StringComparison.Ordinal) > 0);
            Assert.IsTrue(segment.Compare("Foo", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(segment.Compare("FoO", StringComparison.OrdinalIgnoreCase) == 0);

            segment = new StringSegment("SomeCharFoo");
            Assert.IsTrue(segment.Compare("bar", StringComparison.Ordinal) < 0);
            Assert.IsTrue(segment.Compare("Foo", StringComparison.Ordinal) > 0);
            Assert.IsTrue(segment.Compare("Foo", StringComparison.OrdinalIgnoreCase) > 0);
            Assert.IsTrue(segment.Compare("FoO", StringComparison.OrdinalIgnoreCase) > 0);

            segment = new StringSegment("SomeCharFoo", 8, 3);
            Assert.IsTrue(segment.Compare("bar", StringComparison.Ordinal) < 0);
            Assert.IsTrue(segment.Compare("foo", StringComparison.Ordinal) < 0);
            Assert.IsTrue(segment.Compare("Foo", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(segment.Compare("FoO", StringComparison.OrdinalIgnoreCase) == 0);

            segment = new StringSegment("SomeCharFooWithSuffix", 8, 3);
            Assert.IsTrue(segment.Compare("bar", StringComparison.Ordinal) < 0);
            Assert.IsTrue(segment.Compare("foo", StringComparison.Ordinal) < 0);
            Assert.IsTrue(segment.Compare("Foo", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(segment.Compare("FoO", StringComparison.OrdinalIgnoreCase) == 0);

            segment = new StringSegment(null);
            Assert.IsTrue(segment.Compare("bar", StringComparison.Ordinal) < 0);
            Assert.IsTrue(segment.Compare("Foo", StringComparison.Ordinal) < 0);
            Assert.IsTrue(segment.Compare("Foo", StringComparison.OrdinalIgnoreCase) < 0);
            Assert.IsTrue(segment.Compare("FoO", StringComparison.OrdinalIgnoreCase) < 0);
        }

        [Owner("visridha")]
        [TestMethod]
        public void StringSegmentGetString()
        {
            StringSegment segment = new StringSegment("fob");
            Assert.AreEqual("fob", segment.GetString());

            segment = new StringSegment("fob", 1, 1);
            Assert.AreEqual("o", segment.GetString());

            segment = new StringSegment("fob", 0, 1);
            Assert.AreEqual("f", segment.GetString());

            segment = new StringSegment("fob", 0, 0);
            Assert.AreEqual(string.Empty, segment.GetString());

            segment = new StringSegment("fob", 2, 1);
            Assert.AreEqual("b", segment.GetString());
        }

        [Owner("visridha")]
        [TestMethod]
        public void StringSegmentTrimEnd()
        {
            StringSegment segment = new StringSegment("fob");
            StringSegment other = segment.TrimEnd(new char[] { '/' });
            Assert.AreEqual("fob", other.GetString());

            segment = new StringSegment("fob/");
            other = segment.TrimEnd(new char[] { '/' });
            Assert.AreEqual("fob", other.GetString());

            segment = new StringSegment("fob////");
            other = segment.TrimEnd(new char[] { '/' });
            Assert.AreEqual("fob", other.GetString());

            segment = new StringSegment("fob//#/?/??#/");
            other = segment.TrimEnd(new char[] { '/' });
            Assert.AreEqual("fob//#/?/??#", other.GetString());

            segment = new StringSegment("/fob//#/?/??#/");
            other = segment.TrimEnd(new char[] { '/' });
            Assert.AreEqual("/fob//#/?/??#", other.GetString());

            segment = new StringSegment("/fob//#/?/??#/");
            other = segment.TrimEnd(new char[] { '/', '#' });
            Assert.AreEqual("/fob//#/?/??", other.GetString());

            segment = new StringSegment("/fob//#/?/??#/");
            other = segment.TrimEnd(new char[] { '/', '#', '?' });
            Assert.AreEqual("/fob", other.GetString());

            segment = new StringSegment("/fob//#/?/??#/somothersubstring", 0, 14);
            other = segment.TrimEnd(new char[] { '/', '#', '?' });
            Assert.AreEqual("/fob", other.GetString());

            segment = new StringSegment("abc/fob//#/?/??#/somothersubstring", 3, 14);
            other = segment.TrimEnd(new char[] { '/', '#', '?' });
            Assert.AreEqual("/fob", other.GetString());
        }

        [Owner("visridha")]
        [TestMethod]
        public void StringSegmentTrimStart()
        {
            StringSegment segment = new StringSegment("fob");
            StringSegment other = segment.TrimStart(new char[] { '/' });
            Assert.AreEqual("fob", other.GetString());

            segment = new StringSegment("fob/");
            other = segment.TrimStart(new char[] { '/' });
            Assert.AreEqual("fob/", other.GetString());

            segment = new StringSegment("/fob");
            other = segment.TrimStart(new char[] { '/' });
            Assert.AreEqual("fob", other.GetString());

            segment = new StringSegment("////fob");
            other = segment.TrimStart(new char[] { '/' });
            Assert.AreEqual("fob", other.GetString());

            segment = new StringSegment("//#/?/??#/fob");
            other = segment.TrimStart(new char[] { '/' });
            Assert.AreEqual("#/?/??#/fob", other.GetString());

            segment = new StringSegment("//#/?/??#/fob//");
            other = segment.TrimStart(new char[] { '/' });
            Assert.AreEqual("#/?/??#/fob//", other.GetString());

            segment = new StringSegment("//#/?/??#/fob//");
            other = segment.TrimStart(new char[] { '/', '#' });
            Assert.AreEqual("?/??#/fob//", other.GetString());

            segment = new StringSegment("//#/?/??#/fob//");
            other = segment.TrimStart(new char[] { '/', '#', '?' });
            Assert.AreEqual("fob//", other.GetString());

            segment = new StringSegment("//#/?/??#/fob//someothersubstring", 0, 15);
            other = segment.TrimStart(new char[] { '/', '#', '?' });
            Assert.AreEqual("fob//", other.GetString());

            segment = new StringSegment("a//#/?/??#/fob//someothersubstring", 1, 15);
            other = segment.TrimStart(new char[] { '/', '#', '?' });
            Assert.AreEqual("fob//", other.GetString());
        }

        [Owner("visridha")]
        [TestMethod]
        public void StringSegmentLastIndexOf()
        {
            StringSegment segment = new StringSegment("fob");
            Assert.AreEqual(-1, segment.LastIndexOf('#'));
            Assert.AreEqual(0, segment.LastIndexOf('f'));
            Assert.AreEqual(1, segment.LastIndexOf('o'));
            Assert.AreEqual(2, segment.LastIndexOf('b'));
            
            segment = new StringSegment("aaabbbbcccabc");
            Assert.AreEqual(-1, segment.LastIndexOf('#'));
            Assert.AreEqual(segment.Length - 1, segment.LastIndexOf('c'));
            Assert.AreEqual(segment.Length - 2, segment.LastIndexOf('b'));
            Assert.AreEqual(segment.Length - 3, segment.LastIndexOf('a'));

            segment = new StringSegment("aaabbbbcccabc", 3, 6);
            Assert.AreEqual(-1, segment.LastIndexOf('#'));
            Assert.AreEqual(segment.Length - 1, segment.LastIndexOf('c'));
            Assert.AreEqual(segment.Length - 3, segment.LastIndexOf('b'));
            Assert.AreEqual(-1, segment.LastIndexOf('a'));
        }

        [Owner("visridha")]
        [TestMethod]
        public void StringSegmentSubstring()
        {
            StringSegment segment = new StringSegment("fob");
            Assert.AreEqual("o", segment.Substring(1, 1).GetString());
            Assert.AreEqual("ob", segment.Substring(1, 2).GetString());
            segment = new StringSegment("fob", 1, 2);
            Assert.AreEqual("b", segment.Substring(1, 1).GetString());
        }
    }
}
