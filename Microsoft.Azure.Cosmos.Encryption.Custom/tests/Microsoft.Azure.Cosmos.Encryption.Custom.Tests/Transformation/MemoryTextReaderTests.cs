//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MemoryTextReaderTests
    {
        private static MemoryTextReader Create(string value)
        {
            return new MemoryTextReader(value.ToCharArray().AsMemory());
        }

        [TestMethod]
        public void ReadToEnd_WhenFreshReader_ReturnsFullContent()
        {
            using MemoryTextReader reader = Create("hello world");

            string result = reader.ReadToEnd();

            Assert.AreEqual("hello world", result);
        }

        [TestMethod]
        public void ReadToEnd_AfterPartialRead_ReturnsRemainderOnly()
        {
            using MemoryTextReader reader = Create("abcdef");
            _ = reader.Read();
            _ = reader.Read();

            string result = reader.ReadToEnd();

            Assert.AreEqual("cdef", result);
        }

        [TestMethod]
        public void ReadToEnd_WhenEmptyInput_ReturnsEmptyString()
        {
            using MemoryTextReader reader = Create(string.Empty);

            Assert.AreEqual(string.Empty, reader.ReadToEnd());
        }

        [TestMethod]
        public void ReadToEnd_AfterFullyConsumed_ReturnsEmptyString()
        {
            using MemoryTextReader reader = Create("xyz");
            char[] buffer = new char[16];
            int n = reader.Read(buffer, 0, buffer.Length);
            Assert.AreEqual(3, n);

            Assert.AreEqual(string.Empty, reader.ReadToEnd());
        }

        [TestMethod]
        public void ReadToEnd_CalledTwice_SecondCallReturnsEmptyString()
        {
            using MemoryTextReader reader = Create("payload");

            Assert.AreEqual("payload", reader.ReadToEnd());
            Assert.AreEqual(string.Empty, reader.ReadToEnd());
        }

        [TestMethod]
        public void ReadToEnd_AfterClose_Throws()
        {
            MemoryTextReader reader = Create("data");
            reader.Close();

            Assert.ThrowsException<InvalidOperationException>(() => reader.ReadToEnd());
        }

        [TestMethod]
        public void ReadToEnd_OnUnicodeContent_PreservesCharacters()
        {
            string input = "héllo → 🙂 αβγ";
            using MemoryTextReader reader = Create(input);

            Assert.AreEqual(input, reader.ReadToEnd());
        }

        [TestMethod]
        public void ReadToEnd_MatchesStringReaderBehaviour()
        {
            string input = "line1\r\nline2\nline3";
            using MemoryTextReader memoryReader = Create(input);
            using StringReader stringReader = new (input);

            Assert.AreEqual(stringReader.ReadToEnd(), memoryReader.ReadToEnd());
        }

        [TestMethod]
        public void ReadToEnd_AfterPartialRead_MatchesStringReaderBehaviour()
        {
            string input = "abcdefghij";
            using MemoryTextReader memoryReader = Create(input);
            using StringReader stringReader = new (input);

            char[] buffer = new char[4];
            int n1 = memoryReader.Read(buffer, 0, buffer.Length);
            int n2 = stringReader.Read(buffer, 0, buffer.Length);
            Assert.AreEqual(n2, n1);

            Assert.AreEqual(stringReader.ReadToEnd(), memoryReader.ReadToEnd());
        }

        [TestMethod]
        public void Peek_WhenFreshReader_ReturnsFirstChar_WithoutAdvancingPosition()
        {
            using MemoryTextReader reader = Create("abc");

            Assert.AreEqual((int)'a', reader.Peek());
            Assert.AreEqual((int)'a', reader.Peek());
            Assert.AreEqual((int)'a', reader.Read());
            Assert.AreEqual((int)'b', reader.Peek());
        }

        [TestMethod]
        public void Peek_AtEnd_ReturnsMinusOne()
        {
            using MemoryTextReader reader = Create("x");
            _ = reader.Read();

            Assert.AreEqual(-1, reader.Peek());
        }

        [TestMethod]
        public void Peek_WhenEmpty_ReturnsMinusOne()
        {
            using MemoryTextReader reader = Create(string.Empty);

            Assert.AreEqual(-1, reader.Peek());
        }

        [TestMethod]
        public void Peek_AfterClose_Throws()
        {
            MemoryTextReader reader = Create("x");
            reader.Close();

            Assert.ThrowsException<InvalidOperationException>(() => reader.Peek());
        }

        [TestMethod]
        public void Read_AfterClose_Throws()
        {
            MemoryTextReader reader = Create("x");
            reader.Close();

            Assert.ThrowsException<InvalidOperationException>(() => reader.Read());
        }

        [TestMethod]
        public void ReadBuffer_AfterClose_Throws()
        {
            MemoryTextReader reader = Create("x");
            reader.Close();

            Assert.ThrowsException<InvalidOperationException>(
                () => reader.Read(new char[1], 0, 1));
        }

        [TestMethod]
        public void ReadBuffer_NullBuffer_Throws()
        {
            using MemoryTextReader reader = Create("x");

            Assert.ThrowsException<ArgumentNullException>(() => reader.Read(null, 0, 1));
        }

        [TestMethod]
        public void ReadBuffer_NegativeIndex_Throws()
        {
            using MemoryTextReader reader = Create("x");

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => reader.Read(new char[4], -1, 1));
        }

        [TestMethod]
        public void ReadBuffer_NegativeCount_Throws()
        {
            using MemoryTextReader reader = Create("x");

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => reader.Read(new char[4], 0, -1));
        }

        [TestMethod]
        public void ReadBuffer_CountExceedsBuffer_Throws()
        {
            using MemoryTextReader reader = Create("x");

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => reader.Read(new char[4], 2, 10));
        }

        [TestMethod]
        public void ReadBuffer_WhenCountLargerThanRemaining_ReturnsOnlyRemaining()
        {
            using MemoryTextReader reader = Create("abc");
            char[] dest = new char[16];

            int n = reader.Read(dest, 2, 10);
            Assert.AreEqual(3, n);
            Assert.AreEqual('a', dest[2]);
            Assert.AreEqual('b', dest[3]);
            Assert.AreEqual('c', dest[4]);
        }

        [TestMethod]
        public void ReadBuffer_WhenAtEnd_ReturnsZero()
        {
            using MemoryTextReader reader = Create("ab");
            _ = reader.ReadToEnd();

            Assert.AreEqual(0, reader.Read(new char[4], 0, 4));
        }

        [TestMethod]
        public void ReadLine_IteratesExpectedLines()
        {
            using MemoryTextReader reader = Create("a\r\nbc\nd");
            List<string> lines = new ();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lines.Add(line);
            }

            CollectionAssert.AreEqual(new[] { "a", "bc", "d" }, lines);
        }

        [TestMethod]
        public void ReadLine_AfterClose_Throws()
        {
            MemoryTextReader reader = Create("x");
            reader.Close();

            Assert.ThrowsException<InvalidOperationException>(() => reader.ReadLine());
        }
    }
}
