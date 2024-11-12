//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Text;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ObserverExceptionTests
    {
        [TestMethod]
        public void ValidateConstructor()
        {
            ResponseMessage responseMessage = new ResponseMessage();
            ChangeFeedObserverContextCore observerContext = new ChangeFeedObserverContextCore(Guid.NewGuid().ToString(), feedResponse: responseMessage, Mock.Of<PartitionCheckpointer>(), FeedRangeEpk.FullRange);
            ChangeFeedProcessorContextCore changeFeedProcessorContext = new ChangeFeedProcessorContextCore(observerContext);
            Exception exception = new Exception("randomMessage");
            ChangeFeedProcessorUserException ex = new ChangeFeedProcessorUserException(exception, changeFeedProcessorContext);
            Assert.AreEqual(exception.Message, ex.InnerException.Message);
            Assert.AreEqual(exception, ex.InnerException);
            Assert.ReferenceEquals(changeFeedProcessorContext, ex.ChangeFeedProcessorContext);
        }

        // Tests the GetObjectData method and the serialization ctor.
        [TestMethod]
        public void ValidateSerialization_AllFields()
        {
            ResponseMessage responseMessage = new ResponseMessage();
            ChangeFeedObserverContextCore observerContext = new ChangeFeedObserverContextCore(Guid.NewGuid().ToString(), feedResponse: responseMessage, Mock.Of<PartitionCheckpointer>(), FeedRangeEpk.FullRange);
            ChangeFeedProcessorContextCore changeFeedProcessorContext = new ChangeFeedProcessorContextCore(observerContext);
            Exception exception = new Exception("randomMessage");
            ChangeFeedProcessorUserException originalException = new ChangeFeedProcessorUserException(exception, changeFeedProcessorContext);

            string json = JsonConvert.SerializeObject(originalException);
            ChangeFeedProcessorUserException deserializedException = JsonConvert.DeserializeObject<ChangeFeedProcessorUserException>(json);

            Assert.AreEqual(originalException.Message, deserializedException.Message);
            Assert.AreEqual(originalException.InnerException.Message, deserializedException.InnerException.Message);
        }

        // Make sure that when some fields are not set, serialization is not broken.
        [TestMethod]
        public void ValidateSerialization_NullFields()
        {
            ChangeFeedProcessorUserException originalException = new ChangeFeedProcessorUserException(null, null);
            string json = JsonConvert.SerializeObject(originalException);
            ChangeFeedProcessorUserException deserializedException = JsonConvert.DeserializeObject<ChangeFeedProcessorUserException>(json);

            Assert.AreEqual(originalException.Message, deserializedException.Message);
            Assert.IsNull(deserializedException.InnerException);
        }
    }
}