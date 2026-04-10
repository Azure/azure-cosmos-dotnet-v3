//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class CosmosOperationCanceledTests
    {

        [TestMethod]
        public void SerializationValidation()
        {
            //create test exception
            CosmosOperationCanceledException originalException = new CosmosOperationCanceledException(
                new OperationCanceledException("error message"),
                new CosmosTraceDiagnostics(NoOpTrace.Singleton));

            //serialize exception
            string serialized = JsonConvert.SerializeObject(originalException);

            CosmosOperationCanceledException deserializedExceptoin =
                JsonConvert.DeserializeObject<CosmosOperationCanceledException>(serialized);

            //Asserts
            Assert.AreEqual(originalException.ToString(), deserializedExceptoin.ToString());
            Assert.AreEqual(originalException.Message, deserializedExceptoin.Message);
            Assert.AreEqual(originalException.GetBaseException().Message, deserializedExceptoin.GetBaseException().Message);
            Assert.AreEqual(originalException.GetBaseException().ToString(), deserializedExceptoin.GetBaseException().ToString());
            Assert.AreEqual(originalException.GetBaseException().HResult, deserializedExceptoin.GetBaseException().HResult);
        }
    }
}