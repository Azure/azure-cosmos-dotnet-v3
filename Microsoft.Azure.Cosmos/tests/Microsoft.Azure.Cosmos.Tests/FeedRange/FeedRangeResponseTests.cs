//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.FeedRange
{
    using System;
    using System.IO;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class FeedRangeResponseTests
    {
        [TestMethod]
        public void FeedRangeResponse_PromotesContinuation()
        {
            string continuation = Guid.NewGuid().ToString();
            Mock<FeedRangeContinuation> feedContinuation = new Mock<FeedRangeContinuation>();
            feedContinuation
                .Setup(f => f.ToString())
                .Returns(continuation);

            ResponseMessage feedRangeResponse = FeedRangeResponse.CreateSuccess(new ResponseMessage(), feedContinuation.Object);
            Assert.AreEqual(continuation, feedRangeResponse.ContinuationToken);
        }

        [TestMethod]
        public void FeedRangeResponse_ContinuationNull_IfDone()
        {
            Mock<FeedRangeContinuation> feedContinuation = new Mock<FeedRangeContinuation>();
            feedContinuation
                .Setup(f => f.IsDone)
                .Returns(true);

            ResponseMessage feedRangeResponse = FeedRangeResponse.CreateSuccess(new ResponseMessage(), feedContinuation.Object);
            Assert.IsNull(feedRangeResponse.ContinuationToken);
        }

        [TestMethod]
        public void FeedRangeResponse_ContinuationNull_IfFailure()
        {
            ResponseMessage responseMessage = new ResponseMessage();
            responseMessage.Headers.ContinuationToken = Guid.NewGuid().ToString();
            ResponseMessage feedRangeResponse = FeedRangeResponse.CreateFailure(responseMessage);
            Assert.IsNull(feedRangeResponse.ContinuationToken);
        }

        [TestMethod]
        public void FeedRangeResponse_ResponseIsAccessible()
        {
            ResponseMessage original = new ResponseMessage(
                System.Net.HttpStatusCode.OK,
                new RequestMessage(),
                new Headers(),
                CosmosExceptionFactory.CreateBadRequestException("test"),
                CosmosDiagnosticsContext.Create(new RequestOptions()))
            {
                Content = Mock.Of<MemoryStream>()
            };
            Mock<FeedRangeContinuation> feedContinuation = new Mock<FeedRangeContinuation>();

            ResponseMessage feedRangeResponse = FeedRangeResponse.CreateSuccess(original, feedContinuation.Object);
            Assert.AreEqual(original.Content, feedRangeResponse.Content);
            Assert.AreEqual(original.StatusCode, feedRangeResponse.StatusCode);
            Assert.AreEqual(original.RequestMessage, feedRangeResponse.RequestMessage);
            Assert.AreEqual(original.Headers, feedRangeResponse.Headers);
            Assert.AreEqual(original.CosmosException, feedRangeResponse.CosmosException);
            Assert.AreEqual(original.DiagnosticsContext, feedRangeResponse.DiagnosticsContext);
        }
    }
}
