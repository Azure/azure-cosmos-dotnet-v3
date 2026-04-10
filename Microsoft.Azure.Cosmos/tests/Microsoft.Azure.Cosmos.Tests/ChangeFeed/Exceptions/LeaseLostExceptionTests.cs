//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class LeaseLostExceptionTests
    {
        [TestMethod]
        public void ValidateRecommendedConstructors()
        {
            // Default ctor.
            LeaseLostException ex = new LeaseLostException();
            Assert.IsNotNull(ex.Message);

            // ctor(message).
            string message = "message";
            ex = new LeaseLostException(message);
            Assert.AreEqual(message, ex.Message);

            // ctor()
            Exception innerException = new Exception();
            ex = new LeaseLostException(message, innerException);
            Assert.AreEqual(message, ex.Message);
            Assert.AreEqual(innerException, ex.InnerException);
        }

        [TestMethod]
        public void ValidateLeaseContructor()
        {
            DocumentServiceLease lease = Mock.Of<DocumentServiceLease>();
            LeaseLostException ex = new LeaseLostException(lease);
            Assert.AreEqual(lease, ex.Lease);
            Assert.IsNotNull(ex.Message);
        }

        [TestMethod]
        public void ValidateIsGoneConstructor()
        {
            DocumentServiceLease lease = Mock.Of<DocumentServiceLease>();
            Exception innerException = new Exception();
            LeaseLostException ex = new LeaseLostException(lease, innerException, true);

            Assert.IsNotNull(ex.Message);
            Assert.AreEqual(lease, ex.Lease);
            Assert.AreEqual(innerException, ex.InnerException);
            Assert.IsTrue(ex.IsGone);
        }

        // Tests the GetObjectData method and the serialization ctor.
        [TestMethod]
        public void ValidateSerialization_AllFields()
        {
            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore() { LeaseId = "id" };
            LeaseLostException originalException = new LeaseLostException(lease, new Exception("foo"), true);

            string serialized = JsonConvert.SerializeObject(originalException);
            LeaseLostException deserializedException = JsonConvert.DeserializeObject<LeaseLostException>(serialized);

            Assert.AreEqual(originalException.Message, deserializedException.Message);
            Assert.AreEqual(originalException.InnerException.Message, deserializedException.InnerException.Message);
            Assert.AreEqual(originalException.Lease.Id, deserializedException.Lease.Id);
            Assert.AreEqual(originalException.IsGone, deserializedException.IsGone);
        }

        // Make sure that when some fields are not set, serialization is not broken.
        [TestMethod]
        public void ValidateSerialization_NullFields()
        {
            LeaseLostException originalException = new LeaseLostException("message");

            string serialized = JsonConvert.SerializeObject(originalException);
            LeaseLostException deserializedException = JsonConvert.DeserializeObject<LeaseLostException>(serialized);

            Assert.AreEqual(originalException.Message, deserializedException.Message);
            Assert.IsNull(deserializedException.InnerException);
            Assert.IsNull(deserializedException.Lease);
            Assert.IsFalse(deserializedException.IsGone);
        }
    }
}