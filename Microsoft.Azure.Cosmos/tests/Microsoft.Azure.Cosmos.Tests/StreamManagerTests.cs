//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="StreamManager"/>
    /// </summary>
    [TestClass]
    public class StreamManagerTests
    {
        private readonly static Random rnd = new Random();

        [TestMethod]
        [DataRow(000128)]
        [DataRow(001024)]
        [DataRow(005120)]
        [DataRow(102400)]
        public void TestCreationOfReadonlyStream(int arraySize)
        {
            byte[] bytes = new byte[arraySize];
            rnd.NextBytes(bytes);

            Stream stream = StreamManager.GetReadonlyStream(bytes, 0, bytes.Length);

            Assert.IsFalse(stream.CanWrite); // Ensure stream is readonly.
            Assert.IsTrue(stream.CanSeek); // Ensure stream is seekable.

            Assert.IsTrue(stream is MemoryStream);
            Assert.IsFalse(stream is RecyclableMemoryStream);

            AssertByteArraysAreEqual(bytes, ((MemoryStream)stream).ToArray());
        }

        [TestMethod]
        [DataRow(000128)]
        [DataRow(001024)]
        [DataRow(005120)]
        [DataRow(102400)]
        public void TestCreationOfRegularStreams(int arraySize)
        {
            byte[] bytes = new byte[arraySize];
            rnd.NextBytes(bytes);

            Stream stream = StreamManager.GetStream(nameof(TestCreationOfRegularStreams), bytes, 0, bytes.Length);

            Assert.IsTrue(stream.CanWrite); // Ensure stream is writable.
            Assert.IsTrue(stream.CanSeek); // Ensure stream is seekable.

            Assert.IsTrue(stream is MemoryStream);
            Assert.IsTrue(stream is RecyclableMemoryStream);

            AssertByteArraysAreEqual(bytes, ((MemoryStream)stream).ToArray());
        }

        [TestMethod]
        public void TestCreationOfEmptyStream()
        {
            Stream stream = StreamManager.GetStream(nameof(TestCreationOfEmptyStream));

            Assert.IsTrue(stream is MemoryStream);
            Assert.IsTrue(stream is RecyclableMemoryStream);

            Assert.IsTrue(stream.CanWrite); // Ensure stream is writable.
            Assert.IsTrue(stream.CanSeek); // Ensure stream is seekable.
            Assert.AreEqual(0, stream.Position);
            Assert.AreEqual(0, stream.Length);
        }

        private static void AssertByteArraysAreEqual(byte[] expected, byte[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], actual[i]);
            }
        }
    }
}