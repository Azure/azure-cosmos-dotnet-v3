//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using Azure.Cosmos.Serialization;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosJsonSerializerWrapperTests
    {
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void FromStream_Throws()
        {
            var handler = new CosmosJsonSerializerWrapper(new CosmosJsonSerializerFails());
            var retval = handler.FromStream<string>(new MemoryStream());
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ToStream_Throws()
        {
            var handler = new CosmosJsonSerializerWrapper(new CosmosJsonSerializerFails());
            var retval = handler.ToStream("testValue");
        }

        private class CosmosJsonSerializerFails : CosmosSerializer
        {
            public override T FromStream<T>(Stream stream)
            {
                return default(T);
            }

            public override Stream ToStream<T>(T input)
            {
                var memoryStream = new MemoryStream();
                memoryStream.Close();
                return memoryStream;
            }
        }
    }
}
