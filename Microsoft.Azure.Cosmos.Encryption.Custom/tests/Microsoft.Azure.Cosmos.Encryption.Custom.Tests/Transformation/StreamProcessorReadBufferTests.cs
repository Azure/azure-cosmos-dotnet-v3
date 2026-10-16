//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class StreamProcessorReadBufferTests
    {
        [TestMethod]
        public void HandleReadBuffer_WhenShortReadMakesNoProgress_DoesNotGrow()
        {
            using ArrayPoolManager arrayPoolManager = new ();
            byte[] buffer = arrayPoolManager.Rent(16);
            int dataSize = buffer.Length - 1;

            byte[] result = StreamProcessor.HandleReadBuffer(
                buffer,
                dataSize,
                leftOver: dataSize,
                isFinalBlock: false,
                arrayPoolManager,
                maxBufferSize: buffer.Length * 4);

            Assert.AreSame(buffer, result);
            Assert.AreEqual(1, arrayPoolManager.RentedBufferCount);
        }

        [TestMethod]
        public void HandleReadBuffer_WhenIncompleteTokenFillsBuffer_GrowsAndPreservesData()
        {
            using ArrayPoolManager arrayPoolManager = new ();
            byte[] buffer = arrayPoolManager.Rent(16);
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)(i % byte.MaxValue);
            }

            byte[] result = StreamProcessor.HandleReadBuffer(
                buffer,
                dataSize: buffer.Length,
                leftOver: buffer.Length,
                isFinalBlock: false,
                arrayPoolManager,
                maxBufferSize: buffer.Length * 4);

            Assert.AreNotSame(buffer, result);
            Assert.IsTrue(result.Length >= buffer.Length * 2);
            CollectionAssert.AreEqual(buffer, result.AsSpan(0, buffer.Length).ToArray());
            Assert.AreEqual(2, arrayPoolManager.RentedBufferCount);
        }

        [TestMethod]
        public void HandleReadBuffer_WhenIncompleteTokenFillsMaximumBuffer_Throws()
        {
            using ArrayPoolManager arrayPoolManager = new ();
            byte[] buffer = arrayPoolManager.Rent(16);

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => StreamProcessor.HandleReadBuffer(
                    buffer,
                    dataSize: buffer.Length,
                    leftOver: buffer.Length,
                    isFinalBlock: false,
                    arrayPoolManager,
                    maxBufferSize: buffer.Length));

            StringAssert.Contains(exception.Message, "maximum buffer size");
            Assert.AreEqual(1, arrayPoolManager.RentedBufferCount);
        }
    }
}
#endif
