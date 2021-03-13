namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Net;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class BatchOperationResultTests
    {
        static TransactionalBatchOperationResult CreateTestResult() => new TransactionalBatchOperationResult(HttpStatusCode.Unused)
        {
            SubStatusCode = Documents.SubStatusCodes.CanNotAcquireOfferOwnerLock,
            ETag = "TestETag",
            ResourceStream = new MemoryStream(),
            RequestCharge = 1.5,
            RetryAfter = TimeSpan.FromMilliseconds(1234)
        };

        [TestMethod]
        public void StatusCodeIsSetThroughCtor()
        {
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.Unused);

            Assert.AreEqual(HttpStatusCode.Unused, result.StatusCode);
        }

        [TestMethod]
        public void PropertiesAreSetThroughCopyCtor()
        {
            TransactionalBatchOperationResult other = CreateTestResult();
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(other);

            Assert.AreEqual(other.StatusCode, result.StatusCode);
            Assert.AreEqual(other.SubStatusCode, result.SubStatusCode);
            Assert.AreEqual(other.ETag, result.ETag);
            Assert.AreEqual(other.RequestCharge, result.RequestCharge);
            Assert.AreEqual(other.RetryAfter, result.RetryAfter);
            Assert.AreSame(other.ResourceStream, result.ResourceStream);
        }
        
        [TestMethod]
        public void PropertiesAreSetThroughGenericCtor()
        {
            TransactionalBatchOperationResult other = CreateTestResult();
            object testObject = new object();
            TransactionalBatchOperationResult<object> result = new TransactionalBatchOperationResult<object>(other, testObject);

            Assert.AreEqual(other.StatusCode, result.StatusCode);
            Assert.AreEqual(other.SubStatusCode, result.SubStatusCode);
            Assert.AreEqual(other.ETag, result.ETag);
            Assert.AreEqual(other.RequestCharge, result.RequestCharge);
            Assert.AreEqual(other.RetryAfter, result.RetryAfter);
            Assert.AreSame(other.ResourceStream, result.ResourceStream);
            Assert.AreSame(testObject, result.Resource);
        }

        [TestMethod]
        public void ToResponseMessageHasPropertiesMapped()
        {
            TransactionalBatchOperationResult result = CreateTestResult();

            ResponseMessage response = result.ToResponseMessage();

            Assert.AreEqual(result.StatusCode, response.StatusCode);
            Assert.AreEqual(result.SubStatusCode, response.Headers.SubStatusCode);
            Assert.AreEqual(result.ETag, response.Headers.ETag);
            Assert.AreEqual(result.RequestCharge, response.Headers.RequestCharge);
            Assert.AreEqual(result.RetryAfter, response.Headers.RetryAfter);
            Assert.AreSame(result.ResourceStream, response.Content);
            Assert.IsNotNull(response.Diagnostics);
        }

        [TestMethod]
        public void IsSuccessStatusCodeTrueFor200to299()
        {
            for (int x = 100; x < 999; ++x)
            {
                TransactionalBatchOperationResult result = new TransactionalBatchOperationResult((HttpStatusCode)x);
                bool success = x >= 200 && x <= 299;
                Assert.AreEqual(success, result.IsSuccessStatusCode);
            }
        }

        [TestMethod]
        public void CanBeMocked()
        {
            Mock<TransactionalBatchOperationResult> mockResult = new Mock<TransactionalBatchOperationResult>();
            TransactionalBatchOperationResult result = mockResult.Object;

            Assert.AreEqual(default(HttpStatusCode), result.StatusCode);
        }

        [TestMethod]
        public void GenericCanBeMocked()
        {
            Mock<TransactionalBatchOperationResult<object>> mockResult = new Mock<TransactionalBatchOperationResult<object>>();
            TransactionalBatchOperationResult<object> result = mockResult.Object;

            Assert.AreEqual(default(HttpStatusCode), result.StatusCode);
            Assert.AreEqual(default(object), result.Resource);
        }
    }
}
