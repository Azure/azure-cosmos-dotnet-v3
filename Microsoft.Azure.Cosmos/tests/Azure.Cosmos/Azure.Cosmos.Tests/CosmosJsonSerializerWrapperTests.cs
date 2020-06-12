//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Azure.Cosmos.Serialization;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosJsonSerializerWrapperTests
    {
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void FromStream_Throws()
        {
            CosmosJsonSerializerWrapper handler = new CosmosJsonSerializerWrapper(new CosmosJsonSerializerFails());
            handler.Deserialize(new MemoryStream(), typeof(string));
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ToStream_Throws()
        {
            CosmosJsonSerializerWrapper handler = new CosmosJsonSerializerWrapper(new CosmosJsonSerializerFails());
            MemoryStream stream = new MemoryStream();
            stream.Close();
            handler.Serialize(stream, "testValue", typeof(string));
        }

        private class CosmosJsonSerializerFails : Azure.Core.ObjectSerializer
        {
            public override object Deserialize(Stream stream, Type returnType)
            {
                return new object();
            }

            public override ValueTask<object> DeserializeAsync(Stream stream, Type returnType)
            {
                return new ValueTask<object>(new object());
            }

            public override void Serialize(Stream stream, object value, Type inputType)
            {
            }

            public override ValueTask SerializeAsync(Stream stream, object value, Type inputType)
            {
                return new ValueTask();
            }
        }
    }
}
