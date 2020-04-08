//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.FeedRange
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class FeedRangeContinuationTests
    {
        [TestMethod]
        public void FeedRangeCompositeContinuation_MoveToNextTokenCircles()
        {
            const string containerRid = "containerRid";
            List<Documents.Routing.Range<string>> keyRanges = new List<Documents.Routing.Range<string>>()
            {
                new Documents.Routing.Range<string>("A", "B", true, false),
                new Documents.Routing.Range<string>("D", "E", true, false),
            };
            FeedRangeCompositeContinuation token = new FeedRangeCompositeContinuation(containerRid, Mock.Of<FeedRangeInternal>(), keyRanges);
            Assert.AreEqual(keyRanges[0].Min, token.CompositeContinuationTokens.Peek().Range.Min);
            token.UpdateContinuation("something");
            Assert.AreEqual(keyRanges[1].Min, token.CompositeContinuationTokens.Peek().Range.Min);
            token.UpdateContinuation("something");
            Assert.AreEqual(keyRanges[0].Min, token.CompositeContinuationTokens.Peek().Range.Min);
            token.UpdateContinuation("something");
            Assert.AreEqual(keyRanges[1].Min, token.CompositeContinuationTokens.Peek().Range.Min);
        }

        [TestMethod]
        public void FeedRangeCompositeContinuation_TryParse()
        {
            const string containerRid = "containerRid";
            List<Documents.Routing.Range<string>> keyRanges = new List<Documents.Routing.Range<string>>()
            {
                new Documents.Routing.Range<string>("A", "B", true, false),
                new Documents.Routing.Range<string>("D", "E", true, false),
            };
            FeedRangeInternal feedRangeInternal = new FeedRangeEPK(new Documents.Routing.Range<string>("A", "E", true, false));
            FeedRangeCompositeContinuation token = new FeedRangeCompositeContinuation(containerRid, feedRangeInternal, keyRanges);
            Assert.IsTrue(FeedRangeContinuation.TryCreateFromString(token.ToString(), out _));
            Assert.IsFalse(FeedRangeContinuation.TryCreateFromString("whatever", out _));
        }

        [TestMethod]
        public void FeedRangeCompositeContinuation_RequestVisitor()
        {
            const string containerRid = "containerRid";
            const string continuation = "continuation";
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("A", "B", true, false);
            FeedRangeCompositeContinuation token = new FeedRangeCompositeContinuation(containerRid, Mock.Of<FeedRangeInternal>(), new List<Documents.Routing.Range<string>>() { range }, continuation);
            RequestMessage requestMessage = new RequestMessage();
            requestMessage.OperationType = Documents.OperationType.ReadFeed;
            requestMessage.ResourceType = Documents.ResourceType.Document;
            FeedRangeVisitor feedRangeVisitor = new FeedRangeVisitor(requestMessage);
            token.Accept(feedRangeVisitor, ChangeFeedRequestOptions.FillContinuationToken);
            Assert.AreEqual(range.Min, requestMessage.Properties[HandlerConstants.StartEpkString]);
            Assert.AreEqual(range.Max, requestMessage.Properties[HandlerConstants.EndEpkString]);
            Assert.AreEqual(continuation, requestMessage.Headers.IfNoneMatch);
            Assert.IsTrue(requestMessage.IsPartitionKeyRangeHandlerRequired);
        }

        [TestMethod]
        public void FeedRangeCompositeContinuation_RequestVisitor_IfEPKAlreadyExists()
        {
            const string containerRid = "containerRid";
            const string continuation = "continuation";
            string epkString = Guid.NewGuid().ToString();
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("A", "B", true, false);
            FeedRangeCompositeContinuation token = new FeedRangeCompositeContinuation(containerRid, Mock.Of<FeedRangeInternal>(), new List<Documents.Routing.Range<string>>() { range }, continuation);
            RequestMessage requestMessage = new RequestMessage();
            FeedRangeVisitor feedRangeVisitor = new FeedRangeVisitor(requestMessage);
            requestMessage.Properties[HandlerConstants.StartEpkString] = epkString;
            requestMessage.Properties[HandlerConstants.EndEpkString] = epkString;
            token.Accept(feedRangeVisitor, ChangeFeedRequestOptions.FillContinuationToken);
            Assert.AreEqual(epkString, requestMessage.Properties[HandlerConstants.StartEpkString]);
            Assert.AreEqual(epkString, requestMessage.Properties[HandlerConstants.EndEpkString]);
        }

        [TestMethod]
        public void FeedRangeSimpleContinuation_TryParse()
        {
            const string containerRid = "containerRid";
            FeedRangeInternal feedRangeInternal = new FeedRangePartitionKey(new PartitionKey("test"));
            FeedRangeSimpleContinuation token = new FeedRangeSimpleContinuation(containerRid, feedRangeInternal);
            Assert.IsTrue(FeedRangeSimpleContinuation.TryCreateFromString(token.ToString(), out _));
            Assert.IsFalse(FeedRangeSimpleContinuation.TryCreateFromString("whatever", out _));
        }

        [TestMethod]
        public void FeedRangeSimpleContinuation_RequestVisitor()
        {
            const string containerRid = "containerRid";
            const string continuation = "continuation";
            FeedRangeSimpleContinuation token = new FeedRangeSimpleContinuation(containerRid, Mock.Of<FeedRangeInternal>(), continuation);
            RequestMessage requestMessage = new RequestMessage();
            FeedRangeVisitor feedRangeVisitor = new FeedRangeVisitor(requestMessage);
            token.Accept(feedRangeVisitor, ChangeFeedRequestOptions.FillContinuationToken);
            Assert.AreEqual(continuation, requestMessage.Headers.IfNoneMatch);
        }

        [TestMethod]
        public async Task FeedRangeCompositeContinuation_ShouldRetry()
        {
            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
            {
                FeedRangeContinuationTests.BuildTokenForRange("A", "C", "token1"),
                FeedRangeContinuationTests.BuildTokenForRange("C", "F", "token2")
            };

            FeedRangeCompositeContinuation feedRangeCompositeContinuation = new FeedRangeCompositeContinuation(Guid.NewGuid().ToString(), Mock.Of<FeedRangeInternal>(), compositeContinuationTokens);

            ContainerCore containerCore = Mock.Of<ContainerCore>();
            ResponseMessage okResponse = new ResponseMessage(HttpStatusCode.OK);
            okResponse.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";
            Assert.IsFalse(await feedRangeCompositeContinuation.ShouldRetryAsync(containerCore, okResponse, default(CancellationToken)));

            // A 304 on a multi Range token should cycle on all available ranges before stopping retrying
            Assert.IsTrue(await feedRangeCompositeContinuation.ShouldRetryAsync(containerCore, new ResponseMessage(HttpStatusCode.NotModified), default(CancellationToken)));
            feedRangeCompositeContinuation.UpdateContinuation(Guid.NewGuid().ToString());
            Assert.IsTrue(await feedRangeCompositeContinuation.ShouldRetryAsync(containerCore, new ResponseMessage(HttpStatusCode.NotModified), default(CancellationToken)));
            feedRangeCompositeContinuation.UpdateContinuation(Guid.NewGuid().ToString());
            Assert.IsFalse(await feedRangeCompositeContinuation.ShouldRetryAsync(containerCore, new ResponseMessage(HttpStatusCode.NotModified), default(CancellationToken)));
        }

        [TestMethod]
        public async Task FeedRangeCompositeContinuation_HandleSplits()
        {
            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
            {
                FeedRangeContinuationTests.BuildTokenForRange("A", "C", "token1"),
                FeedRangeContinuationTests.BuildTokenForRange("C", "F", "token2")
            };

            FeedRangeCompositeContinuation feedRangeCompositeContinuation = new FeedRangeCompositeContinuation(Guid.NewGuid().ToString(), Mock.Of<FeedRangeInternal>(), compositeContinuationTokens);

            MultiRangeMockDocumentClient documentClient = new MultiRangeMockDocumentClient();

            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            cosmosClientContext.Setup(c => c.DocumentClient).Returns(documentClient);

            Mock<ContainerCore> containerCore = new Mock<ContainerCore>();
            containerCore
                .Setup(c => c.ClientContext).Returns(cosmosClientContext.Object);

            Assert.AreEqual(2, feedRangeCompositeContinuation.CompositeContinuationTokens.Count);

            ResponseMessage split = new ResponseMessage(HttpStatusCode.Gone);
            split.Headers.SubStatusCode = Documents.SubStatusCodes.PartitionKeyRangeGone;
            Assert.IsTrue(await feedRangeCompositeContinuation.ShouldRetryAsync(containerCore.Object, split, default(CancellationToken)));

            // verify token state
            // Split should have updated initial and created a new token at the end
            Assert.AreEqual(3, feedRangeCompositeContinuation.CompositeContinuationTokens.Count);
            CompositeContinuationToken[] continuationTokens = feedRangeCompositeContinuation.CompositeContinuationTokens.ToArray();
            // First token is split
            Assert.AreEqual(compositeContinuationTokens[0].Token, continuationTokens[0].Token);
            Assert.AreEqual(documentClient.AvailablePartitionKeyRanges[0].MinInclusive, continuationTokens[0].Range.Min);
            Assert.AreEqual(documentClient.AvailablePartitionKeyRanges[0].MaxExclusive, continuationTokens[0].Range.Max);

            // Second token remains the same
            Assert.AreEqual(compositeContinuationTokens[1].Token, continuationTokens[1].Token);
            Assert.AreEqual(compositeContinuationTokens[1].Range.Min, continuationTokens[1].Range.Min);
            Assert.AreEqual(compositeContinuationTokens[1].Range.Max, continuationTokens[1].Range.Max);

            // New third token
            Assert.AreEqual(compositeContinuationTokens[0].Token, continuationTokens[2].Token);
            Assert.AreEqual(documentClient.AvailablePartitionKeyRanges[1].MinInclusive, continuationTokens[2].Range.Min);
            Assert.AreEqual(documentClient.AvailablePartitionKeyRanges[1].MaxExclusive, continuationTokens[2].Range.Max);
        }

        [TestMethod]
        public void FeedRangeSimpleContinuation_IsDone()
        {
            FeedRangeSimpleContinuation token = new FeedRangeSimpleContinuation(null, Mock.Of<FeedRangeInternal>());
            token.UpdateContinuation(Guid.NewGuid().ToString());
            Assert.IsFalse(token.IsDone);
            token.UpdateContinuation(null);
            Assert.IsTrue(token.IsDone);
        }

        [TestMethod]
        public void FeedRangeCompositeContinuation_IsDone()
        {
            const string containerRid = "containerRid";
            FeedRangeCompositeContinuation token = new FeedRangeCompositeContinuation(
                containerRid,
                Mock.Of<FeedRangeInternal>(),
                new List<Documents.Routing.Range<string>>() {
                    new Documents.Routing.Range<string>("A", "B", true, false)
                }, continuation: Guid.NewGuid().ToString());
            Assert.IsFalse(token.IsDone);

            token.UpdateContinuation(null);
            Assert.IsTrue(token.IsDone);
        }

        [TestMethod]
        public void FeedRangeCompositeContinuation_IsDone_MultipleRanges()
        {
            const string containerRid = "containerRid";
            FeedRangeCompositeContinuation token = new FeedRangeCompositeContinuation(
                containerRid,
                Mock.Of<FeedRangeInternal>(),
                new List<Documents.Routing.Range<string>>() {
                    new Documents.Routing.Range<string>("A", "B", true, false),
                    new Documents.Routing.Range<string>("B", "C", true, false),
                    new Documents.Routing.Range<string>("C", "D", true, false)
                }, continuation: null);

            // First range has continuation
            token.UpdateContinuation(Guid.NewGuid().ToString());
            Assert.IsFalse(token.IsDone);

            // Second range is done
            token.UpdateContinuation(null);
            Assert.IsFalse(token.IsDone);

            // Third range is done
            token.UpdateContinuation(null);
            Assert.IsFalse(token.IsDone);

            // First range has continuation
            token.UpdateContinuation(Guid.NewGuid().ToString());
            Assert.IsFalse(token.IsDone);

            // MoveNext should skip the second and third
            // Finish first one
            token.UpdateContinuation(null);
            Assert.IsTrue(token.IsDone);
        }

        private static CompositeContinuationToken BuildTokenForRange(
            string min,
            string max,
            string token)
        {
            return new CompositeContinuationToken()
            {
                Token = token,
                Range = new Documents.Routing.Range<string>(min, max, true, false)
            };
        }

        private class MultiRangeMockDocumentClient : MockDocumentClient
        {
            public List<Documents.PartitionKeyRange> AvailablePartitionKeyRanges = new List<Documents.PartitionKeyRange>() {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B", Id = "0" },
                new Documents.PartitionKeyRange() { MinInclusive = "B", MaxExclusive ="C", Id = "0" },
                new Documents.PartitionKeyRange() { MinInclusive = "C", MaxExclusive ="F", Id = "0" },
            };

            internal override IReadOnlyList<Documents.PartitionKeyRange> ResolveOverlapingPartitionKeyRanges(string collectionRid, Documents.Routing.Range<string> range, bool forceRefresh)
            {
                return new List<Documents.PartitionKeyRange>() { this.AvailablePartitionKeyRanges[0], this.AvailablePartitionKeyRanges[1] };
            }
        }

        private class PKRangeSplitMockDocumentClient : MockDocumentClient
        {
            public List<Documents.PartitionKeyRange> AvailablePartitionKeyRanges = new List<Documents.PartitionKeyRange>() {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B", Id = "1", Parents = new System.Collections.ObjectModel.Collection<string>(){ "0" } },
                new Documents.PartitionKeyRange() { MinInclusive = "B", MaxExclusive ="C", Id = "2", Parents = new System.Collections.ObjectModel.Collection<string>(){ "0" } },
                new Documents.PartitionKeyRange() { MinInclusive = "C", MaxExclusive ="F", Id = "3", Parents = new System.Collections.ObjectModel.Collection<string>() },
            };

            internal override IReadOnlyList<Documents.PartitionKeyRange> ResolveOverlapingPartitionKeyRanges(string collectionRid, Documents.Routing.Range<string> range, bool forceRefresh)
            {
                return this.AvailablePartitionKeyRanges;
            }
        }
    }
}
