//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------


namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Linq;
    using System.Net;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ResponseMockTest
    {
        [TestMethod]
        public void MockItemResponse()
        {
            Mock<ItemResponse<dynamic>> itemResponseMock = new Mock<ItemResponse<dynamic>>();
            itemResponseMock.SetupGet(x => x.RequestCharge).Returns(5);
            itemResponseMock.SetupGet(x => x.Resource).Returns(new { id = "TestItem" });
            itemResponseMock.SetupGet(x => x.Headers).Returns(new Headers() { ETag = "TestEtag" });
            itemResponseMock.SetupGet(x => x.StatusCode).Returns(HttpStatusCode.Found);
            itemResponseMock.SetupGet(x => x.ActivityId).Returns("TestActivityId");

            Assert.AreEqual(5, itemResponseMock.Object.RequestCharge);
            Assert.AreEqual("TestItem", itemResponseMock.Object.Resource.id);
            Assert.AreEqual("TestEtag", itemResponseMock.Object.Headers.ETag);
            Assert.AreEqual(HttpStatusCode.Found, itemResponseMock.Object.StatusCode);
            Assert.AreEqual("TestActivityId", itemResponseMock.Object.ActivityId);
        }

        [TestMethod]
        public void MockContainerResponse()
        {
            Mock<ContainerResponse> itemResponseMock = new Mock<ContainerResponse>();
            itemResponseMock.SetupGet(x => x.RequestCharge).Returns(5);
            itemResponseMock.SetupGet(x => x.Resource).Returns(new ContainerProperties() { Id = "TestContainer" });
            itemResponseMock.SetupGet(x => x.Headers).Returns(new Headers() { ETag = "TestEtag" });
            itemResponseMock.SetupGet(x => x.StatusCode).Returns(HttpStatusCode.Found);
            itemResponseMock.SetupGet(x => x.ActivityId).Returns("TestActivityId");

            Assert.AreEqual(5, itemResponseMock.Object.RequestCharge);
            Assert.AreEqual("TestContainer", itemResponseMock.Object.Resource.Id);
            Assert.AreEqual("TestEtag", itemResponseMock.Object.Headers.ETag);
            Assert.AreEqual(HttpStatusCode.Found, itemResponseMock.Object.StatusCode);
            Assert.AreEqual("TestActivityId", itemResponseMock.Object.ActivityId);
        }

        [TestMethod]
        public void MockDatabaseResponse()
        {
            Mock<DatabaseResponse> itemResponseMock = new Mock<DatabaseResponse>();
            itemResponseMock.SetupGet(x => x.RequestCharge).Returns(5);
            itemResponseMock.SetupGet(x => x.Resource).Returns(new DatabaseProperties() { Id = "TestDatabase" });
            itemResponseMock.SetupGet(x => x.Headers).Returns(new Headers() { ETag = "TestEtag" });
            itemResponseMock.SetupGet(x => x.StatusCode).Returns(HttpStatusCode.Found);
            itemResponseMock.SetupGet(x => x.ActivityId).Returns("TestActivityId");

            Assert.AreEqual(5, itemResponseMock.Object.RequestCharge);
            Assert.AreEqual("TestDatabase", itemResponseMock.Object.Resource.Id);
            Assert.AreEqual("TestEtag", itemResponseMock.Object.Headers.ETag);
            Assert.AreEqual(HttpStatusCode.Found, itemResponseMock.Object.StatusCode);
            Assert.AreEqual("TestActivityId", itemResponseMock.Object.ActivityId);
        }

        [TestMethod]
        public void MockStoredProcedureResponse()
        {
            Mock<StoredProcedureResponse> itemResponseMock = new Mock<StoredProcedureResponse>();
            itemResponseMock.SetupGet(x => x.RequestCharge).Returns(5);
            itemResponseMock.SetupGet(x => x.Resource).Returns(new StoredProcedureProperties() { Id = "TestSproc" });
            itemResponseMock.SetupGet(x => x.Headers).Returns(new Headers() { ETag = "TestEtag" });
            itemResponseMock.SetupGet(x => x.StatusCode).Returns(HttpStatusCode.Found);
            itemResponseMock.SetupGet(x => x.ActivityId).Returns("TestActivityId");

            Assert.AreEqual(5, itemResponseMock.Object.RequestCharge);
            Assert.AreEqual("TestSproc", itemResponseMock.Object.Resource.Id);
            Assert.AreEqual("TestEtag", itemResponseMock.Object.Headers.ETag);
            Assert.AreEqual(HttpStatusCode.Found, itemResponseMock.Object.StatusCode);
            Assert.AreEqual("TestActivityId", itemResponseMock.Object.ActivityId);
        }

        [TestMethod]
        public void MockFeedResponse()
        {
            Mock<FeedResponse<dynamic>> itemResponseMock = new Mock<FeedResponse<dynamic>>();
            itemResponseMock.SetupGet(x => x.RequestCharge).Returns(5);
            itemResponseMock.SetupGet(x => x.Resource).Returns(new dynamic[] { new { id = "TestItem" } });
            itemResponseMock.SetupGet(x => x.Headers).Returns(new Headers() { ETag = "TestEtag" });
            itemResponseMock.SetupGet(x => x.StatusCode).Returns(HttpStatusCode.Found);
            itemResponseMock.SetupGet(x => x.ActivityId).Returns("TestActivityId");

            Assert.AreEqual(5, itemResponseMock.Object.RequestCharge);
            Assert.AreEqual("TestItem", itemResponseMock.Object.Resource.First().id);
            Assert.AreEqual("TestEtag", itemResponseMock.Object.Headers.ETag);
            Assert.AreEqual(HttpStatusCode.Found, itemResponseMock.Object.StatusCode);
            Assert.AreEqual("TestActivityId", itemResponseMock.Object.ActivityId);
        }
    }
}