//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
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
        public async Task QueryFeedToken_Serialization()
        {
            string continuation = "TBD";
            QueryDefinition queryDefinition = new QueryDefinition("select * from c");
            List<QueryFeedToken> tokens = (await this.Container.GetQueryFeedTokensAsync(queryDefinition)).ToList();
            List<string> serializations = new List<string>();
            foreach (QueryFeedToken token in tokens)
            {
                (token as QueryFeedTokenInternal).QueryFeedToken.UpdateContinuation(continuation);
                serializations.Add(token.ToString());
            }

            List<QueryFeedToken> deserialized = new List<QueryFeedToken>();
            foreach (string serialized in serializations)
            {
                QueryFeedToken token = QueryFeedToken.FromString(serialized);
                deserialized.Add(token);
            }

            Assert.AreEqual(tokens.Count, deserialized.Count);
            for (int i = 0; i < tokens.Count; i++)
            {
                Assert.AreEqual((tokens[i] as QueryFeedTokenInternal).QueryDefinition.QueryText, (deserialized[i] as QueryFeedTokenInternal).QueryDefinition.QueryText);
                FeedTokenEPKRange originalToken = (tokens[i] as QueryFeedTokenInternal).QueryFeedToken as FeedTokenEPKRange;
                FeedTokenEPKRange deserializedToken = (deserialized[i] as QueryFeedTokenInternal).QueryFeedToken as FeedTokenEPKRange;
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

            // Checking queryDefinition null
            QueryFeedTokenInternal queryFeedTokenInternal = new QueryFeedTokenInternal((tokens[0] as QueryFeedTokenInternal).QueryFeedToken, queryDefinition: null);
            string nullSerialized = queryFeedTokenInternal.ToString();
            QueryFeedToken nullDeserialized = QueryFeedToken.FromString(nullSerialized);
            Assert.IsNull((nullDeserialized as QueryFeedTokenInternal).QueryDefinition);
        }

        /// <summary>
        /// Covers the case of someone migrating from PKRangeId in V2
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task QueryFeedToken_BackwardCompatibleSerialization()
        {
            string continuation = "TBD";
            DocumentFeedResponse<Documents.PartitionKeyRange> ranges = await this.Container.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri);
            Documents.PartitionKeyRange oneRange = ranges.First();

            QueryDefinition queryDefinition = new QueryDefinition("select * from c");
            FeedTokenPartitionKeyRange feedTokenPartitionKeyRange = new FeedTokenPartitionKeyRange(oneRange.Id, continuationToken: continuation);
            QueryFeedToken queryFeedToken = new QueryFeedTokenInternal(feedTokenPartitionKeyRange, queryDefinition);

            string serialization = queryFeedToken.ToString();
            QueryFeedTokenInternal deserialized = QueryFeedToken.FromString(serialization) as QueryFeedTokenInternal;
            Assert.AreEqual(queryDefinition.QueryText, deserialized.QueryDefinition.QueryText);
            Assert.AreEqual(feedTokenPartitionKeyRange.PartitionKeyRangeId, (deserialized.QueryFeedToken as FeedTokenPartitionKeyRange).PartitionKeyRangeId);
            Assert.AreEqual(continuation, (deserialized.QueryFeedToken as FeedTokenPartitionKeyRange).GetContinuation());
        }

        [TestMethod]
        public async Task FeedToken_EPKRange_Serialization()
        {
            string continuation = "TBD";
            List<ChangeFeedToken> tokens = (await this.Container.GetChangeFeedTokensAsync()).ToList();
            List<string> serializations = new List<string>();
            foreach(ChangeFeedToken token in tokens)
            {
                (token as ChangeFeedTokenInternal).ChangeFeedToken.UpdateContinuation(continuation);
                serializations.Add(token.ToString());
            }

            List<ChangeFeedToken> deserialized = new List<ChangeFeedToken>();
            foreach(string serialized in serializations)
            {
                ChangeFeedToken token = ChangeFeedToken.FromString(serialized);
                deserialized.Add(token);
            }

            Assert.AreEqual(tokens.Count, deserialized.Count);

            for (int i = 0; i < tokens.Count; i++)
            {
                FeedTokenEPKRange originalToken = (tokens[i] as ChangeFeedTokenInternal).ChangeFeedToken as FeedTokenEPKRange;
                FeedTokenEPKRange deserializedToken = (deserialized[i] as ChangeFeedTokenInternal).ChangeFeedToken as FeedTokenEPKRange;
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

            FeedTokenPartitionKeyRange original = new FeedTokenPartitionKeyRange(oneRange.Id, continuationToken: continuationToken);
            string serialized = new ChangeFeedTokenInternal(original).ToString();
            ChangeFeedToken deserialized = ChangeFeedToken.FromString(serialized);
            FeedTokenPartitionKeyRange deserializedFeedToken = (deserialized as ChangeFeedTokenInternal).ChangeFeedToken as FeedTokenPartitionKeyRange;
            Assert.IsNotNull(deserialized, "Error deserializing to FeedTokenPartitionKeyRange");
            Assert.AreEqual(original.PartitionKeyRangeId, deserializedFeedToken.PartitionKeyRangeId);
            Assert.AreEqual(continuationToken, deserializedFeedToken.GetContinuation());

            // Verify that the backward compatible way works too
            ChangeFeedToken deserializedFromBackwardcompatible = ChangeFeedToken.FromString(oneRange.Id);
            FeedTokenPartitionKeyRange deserializedFromBackwardcompatibleToken = (deserializedFromBackwardcompatible as ChangeFeedTokenInternal).ChangeFeedToken as FeedTokenPartitionKeyRange;
            Assert.IsNotNull(deserializedFromBackwardcompatibleToken, "Error deserializing to FeedTokenPartitionKeyRange");
            Assert.AreEqual(deserializedFromBackwardcompatibleToken.PartitionKeyRangeId, deserializedFeedToken.PartitionKeyRangeId);
        }

        [TestMethod]
        public async Task GetPartitionKeyRangesAsync_WithEPKToken()
        {
            DocumentFeedResponse<Documents.PartitionKeyRange> ranges = await this.Container.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri);
            int pkRangesCount = ranges.Count;
            List<ChangeFeedToken> tokens = (await this.Container.GetChangeFeedTokensAsync()).ToList();
            List<string> resolvedRanges = new List<string>();
            foreach(ChangeFeedToken token in tokens)
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
        }

        [TestMethod]
        public async Task GetPartitionKeyRangesAsync_WithPKToken()
        {
            DocumentFeedResponse<Documents.PartitionKeyRange> ranges = await this.Container.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri);

            ChangeFeedToken feedToken = new ChangeFeedTokenInternal(new FeedTokenPartitionKey(new PartitionKey("TBD")));
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

            ChangeFeedToken feedToken = new ChangeFeedTokenInternal(new FeedTokenPartitionKeyRange(ranges.First().Id, continuationToken: null));
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
            string serialized = new ChangeFeedTokenInternal(feedTokenPartitionKey).ToString();
            ChangeFeedToken deserialized = ChangeFeedToken.FromString(serialized);
            FeedTokenPartitionKey deserializedFeedToken = (deserialized as ChangeFeedTokenInternal).ChangeFeedToken as FeedTokenPartitionKey;
            Assert.IsNotNull(deserialized, "Error deserializing to FeedTokenPartitionKey");
            Assert.AreEqual(feedTokenPartitionKey.PartitionKey.ToJsonString(), deserializedFeedToken.PartitionKey.ToJsonString());
            Assert.AreEqual(continuationToken, deserializedFeedToken.GetContinuation());
        }
    }
}
