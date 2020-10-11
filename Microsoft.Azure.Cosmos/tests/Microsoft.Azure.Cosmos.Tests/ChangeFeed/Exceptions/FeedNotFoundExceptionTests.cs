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
    public class FeedNotFoundExceptionTests
    {
        [TestMethod]
        public void ValidateRecommendedConstructors()
        {
            string message = "message";
            string lastContinuation = "lastContinuation";
            FeedNotFoundException ex = new FeedNotFoundException(message, lastContinuation);
            Assert.AreEqual(message, ex.Message);
            Assert.AreEqual(lastContinuation, ex.LastContinuation);

            Exception innerException = new Exception();
            ex = new FeedNotFoundException(message, lastContinuation, innerException);
            Assert.AreEqual(message, ex.Message);
            Assert.AreEqual(innerException, ex.InnerException);
            Assert.AreEqual(lastContinuation, ex.LastContinuation);
        }

        // Tests the GetObjectData method and the serialization ctor.
        [TestMethod]
        public void ValidateSerialization_AllFields()
        {
            FeedNotFoundException originalException = new FeedNotFoundException("message", "continuation", new Exception("foo"));
            byte[] buffer = new byte[4096];
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream stream1 = new MemoryStream(buffer);
            MemoryStream stream2 = new MemoryStream(buffer);

            formatter.Serialize(stream1, originalException);
            FeedNotFoundException deserializedException = (FeedNotFoundException)formatter.Deserialize(stream2);

            Assert.AreEqual(originalException.Message, deserializedException.Message);
            Assert.AreEqual(originalException.InnerException.Message, deserializedException.InnerException.Message);
            Assert.AreEqual(originalException.LastContinuation, deserializedException.LastContinuation);
        }

        // Make sure that when some fields are not set, serialization is not broken.
        [TestMethod]
        public void ValidateSerialization_NullFields()
        {
            FeedNotFoundException originalException = new FeedNotFoundException("message", null);
            byte[] buffer = new byte[4096];
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream stream1 = new MemoryStream(buffer);
            MemoryStream stream2 = new MemoryStream(buffer);

            formatter.Serialize(stream1, originalException);
            FeedNotFoundException deserializedException = (FeedNotFoundException)formatter.Deserialize(stream2);

            Assert.AreEqual(originalException.Message, deserializedException.Message);
            Assert.IsNull(deserializedException.InnerException);
            Assert.IsNull(deserializedException.LastContinuation);
        }
    }
}
