//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonFeedStreamHelperTests
    {
        [TestMethod]
        public void HandleLeftOver_WhenBufferExceedsMaxSize_Throws()
        {
            int maxBufferSize = 1024;
            byte[] buffer = new byte[maxBufferSize];
            int dataLength = maxBufferSize;
            int leftOver = dataLength;
            int bytesConsumed = 0;

            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(
                () => JsonFeedStreamHelper.HandleLeftOver(buffer, dataLength, leftOver, bytesConsumed, maxBufferSize));

            StringAssert.Contains(ex.Message, "maximum buffer size");
        }
    }
}
