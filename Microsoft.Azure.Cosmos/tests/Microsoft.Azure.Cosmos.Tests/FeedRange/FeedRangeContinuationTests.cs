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
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

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
            token.ReplaceContinuation("something");
            Assert.AreEqual(keyRanges[1].Min, token.CompositeContinuationTokens.Peek().Range.Min);
            token.ReplaceContinuation("something");
            Assert.AreEqual(keyRanges[0].Min, token.CompositeContinuationTokens.Peek().Range.Min);
            token.ReplaceContinuation("something");
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
            FeedRangeInternal feedRangeInternal = new FeedRangeEpk(new Documents.Routing.Range<string>("A", "E", true, false));
            FeedRangeCompositeContinuation token = new FeedRangeCompositeContinuation(containerRid, feedRangeInternal, keyRanges);
            Assert.IsTrue(FeedRangeContinuation.TryParse(token.ToString(), out _));
            Assert.IsFalse(FeedRangeContinuation.TryParse("whatever", out _));
        }

        [TestMethod]
        public void FeedRangeCompositeContinuation_ShouldRetry()
        {
            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
            {
                FeedRangeContinuationTests.BuildTokenForRange("A", "C", "token1"),
                FeedRangeContinuationTests.BuildTokenForRange("C", "F", "token2")
            };

            FeedRangeCompositeContinuation feedRangeCompositeContinuation = new FeedRangeCompositeContinuation(Guid.NewGuid().ToString(), Mock.Of<FeedRangeInternal>(), compositeContinuationTokens);

            ResponseMessage okResponse = new ResponseMessage(HttpStatusCode.OK);
            okResponse.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";
            okResponse.Headers[Documents.HttpConstants.HttpHeaders.ETag] = "1";
            Assert.IsFalse(feedRangeCompositeContinuation.HandleChangeFeedNotModified(okResponse).ShouldRetry);

            ResponseMessage notModified = new ResponseMessage(HttpStatusCode.NotModified);
            notModified.Headers[Documents.HttpConstants.HttpHeaders.ETag] = "1";

            // A 304 on a multi Range token should cycle on all available ranges before stopping retrying
            Assert.IsTrue(feedRangeCompositeContinuation.HandleChangeFeedNotModified(notModified).ShouldRetry);
            Assert.IsTrue(feedRangeCompositeContinuation.HandleChangeFeedNotModified(notModified).ShouldRetry);
            Assert.IsFalse(feedRangeCompositeContinuation.HandleChangeFeedNotModified(notModified).ShouldRetry);
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

            Mock<ContainerInternal> containerCore = new Mock<ContainerInternal>();
            containerCore
                .Setup(c => c.ClientContext).Returns(cosmosClientContext.Object);

            Assert.AreEqual(2, feedRangeCompositeContinuation.CompositeContinuationTokens.Count);

            ResponseMessage split = new ResponseMessage(HttpStatusCode.Gone);
            split.Headers.SubStatusCode = Documents.SubStatusCodes.PartitionKeyRangeGone;
            Assert.IsTrue((await feedRangeCompositeContinuation.HandleSplitAsync(containerCore.Object, split, default)).ShouldRetry);

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
        public async Task FeedRangeCompositeContinuation_HandleSplits_ReadFeed()
        {
            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
            {
                FeedRangeContinuationTests.BuildTokenForRange("A", "C", JsonConvert.SerializeObject(new CompositeContinuationToken() { Token = "token1", Range = new Documents.Routing.Range<string>("A", "C", true, false) })),
                FeedRangeContinuationTests.BuildTokenForRange("C", "F", JsonConvert.SerializeObject(new CompositeContinuationToken() { Token = "token2", Range = new Documents.Routing.Range<string>("C", "F", true, false) })),
            };

            FeedRangeCompositeContinuation feedRangeCompositeContinuation = new FeedRangeCompositeContinuation(Guid.NewGuid().ToString(), Mock.Of<FeedRangeInternal>(), JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(JsonConvert.SerializeObject(compositeContinuationTokens)));

            MultiRangeMockDocumentClient documentClient = new MultiRangeMockDocumentClient();

            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            cosmosClientContext.Setup(c => c.DocumentClient).Returns(documentClient);

            Mock<ContainerInternal> containerCore = new Mock<ContainerInternal>();
            containerCore
                .Setup(c => c.ClientContext).Returns(cosmosClientContext.Object);

            Assert.AreEqual(2, feedRangeCompositeContinuation.CompositeContinuationTokens.Count);

            ResponseMessage split = new ResponseMessage(HttpStatusCode.Gone);
            split.Headers.SubStatusCode = Documents.SubStatusCodes.PartitionKeyRangeGone;
            Assert.IsTrue((await feedRangeCompositeContinuation.HandleSplitAsync(containerCore.Object, split, default)).ShouldRetry);

            // verify token state
            // Split should have updated initial and created a new token at the end
            Assert.AreEqual(3, feedRangeCompositeContinuation.CompositeContinuationTokens.Count);
            CompositeContinuationToken[] continuationTokens = feedRangeCompositeContinuation.CompositeContinuationTokens.ToArray();
            // First token is split
            Assert.AreEqual(JsonConvert.DeserializeObject<CompositeContinuationToken>(compositeContinuationTokens[0].Token).Range.Min, JsonConvert.DeserializeObject<CompositeContinuationToken>(continuationTokens[0].Token).Range.Min);
            Assert.AreEqual(JsonConvert.DeserializeObject<CompositeContinuationToken>(compositeContinuationTokens[0].Token).Token, JsonConvert.DeserializeObject<CompositeContinuationToken>(continuationTokens[0].Token).Token);
            Assert.AreEqual(documentClient.AvailablePartitionKeyRanges[0].MinInclusive, continuationTokens[0].Range.Min);
            Assert.AreEqual(documentClient.AvailablePartitionKeyRanges[0].MaxExclusive, continuationTokens[0].Range.Max);

            // Second token remains the same
            Assert.AreEqual(compositeContinuationTokens[1].Token, continuationTokens[1].Token);
            Assert.AreEqual(compositeContinuationTokens[1].Range.Min, continuationTokens[1].Range.Min);
            Assert.AreEqual(compositeContinuationTokens[1].Range.Max, continuationTokens[1].Range.Max);

            // New third token
            Assert.AreEqual(JsonConvert.DeserializeObject<CompositeContinuationToken>(compositeContinuationTokens[0].Token).Range.Max, JsonConvert.DeserializeObject<CompositeContinuationToken>(continuationTokens[2].Token).Range.Max);
            Assert.AreEqual(JsonConvert.DeserializeObject<CompositeContinuationToken>(compositeContinuationTokens[0].Token).Token, JsonConvert.DeserializeObject<CompositeContinuationToken>(continuationTokens[2].Token).Token);
            Assert.AreEqual(documentClient.AvailablePartitionKeyRanges[1].MinInclusive, continuationTokens[2].Range.Min);
            Assert.AreEqual(documentClient.AvailablePartitionKeyRanges[1].MaxExclusive, continuationTokens[2].Range.Max);
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

            token.ReplaceContinuation(null);
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
            token.ReplaceContinuation(Guid.NewGuid().ToString());
            Assert.IsFalse(token.IsDone);

            // Second range is done
            token.ReplaceContinuation(null);
            Assert.IsFalse(token.IsDone);

            // Third range is done
            token.ReplaceContinuation(null);
            Assert.IsFalse(token.IsDone);

            // First range has continuation
            token.ReplaceContinuation(Guid.NewGuid().ToString());
            Assert.IsFalse(token.IsDone);

            // MoveNext should skip the second and third
            // Finish first one
            token.ReplaceContinuation(null);
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