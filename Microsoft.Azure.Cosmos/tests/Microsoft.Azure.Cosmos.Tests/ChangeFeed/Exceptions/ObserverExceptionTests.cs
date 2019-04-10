using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    [TestClass]
    public class ObserverExceptionTests
    {
        [TestMethod]
        public void ValidateConstructor()
        {
            Exception exception = new Exception("randomMessage");
            var ex = new ObserverException(exception);
            Assert.AreEqual(exception.Message, ex.InnerException.Message);
            Assert.AreEqual(exception, ex.InnerException);
        }

        // Tests the GetObjectData method and the serialization ctor.
        [TestMethod]
        public void ValidateSerialization_AllFields()
        {
            Exception exception = new Exception("randomMessage");
            var originalException = new ObserverException(exception);
            var buffer = new byte[4096];
            var formatter = new BinaryFormatter();
            var stream1 = new MemoryStream(buffer);
            var stream2 = new MemoryStream(buffer);

            formatter.Serialize(stream1, originalException);
            var deserializedException = (ObserverException)formatter.Deserialize(stream2);

            Assert.AreEqual(originalException.Message, deserializedException.Message);
            Assert.AreEqual(originalException.InnerException.Message, deserializedException.InnerException.Message);
        }

        // Make sure that when some fields are not set, serialization is not broken.
        [TestMethod]
        public void ValidateSerialization_NullFields()
        {
            var originalException = new ObserverException(null);
            var buffer = new byte[4096];
            var formatter = new BinaryFormatter();
            var stream1 = new MemoryStream(buffer);
            var stream2 = new MemoryStream(buffer);

            formatter.Serialize(stream1, originalException);
            var deserializedException = (ObserverException)formatter.Deserialize(stream2);

            Assert.AreEqual(originalException.Message, deserializedException.Message);
            Assert.IsNull(deserializedException.InnerException);
        }
    }
}
