//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ObserverExceptionTests
    {
        [TestMethod]
        public void ValidateConstructor()
        {
            Exception exception = new Exception("randomMessage");
            ChangeFeedProcessorUserException ex = new ChangeFeedProcessorUserException(exception);
            Assert.AreEqual(exception.Message, ex.InnerException.Message);
            Assert.AreEqual(exception, ex.InnerException);
        }

        // Tests the GetObjectData method and the serialization ctor.
        [TestMethod]
        public void ValidateSerialization_AllFields()
        {
            Exception exception = new Exception("randomMessage");
            ChangeFeedProcessorUserException originalException = new ChangeFeedProcessorUserException(exception);
            byte[] buffer = new byte[4096];
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream stream1 = new MemoryStream(buffer);
            MemoryStream stream2 = new MemoryStream(buffer);

            formatter.Serialize(stream1, originalException);
            ChangeFeedProcessorUserException deserializedException = (ChangeFeedProcessorUserException)formatter.Deserialize(stream2);

            Assert.AreEqual(originalException.Message, deserializedException.Message);
            Assert.AreEqual(originalException.InnerException.Message, deserializedException.InnerException.Message);
        }

        // Make sure that when some fields are not set, serialization is not broken.
        [TestMethod]
        public void ValidateSerialization_NullFields()
        {
            ChangeFeedProcessorUserException originalException = new ChangeFeedProcessorUserException(null);
            byte[] buffer = new byte[4096];
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream stream1 = new MemoryStream(buffer);
            MemoryStream stream2 = new MemoryStream(buffer);

            formatter.Serialize(stream1, originalException);
            ChangeFeedProcessorUserException deserializedException = (ChangeFeedProcessorUserException)formatter.Deserialize(stream2);

            Assert.AreEqual(originalException.Message, deserializedException.Message);
            Assert.IsNull(deserializedException.InnerException);
        }
    }
}
