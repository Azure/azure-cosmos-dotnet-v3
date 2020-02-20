//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class FeedTokenTests : BaseCosmosClientHelper
    {
        private ContainerCore Container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            ContainerResponse largerContainer = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                throughput: 20000,
                cancellationToken: this.cancellationToken);

            this.Container = (ContainerInlineCore)largerContainer;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task FeedToken_EPKRange_Serialization()
        {
            string continuation = "TBD";
            List<FeedToken> tokens = (await this.Container.GetFeedTokensAsync()).ToList();
            List<string> serializations = new List<string>();
            foreach(FeedToken token in tokens)
            {
                (token as FeedTokenInternal).UpdateContinuation(continuation);
                serializations.Add(token.ToString());
            }

            List<FeedToken> deserialized = new List<FeedToken>();
            foreach(string serialized in serializations)
            {
                FeedToken token = FeedToken.FromString(serialized);
                deserialized.Add(token);
            }

            Assert.AreEqual(tokens.Count, deserialized.Count);

            for (int i = 0; i < tokens.Count; i++)
            {
                FeedTokenEPKRange originalToken = tokens[i] as FeedTokenEPKRange;
                FeedTokenEPKRange deserializedToken = deserialized[i] as FeedTokenEPKRange;
                Assert.AreEqual(originalToken.GetContinuation(), deserializedToken.GetContinuation());
                Assert.AreEqual(originalToken.ContainerRid, deserializedToken.ContainerRid);
                Assert.AreEqual(originalToken.CompositeContinuationTokens.Count, deserializedToken.CompositeContinuationTokens.Count);
                Assert.AreEqual(originalToken.CompleteRange.Min, deserializedToken.CompleteRange.Min);
                Assert.AreEqual(originalToken.CompleteRange.Max, deserializedToken.CompleteRange.Max);
                Assert.AreEqual(originalToken.CompositeContinuationTokens.Peek().Token, deserializedToken.CompositeContinuationTokens.Peek().Token);
                Assert.AreEqual(originalToken.CompositeContinuationTokens.Peek().Range.Min, deserializedToken.CompositeContinuationTokens.Peek().Range.Min);
                Assert.AreEqual(originalToken.CompositeContinuationTokens.Peek().Range.Max, deserializedToken.CompositeContinuationTokens.Peek().Range.Max);
                Assert.AreEqual(originalToken.CompositeContinuationTokens.Peek().Range.IsMinInclusive, deserializedToken.CompositeContinuationTokens.Peek().Range.IsMinInclusive);
                Assert.AreEqual(originalToken.CompositeContinuationTokens.Peek().Range.IsMaxInclusive, deserializedToken.CompositeContinuationTokens.Peek().Range.IsMaxInclusive);
            }
        }

        [TestMethod]
        public async Task FeedToken_EPKRange_SingleToken_Serialization()
        {
            string continuation = "TBD";
            List<FeedToken> tokens = (await this.Container.GetFeedTokensAsync(maxTokens: 1)).ToList();
            List<string> serializations = new List<string>();
            foreach (FeedToken token in tokens)
            {
                (token as FeedTokenInternal).UpdateContinuation(continuation);
                serializations.Add(token.ToString());
            }

            List<FeedToken> deserialized = new List<FeedToken>();
            foreach (string serialized in serializations)
            {
                FeedToken token = FeedToken.FromString(serialized);
                deserialized.Add(token);
            }

            Assert.AreEqual(tokens.Count, deserialized.Count);

            for (int i = 0; i < tokens.Count; i++)
            {
                FeedTokenEPKRange originalToken = tokens[i] as FeedTokenEPKRange;
                FeedTokenEPKRange deserializedToken = deserialized[i] as FeedTokenEPKRange;
                Assert.AreEqual(originalToken.CompleteRange.Min, deserializedToken.CompleteRange.Min);
                Assert.AreEqual(originalToken.CompleteRange.Max, deserializedToken.CompleteRange.Max);
                Assert.AreEqual(originalToken.ContainerRid, deserializedToken.ContainerRid);
                CompositeContinuationToken[] originalTokenArray = originalToken.CompositeContinuationTokens.ToArray();
                CompositeContinuationToken[] deserializedTokenArray = deserializedToken.CompositeContinuationTokens.ToArray();
                Assert.AreEqual(originalTokenArray.Length, deserializedTokenArray.Length);
                for(int j = 0; j < originalTokenArray.Length; j++)
                {
                    Assert.AreEqual(originalTokenArray[j].Token, deserializedTokenArray[j].Token);
                    Assert.AreEqual(originalTokenArray[j].Range.Min, deserializedTokenArray[j].Range.Min);
                    Assert.AreEqual(originalTokenArray[j].Range.Max, deserializedTokenArray[j].Range.Max);
                    Assert.AreEqual(originalTokenArray[j].Range.IsMinInclusive, deserializedTokenArray[j].Range.IsMinInclusive);
                    Assert.AreEqual(originalTokenArray[j].Range.IsMaxInclusive, deserializedTokenArray[j].Range.IsMaxInclusive);
                }
            }
        }

        [TestMethod]
        public void FeedToken_PartitionKey_Serialization()
        {
            this.FeedToken_PartitionKey_Validate(new PartitionKey("TBD"));
            this.FeedToken_PartitionKey_Validate(new PartitionKey(10));
            this.FeedToken_PartitionKey_Validate(new PartitionKey(15.6));
            this.FeedToken_PartitionKey_Validate(new PartitionKey(true));
        }

        [TestMethod]
        public async Task FeedToken_PKRangeId_Serialization()
        {
            string continuationToken = "TBD";
            DocumentFeedResponse<Documents.PartitionKeyRange> ranges = await this.Container.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri);
            Documents.PartitionKeyRange oneRange = ranges.First();

            FeedTokenPartitionKeyRange original = new FeedTokenPartitionKeyRange(oneRange.Id);
            original.UpdateContinuation(continuationToken);
            string serialized = original.ToString();
            FeedToken deserialized = FeedToken.FromString(serialized);
            FeedTokenPartitionKeyRange deserializedFeedToken = deserialized as FeedTokenPartitionKeyRange;
            Assert.IsNotNull(deserialized, "Error deserializing to FeedTokenPartitionKeyRange");
            Assert.AreEqual(original.PartitionKeyRangeId, deserializedFeedToken.PartitionKeyRangeId);
            Assert.AreEqual(continuationToken, deserializedFeedToken.GetContinuation());

            // Verify that the backward compatible way works too
            FeedToken deserializedFromBackwardcompatible = FeedToken.FromString(oneRange.Id);
            FeedTokenPartitionKeyRange deserializedFromBackwardcompatibleToken = deserializedFromBackwardcompatible as FeedTokenPartitionKeyRange;
            Assert.IsNotNull(deserializedFromBackwardcompatibleToken, "Error deserializing to FeedTokenPartitionKeyRange");
            Assert.AreEqual(deserializedFromBackwardcompatibleToken.PartitionKeyRangeId, deserializedFeedToken.PartitionKeyRangeId);
        }

        [TestMethod]
        public async Task GetPartitionKeyRangesAsync_WithEPKToken()
        {
            DocumentFeedResponse<Documents.PartitionKeyRange> ranges = await this.Container.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri);
            int pkRangesCount = ranges.Count;
            List<FeedToken> tokens = (await this.Container.GetFeedTokensAsync()).ToList();
            List<string> resolvedRanges = new List<string>();
            foreach(FeedToken token in tokens)
            {
                resolvedRanges.AddRange(await this.Container.GetPartitionKeyRangesAsync(token));
            }

            Assert.AreEqual(pkRangesCount, resolvedRanges.Count);
            foreach (Documents.PartitionKeyRange range in ranges)
            {
                Assert.IsTrue(resolvedRanges.Contains(range.Id));
            }

            foreach (string id in resolvedRanges)
            {
                Assert.IsTrue(ranges.Any(range => range.Id == id));
            }

            // Now for a single token
            tokens = (await this.Container.GetFeedTokensAsync(maxTokens: 1)).ToList();
            resolvedRanges = (await this.Container.GetPartitionKeyRangesAsync(tokens[0])).ToList();

            Assert.AreEqual(pkRangesCount, resolvedRanges.Count);
            foreach (Documents.PartitionKeyRange range in ranges)
            {
                Assert.IsTrue(resolvedRanges.Contains(range.Id));
            }

            foreach (string id in resolvedRanges)
            {
                Assert.IsTrue(ranges.Any(range => range.Id == id));
            }
        }

        [TestMethod]
        public async Task GetPartitionKeyRangesAsync_WithPKToken()
        {
            DocumentFeedResponse<Documents.PartitionKeyRange> ranges = await this.Container.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri);

            FeedToken feedToken = new FeedTokenPartitionKey(new PartitionKey("TBD"));
            List<string> resolvedRanges = (await this.Container.GetPartitionKeyRangesAsync(feedToken)).ToList();

            Assert.AreEqual(1, resolvedRanges.Count, "PK value should resolve to a single range");

            foreach (string id in resolvedRanges)
            {
                Assert.IsTrue(ranges.Any(range => range.Id == id));
            }
        }

        [TestMethod]
        public async Task GetPartitionKeyRangesAsync_WithPKRangeIdToken()
        {
            DocumentFeedResponse<Documents.PartitionKeyRange> ranges = await this.Container.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri);

            FeedToken feedToken = new FeedTokenPartitionKeyRange(ranges.First().Id);
            List<string> resolvedRanges = (await this.Container.GetPartitionKeyRangesAsync(feedToken)).ToList();

            Assert.AreEqual(1, resolvedRanges.Count);

            foreach (string id in resolvedRanges)
            {
                Assert.IsTrue(ranges.Any(range => range.Id == id));
            }
        }

        private void FeedToken_PartitionKey_Validate(PartitionKey partitionKey)
        {
            string continuationToken = "TBD";
            FeedTokenPartitionKey feedTokenPartitionKey = new FeedTokenPartitionKey(partitionKey);
            feedTokenPartitionKey.UpdateContinuation(continuationToken);
            string serialized = feedTokenPartitionKey.ToString();
            FeedToken deserialized = FeedToken.FromString(serialized);
            FeedTokenPartitionKey deserializedFeedToken = deserialized as FeedTokenPartitionKey;
            Assert.IsNotNull(deserialized, "Error deserializing to FeedTokenPartitionKey");
            Assert.AreEqual(feedTokenPartitionKey.PartitionKey.ToJsonString(), deserializedFeedToken.PartitionKey.ToJsonString());
            Assert.AreEqual(continuationToken, deserializedFeedToken.GetContinuation());
        }
    }
}
