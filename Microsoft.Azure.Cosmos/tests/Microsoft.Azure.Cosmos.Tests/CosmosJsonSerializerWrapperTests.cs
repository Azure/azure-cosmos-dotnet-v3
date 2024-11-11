//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosJsonSerializerWrapperTests
    {
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void FromStream_Throws()
        {
            CosmosJsonSerializerWrapper handler = new CosmosJsonSerializerWrapper(new CosmosJsonSerializerFails());
            _ = handler.FromStream<string>(new MemoryStream());
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ToStream_Throws()
        {
            CosmosJsonSerializerWrapper handler = new CosmosJsonSerializerWrapper(new CosmosJsonSerializerFails());
            _ = handler.ToStream("testValue");
        }

        private class CosmosJsonSerializerFails : CosmosSerializer
        {
            public override T FromStream<T>(Stream stream)
            {
                return default;
            }

            public override Stream ToStream<T>(T input)
            {
                MemoryStream memoryStream = new MemoryStream();
                memoryStream.Close();
                return memoryStream;
            }
        }
    }
}