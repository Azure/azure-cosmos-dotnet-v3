//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using System;
    using System.IO;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class StreamExtensionTests
    {
        [TestMethod]
        [Owner("kirankk")]
        // Verifies that the StoreResponse's successful response Stream has a publicly accesible Buffer
        public void CreateBufferExposableStreamFromByteArray_CreatesExposableStream()
        {
            byte[] body = new byte[] { 0, 1, 127, 128, 255 };
            using MemoryStream memoryStream = StreamExtension.CreateExportableMemoryStream(body);
            Assert.AreEqual(0, memoryStream.Position);
            Assert.IsTrue(memoryStream.TryGetBuffer(out ArraySegment<byte> buffer));
            CollectionAssert.AreEqual(body, buffer.Array);
        }
    }
}