using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    [TestClass]
    public class LeaseLostExceptionTests
    {
        [TestMethod]
        public void ValidateRecommendedConstructors()
        {
            // Default ctor.
            var ex = new LeaseLostException();
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
            var lease = Mock.Of<DocumentServiceLease>();
            var ex = new LeaseLostException(lease);
            Assert.AreEqual(lease, ex.Lease);
            Assert.IsNotNull(ex.Message);
        }

        [TestMethod]
        public void ValidateIsGoneConstructor()
        {
            var lease = Mock.Of<DocumentServiceLease>();
            var innerException = new Exception();
            var ex = new LeaseLostException(lease, innerException, true);

            Assert.IsNotNull(ex.Message);
            Assert.AreEqual(lease, ex.Lease);
            Assert.AreEqual(innerException, ex.InnerException);
            Assert.IsTrue(ex.IsGone);
        }

        // Tests the GetObjectData method and the serialization ctor.
        [TestMethod]
        public void ValidateSerialization_AllFields()
        {
            var lease = new DocumentServiceLeaseCore() { LeaseId = "id" };
            var originalException = new LeaseLostException(lease, new Exception("foo"), true);
            var buffer = new byte[4096];
            var formatter = new BinaryFormatter();
            var stream1 = new MemoryStream(buffer);
            var stream2 = new MemoryStream(buffer);

            formatter.Serialize(stream1, originalException);
            var deserializedException = (LeaseLostException)formatter.Deserialize(stream2);

            Assert.AreEqual(originalException.Message, deserializedException.Message);
            Assert.AreEqual(originalException.InnerException.Message, deserializedException.InnerException.Message);
            Assert.AreEqual(originalException.Lease.Id, deserializedException.Lease.Id);
            Assert.AreEqual(originalException.IsGone, deserializedException.IsGone);
        }

        // Make sure that when some fields are not set, serialization is not broken.
        [TestMethod]
        public void ValidateSerialization_NullFields()
        {
            var originalException = new LeaseLostException("message");
            var buffer = new byte[4096];
            var formatter = new BinaryFormatter();
            var stream1 = new MemoryStream(buffer);
            var stream2 = new MemoryStream(buffer);

            formatter.Serialize(stream1, originalException);
            var deserializedException = (LeaseLostException)formatter.Deserialize(stream2);

            Assert.AreEqual(originalException.Message, deserializedException.Message);
            Assert.IsNull(deserializedException.InnerException);
            Assert.IsNull(deserializedException.Lease);
            Assert.IsFalse(deserializedException.IsGone);
        }
    }
}
