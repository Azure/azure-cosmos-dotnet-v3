//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class FeedTokenTests
    {
        [TestMethod]
        public void FeedToken_EPK_MoveToNextTokenCircles()
        {
            const string containerRid = "containerRid";
            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "D", MaxExclusive ="E" },
            };
            FeedTokenEPKRange token = new FeedTokenEPKRange(containerRid, keyRanges);
            Assert.AreEqual(keyRanges[0].MinInclusive, token.CompositeContinuationTokens.Peek().Range.Min);
            token.UpdateContinuation("something");
            Assert.AreEqual(keyRanges[1].MinInclusive, token.CompositeContinuationTokens.Peek().Range.Min);
            token.UpdateContinuation("something");
            Assert.AreEqual(keyRanges[0].MinInclusive, token.CompositeContinuationTokens.Peek().Range.Min);
            token.UpdateContinuation("something");
            Assert.AreEqual(keyRanges[1].MinInclusive, token.CompositeContinuationTokens.Peek().Range.Min);
        }

        [TestMethod]
        public void FeedToken_EPK_Scale()
        {
            const string containerRid = "containerRid";
            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "D", MaxExclusive ="E" },
            };
            FeedTokenEPKRange token = new FeedTokenEPKRange(containerRid, keyRanges);
            IReadOnlyList<FeedToken> splitTokens = token.Scale();
            Assert.AreEqual(keyRanges.Count, splitTokens.Count);

            List<FeedTokenEPKRange> feedTokenEPKRanges = splitTokens.Select(t => t as FeedTokenEPKRange).ToList();
            Assert.AreEqual(keyRanges[0].MinInclusive, feedTokenEPKRanges[0].CompositeContinuationTokens.Peek().Range.Min);
            Assert.AreEqual(keyRanges[0].MaxExclusive, feedTokenEPKRanges[0].CompositeContinuationTokens.Peek().Range.Max);
            Assert.AreEqual(keyRanges[1].MinInclusive, feedTokenEPKRanges[1].CompositeContinuationTokens.Peek().Range.Min);
            Assert.AreEqual(keyRanges[1].MaxExclusive, feedTokenEPKRanges[1].CompositeContinuationTokens.Peek().Range.Max);
            Assert.AreEqual(keyRanges[0].MinInclusive, feedTokenEPKRanges[0].CompleteRange.Min);
            Assert.AreEqual(keyRanges[0].MaxExclusive, feedTokenEPKRanges[0].CompleteRange.Max);
            Assert.AreEqual(keyRanges[1].MinInclusive, feedTokenEPKRanges[1].CompleteRange.Min);
            Assert.AreEqual(keyRanges[1].MaxExclusive, feedTokenEPKRanges[1].CompleteRange.Max);

            FeedTokenEPKRange singleToken = new FeedTokenEPKRange(containerRid, new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive = "B" });
            Assert.AreEqual(0, singleToken.Scale().Count);
        }

        [TestMethod]
        public void FeedToken_EPK_TryParse()
        {
            const string containerRid = "containerRid";
            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "D", MaxExclusive ="E" },
            };
            FeedTokenEPKRange token = new FeedTokenEPKRange(containerRid, keyRanges);
            Assert.IsTrue(FeedTokenEPKRange.TryParseInstance(token.ToString(), out FeedToken parsed));
            Assert.IsFalse(FeedTokenEPKRange.TryParseInstance("whatever", out FeedToken _));
        }

        [TestMethod]
        public void FeedToken_EPK_EnrichRequest()
        {
            const string containerRid = "containerRid";
            FeedTokenEPKRange token = new FeedTokenEPKRange(containerRid, new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive = "B" });
            RequestMessage requestMessage = new RequestMessage();
            token.EnrichRequest(requestMessage);
            Assert.AreEqual(token.CompleteRange.Min, requestMessage.Properties[HandlerConstants.StartEpkString]);
            Assert.AreEqual(token.CompleteRange.Max, requestMessage.Properties[HandlerConstants.EndEpkString]);

            Assert.ThrowsException<ArgumentNullException>(() => token.EnrichRequest(null));
        }

        [TestMethod]
        public void FeedToken_EPK_NotEnrichRequest_IfEPKAlreadyExists()
        {
            const string containerRid = "containerRid";
            string epkString = Guid.NewGuid().ToString();
            FeedTokenEPKRange token = new FeedTokenEPKRange(containerRid, new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive = "B" });
            RequestMessage requestMessage = new RequestMessage();
            requestMessage.Properties[HandlerConstants.StartEpkString] = epkString;
            requestMessage.Properties[HandlerConstants.EndEpkString] = epkString;
            token.EnrichRequest(requestMessage);
            Assert.AreEqual(epkString, requestMessage.Properties[HandlerConstants.StartEpkString]);
            Assert.AreEqual(epkString, requestMessage.Properties[HandlerConstants.EndEpkString]);
        }

        [TestMethod]
        public void FeedToken_PartitionKey_TryParse()
        {
            FeedTokenPartitionKey token = new FeedTokenPartitionKey(new PartitionKey("test"));
            Assert.IsTrue(FeedTokenPartitionKey.TryParseInstance(token.ToString(), out FeedToken parsed));
            Assert.IsFalse(FeedTokenPartitionKey.TryParseInstance("whatever", out FeedToken _));
        }

        [TestMethod]
        public void FeedToken_PartitionKeyRange_TryParse()
        {
            FeedTokenPartitionKeyRange token = new FeedTokenPartitionKeyRange("0");
            Assert.IsTrue(FeedTokenPartitionKeyRange.TryParseInstance(token.ToString(), out FeedToken parsed));
            Assert.IsTrue(FeedTokenPartitionKeyRange.TryParseInstance("1", out FeedToken _));
            Assert.IsFalse(FeedTokenPartitionKey.TryParseInstance("whatever", out FeedToken _));
        }

        [TestMethod]
        public void FeedToken_PartitionKey_EnrichRequest()
        {
            PartitionKey pk = new PartitionKey("test");
            FeedTokenPartitionKey token = new FeedTokenPartitionKey(pk);
            RequestMessage requestMessage = new RequestMessage();
            token.EnrichRequest(requestMessage);
            Assert.AreEqual(pk.ToJsonString(), requestMessage.Headers.PartitionKey);

            Assert.ThrowsException<ArgumentNullException>(() => token.EnrichRequest(null));
        }

        [TestMethod]
        public void FeedToken_PartitionKeyRange_EnrichRequest()
        {
            string pkrangeId = "0";
            FeedTokenPartitionKeyRange token = new FeedTokenPartitionKeyRange(pkrangeId);
            RequestMessage requestMessage = new RequestMessage();
            token.EnrichRequest(requestMessage);
            Assert.AreEqual(pkrangeId, requestMessage.PartitionKeyRangeId.PartitionKeyRangeId);

            Assert.ThrowsException<ArgumentNullException>(() => token.EnrichRequest(null));
        }

        [TestMethod]
        public void FeedToken_EPK_CompleteRange()
        {
            const string containerRid = "containerRid";
            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "D", MaxExclusive ="E" },
            };
            FeedTokenEPKRange token = new FeedTokenEPKRange(containerRid, keyRanges);
            Assert.AreEqual(keyRanges.Count, token.CompositeContinuationTokens.Count);
            Assert.AreEqual(keyRanges[0].MinInclusive, token.CompleteRange.Min);
            Assert.AreEqual(keyRanges[1].MaxExclusive, token.CompleteRange.Max);
        }

        [TestMethod]
        public void FeedToken_EPK_SingleRange()
        {
            const string containerRid = "containerRid";
            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive = "B" };
            FeedTokenEPKRange token = new FeedTokenEPKRange(containerRid, partitionKeyRange);
            Assert.AreEqual(1, token.CompositeContinuationTokens.Count);
            Assert.AreEqual(partitionKeyRange.MinInclusive, token.CompleteRange.Min);
            Assert.AreEqual(partitionKeyRange.MaxExclusive, token.CompleteRange.Max);
        }

        [TestMethod]
        public async Task FeedToken_EPK_ShouldRetry()
        {
            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
            {
                FeedTokenTests.BuildTokenForRange("A", "C", "token1"),
                FeedTokenTests.BuildTokenForRange("C", "F", "token2")
            };

            FeedTokenEPKRange feedTokenEPKRange = new FeedTokenEPKRange(Guid.NewGuid().ToString(), new Documents.Routing.Range<string>(compositeContinuationTokens[0].Range.Min, compositeContinuationTokens[1].Range.Min, true, false), compositeContinuationTokens);

            ContainerCore containerCore = Mock.Of<ContainerCore>();
            ResponseMessage okResponse = new ResponseMessage(HttpStatusCode.OK);
            okResponse.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";
            Assert.IsFalse(await feedTokenEPKRange.ShouldRetryAsync(containerCore, okResponse));

            // A 304 on a multi Range token should cycle on all available ranges before stopping retrying
            Assert.IsTrue(await feedTokenEPKRange.ShouldRetryAsync(containerCore, new ResponseMessage(HttpStatusCode.NotModified)));
            feedTokenEPKRange.UpdateContinuation(Guid.NewGuid().ToString());
            Assert.IsTrue(await feedTokenEPKRange.ShouldRetryAsync(containerCore, new ResponseMessage(HttpStatusCode.NotModified)));
            feedTokenEPKRange.UpdateContinuation(Guid.NewGuid().ToString());
            Assert.IsFalse(await feedTokenEPKRange.ShouldRetryAsync(containerCore, new ResponseMessage(HttpStatusCode.NotModified)));
        }

        [TestMethod]
        public async Task FeedToken_EPK_HandleSplits()
        {
            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
            {
                FeedTokenTests.BuildTokenForRange("A", "C", "token1"),
                FeedTokenTests.BuildTokenForRange("C", "F", "token2")
            };

            FeedTokenEPKRange feedTokenEPKRange = new FeedTokenEPKRange(Guid.NewGuid().ToString(), new Documents.Routing.Range<string>(compositeContinuationTokens[0].Range.Min, compositeContinuationTokens[1].Range.Min, true, false), compositeContinuationTokens);

            MultiRangeMockDocumentClient documentClient = new MultiRangeMockDocumentClient();

            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            cosmosClientContext.Setup(c => c.DocumentClient).Returns(documentClient);

            Mock<ContainerCore> containerCore = new Mock<ContainerCore>();
            containerCore
                .Setup(c => c.ClientContext).Returns(cosmosClientContext.Object);

            Assert.AreEqual(2, feedTokenEPKRange.CompositeContinuationTokens.Count);

            ResponseMessage split = new ResponseMessage(HttpStatusCode.Gone);
            split.Headers.SubStatusCode = Documents.SubStatusCodes.PartitionKeyRangeGone;
            Assert.IsTrue(await feedTokenEPKRange.ShouldRetryAsync(containerCore.Object, split));

            // verify token state
            // Split should have updated initial and created a new token at the end
            Assert.AreEqual(3, feedTokenEPKRange.CompositeContinuationTokens.Count);
            CompositeContinuationToken[] continuationTokens = feedTokenEPKRange.CompositeContinuationTokens.ToArray();
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
        public async Task FeedToken_PartitionKeyRange_HandleSplits()
        {
            string containerRid = Guid.NewGuid().ToString();
            string continuation = Guid.NewGuid().ToString();

            FeedTokenPartitionKeyRange feedTokenPartitionKeyRange = new FeedTokenPartitionKeyRange("0");
            feedTokenPartitionKeyRange.UpdateContinuation(continuation);
            PKRangeSplitMockDocumentClient documentClient = new PKRangeSplitMockDocumentClient();

            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            cosmosClientContext.Setup(c => c.DocumentClient).Returns(documentClient);

            Mock<ContainerCore> containerCore = new Mock<ContainerCore>();
            containerCore
                .Setup(c => c.ClientContext).Returns(cosmosClientContext.Object);

            containerCore
                .Setup(c => c.GetRIDAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(containerRid));

            ResponseMessage split = new ResponseMessage(HttpStatusCode.Gone);
            split.Headers.SubStatusCode = Documents.SubStatusCodes.PartitionKeyRangeGone;
            Assert.IsTrue(await feedTokenPartitionKeyRange.ShouldRetryAsync(containerCore.Object, split));

            // FeedToken should have converted to EPKRange token

            string serialization = feedTokenPartitionKeyRange.ToString();
            FeedTokenEPKRange feedTokenEPKRange = FeedToken.FromString(serialization) as FeedTokenEPKRange;
            Assert.IsNotNull(feedTokenEPKRange, "FeedTokenPartitionKeyRange did not convert to FeedTokenEPKRange after split");
            Assert.AreEqual(containerRid, feedTokenEPKRange.ContainerRid);

            // Split should only capture the sons of the original PKRangeId
            Assert.AreEqual(2, feedTokenEPKRange.CompositeContinuationTokens.Count);
            CompositeContinuationToken[] continuationTokens = feedTokenEPKRange.CompositeContinuationTokens.ToArray();
            // First token is split
            Assert.AreEqual(continuation, continuationTokens[0].Token);
            Assert.AreEqual(documentClient.AvailablePartitionKeyRanges[0].MinInclusive, continuationTokens[0].Range.Min);
            Assert.AreEqual(documentClient.AvailablePartitionKeyRanges[0].MaxExclusive, continuationTokens[0].Range.Max);

            // Second token remains the same
            Assert.AreEqual(continuation, continuationTokens[1].Token);
            Assert.AreEqual(documentClient.AvailablePartitionKeyRanges[1].MinInclusive, continuationTokens[1].Range.Min);
            Assert.AreEqual(documentClient.AvailablePartitionKeyRanges[1].MaxExclusive, continuationTokens[1].Range.Max);
        }

        [TestMethod]
        public void FeedToken_PartitionKey_IsDone()
        {
            PartitionKey pk = new PartitionKey("test");
            FeedTokenPartitionKey token = new FeedTokenPartitionKey(pk);
            token.UpdateContinuation(Guid.NewGuid().ToString());
            Assert.IsFalse(token.IsDone);
            token.UpdateContinuation(null);
            Assert.IsTrue(token.IsDone);
        }

        [TestMethod]
        public void FeedToken_PartitionKeyRange_IsDone()
        {
            string pkrangeId = "0";
            FeedTokenPartitionKeyRange token = new FeedTokenPartitionKeyRange(pkrangeId);
            token.UpdateContinuation(Guid.NewGuid().ToString());
            Assert.IsFalse(token.IsDone);
            token.UpdateContinuation(null);
            Assert.IsTrue(token.IsDone);
        }

        [TestMethod]
        public void FeedToken_EPK_IsDone()
        {
            const string containerRid = "containerRid";
            FeedTokenEPKRange token = new FeedTokenEPKRange(containerRid,
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive = "B" });

            token.UpdateContinuation(Guid.NewGuid().ToString());
            Assert.IsFalse(token.IsDone);

            token.UpdateContinuation(null);
            Assert.IsTrue(token.IsDone);
        }

        [TestMethod]
        public void FeedToken_EPK_IsDone_MultipleRanges()
        {
            const string containerRid = "containerRid";
            FeedTokenEPKRange token = new FeedTokenEPKRange(containerRid,
                new List<Documents.PartitionKeyRange>() {
                    new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive = "B" },
                    new Documents.PartitionKeyRange() { MinInclusive = "B", MaxExclusive = "C" },
                    new Documents.PartitionKeyRange() { MinInclusive = "C", MaxExclusive = "D" }
                });

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

        private sealed class MultiRangeMockDocumentClient : MockDocumentClient
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

        private sealed class PKRangeSplitMockDocumentClient : MockDocumentClient
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
