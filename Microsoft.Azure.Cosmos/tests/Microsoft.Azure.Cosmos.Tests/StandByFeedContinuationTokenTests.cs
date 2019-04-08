//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class StandByFeedContinuationTokenTests
    {
        private const string ContainerRid = "containerRid";

        [TestMethod]
        public async Task EnsureInitialized_CreatesToken_WithNoInitialContinuation()
        {
            IReadOnlyList<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B", Id = "0" },
                new Documents.PartitionKeyRange() { MinInclusive = "C", MaxExclusive ="D", Id = "1" },
            };

            StandByFeedContinuationToken compositeToken = new StandByFeedContinuationToken(StandByFeedContinuationTokenTests.ContainerRid, null, StandByFeedContinuationTokenTests.CreateCacheFromRange(keyRanges));
            (CompositeContinuationToken token, string rangeId) = await compositeToken.GetCurrentToken();
            Assert.AreEqual(keyRanges[0].MinInclusive, token.Range.Min);
            Assert.AreEqual(keyRanges[0].MaxExclusive, token.Range.Max);
            Assert.AreEqual(keyRanges[0].Id, rangeId);
            await compositeToken.MoveToNextTokenAsync();
            (CompositeContinuationToken token2, string rangeId2) = await compositeToken.GetCurrentToken();
            Assert.AreEqual(keyRanges[1].MinInclusive, token2.Range.Min);
            Assert.AreEqual(keyRanges[1].MaxExclusive, token2.Range.Max);
            Assert.AreEqual(keyRanges[1].Id, rangeId2);
        }

        [TestMethod]
        public async Task EnsureInitialized_CreatesToken_WithInitialContinuation()
        {
            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
                {
                    StandByFeedContinuationTokenTests.BuildTokenForRange("A", "B", "token1"),
                    StandByFeedContinuationTokenTests.BuildTokenForRange("C", "D", "token2")
                };

            string initialToken = JsonConvert.SerializeObject(compositeContinuationTokens);

            IReadOnlyList<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B", Id = "0" },
                new Documents.PartitionKeyRange() { MinInclusive = "C", MaxExclusive ="D", Id = "1" },
            };

            StandByFeedContinuationToken compositeToken = new StandByFeedContinuationToken(StandByFeedContinuationTokenTests.ContainerRid, initialToken, CreateCacheFromRange(keyRanges));
            (CompositeContinuationToken token, string rangeId) = await compositeToken.GetCurrentToken();
            Assert.AreEqual(keyRanges[0].MinInclusive, token.Range.Min);
            Assert.AreEqual(keyRanges[0].MaxExclusive, token.Range.Max);
            Assert.AreEqual(keyRanges[0].Id, rangeId);
            Assert.AreEqual(compositeContinuationTokens[0].Token, token.Token);

            await compositeToken.MoveToNextTokenAsync();
            (CompositeContinuationToken token2, string rangeId2) = await compositeToken.GetCurrentToken();
            Assert.AreEqual(keyRanges[1].MinInclusive, token2.Range.Min);
            Assert.AreEqual(keyRanges[1].MaxExclusive, token2.Range.Max);
            Assert.AreEqual(keyRanges[1].Id, rangeId2);
            Assert.AreEqual(compositeContinuationTokens[1].Token, token2.Token);
        }

        [TestMethod]
        public async Task SerializationIsExpected()
        {
            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
            {
                StandByFeedContinuationTokenTests.BuildTokenForRange("A", "B", "C"),
                StandByFeedContinuationTokenTests.BuildTokenForRange("D", "E", "F")
            };

            string expected = JsonConvert.SerializeObject(compositeContinuationTokens);

            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "D", MaxExclusive ="E" },
            };
            StandByFeedContinuationToken compositeToken = new StandByFeedContinuationToken(StandByFeedContinuationTokenTests.ContainerRid, null, CreateCacheFromRange(keyRanges));
            (CompositeContinuationToken token, string rangeId) = await compositeToken.GetCurrentToken();
            token.Token = "C";
            await compositeToken.MoveToNextTokenAsync();
            (CompositeContinuationToken token2, string rangeId2) = await compositeToken.GetCurrentToken();
            token2.Token = "F";
            await compositeToken.MoveToNextTokenAsync();

            Assert.AreEqual(expected, compositeToken.ToString());
        }

        [TestMethod]
        public async Task MoveToNextTokenCircles()
        {
            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "D", MaxExclusive ="E" },
            };
            StandByFeedContinuationToken compositeToken = new StandByFeedContinuationToken(StandByFeedContinuationTokenTests.ContainerRid, null, CreateCacheFromRange(keyRanges));
            (CompositeContinuationToken token, string rangeId) = await compositeToken.GetCurrentToken();
            Assert.AreEqual(keyRanges[0].MinInclusive, token.Range.Min);
            await compositeToken.MoveToNextTokenAsync();
            (CompositeContinuationToken token2, string rangeId2) = await compositeToken.GetCurrentToken();
            Assert.AreEqual(keyRanges[1].MinInclusive, token2.Range.Min);
            await compositeToken.MoveToNextTokenAsync();
            (CompositeContinuationToken token3, string rangeId3) = await compositeToken.GetCurrentToken();
            Assert.AreEqual(keyRanges[0].MinInclusive, token3.Range.Min);
            await compositeToken.MoveToNextTokenAsync();
            (CompositeContinuationToken token4, string rangeId4) = await compositeToken.GetCurrentToken();
            Assert.AreEqual(keyRanges[1].MinInclusive, token4.Range.Min);
        }

        [TestMethod]
        public async Task HandleSplitGeneratesChildren()
        {
            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
            {
                StandByFeedContinuationTokenTests.BuildTokenForRange("A", "C", ""),
                StandByFeedContinuationTokenTests.BuildTokenForRange("C", "F", "")
            };

            string expected = JsonConvert.SerializeObject(compositeContinuationTokens);

            List<Documents.PartitionKeyRange> keyRangesAfterSplit = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "B", MaxExclusive ="C" },
                new Documents.PartitionKeyRange() { MinInclusive = "C", MaxExclusive ="F" },
            };

            StandByFeedContinuationToken compositeToken = new StandByFeedContinuationToken(StandByFeedContinuationTokenTests.ContainerRid, expected, CreateCacheFromRange(keyRangesAfterSplit));

            (CompositeContinuationToken token, string rangeId) = await compositeToken.GetCurrentToken();
            //Assert.AreEqual(keyRanges[0].MinInclusive, token.Range.Min);
            //Assert.AreEqual(keyRanges[0].MaxExclusive, token.Range.Max);

            //compositeToken.HandleSplit(keyRangesAfterSplit);
            // Current should be updated
            Assert.AreEqual(keyRangesAfterSplit[0].MinInclusive, token.Range.Min);
            Assert.AreEqual(keyRangesAfterSplit[0].MaxExclusive, token.Range.Max);
            await compositeToken.MoveToNextTokenAsync();
            (CompositeContinuationToken token2, string rangeId2) = await compositeToken.GetCurrentToken();
            // Next should be the original second
            Assert.AreEqual(compositeContinuationTokens[1].Range.Min, token2.Range.Min);
            Assert.AreEqual(compositeContinuationTokens[1].Range.Max, token2.Range.Max);
            await compositeToken.MoveToNextTokenAsync();
            (CompositeContinuationToken token3, string rangeId3) = await compositeToken.GetCurrentToken();
            // Finally the new children
            Assert.AreEqual(keyRangesAfterSplit[1].MinInclusive, token3.Range.Min);
            Assert.AreEqual(keyRangesAfterSplit[1].MaxExclusive, token3.Range.Max);
            // And go back to the beginning
            await compositeToken.MoveToNextTokenAsync();
            (CompositeContinuationToken token5, string rangeId5) = await compositeToken.GetCurrentToken();
            Assert.AreEqual(keyRangesAfterSplit[0].MinInclusive, token5.Range.Min);
            Assert.AreEqual(keyRangesAfterSplit[0].MaxExclusive, token5.Range.Max);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public async Task ConstructorWithInvalidTokenFormat()
        {
            IReadOnlyList<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B", Id = "0" },
                new Documents.PartitionKeyRange() { MinInclusive = "C", MaxExclusive ="D", Id = "1" },
            };

            StandByFeedContinuationToken token = new StandByFeedContinuationToken("containerRid", "notatoken", CreateCacheFromRange(keyRanges));
            await token.GetCurrentToken();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorWithNullContainer()
        {
            new StandByFeedContinuationToken(null, null, null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorWithNullDelegate()
        {
            new StandByFeedContinuationToken("something", null, null);
        }

        private static Func<string, Documents.Routing.Range<string>, bool, Task<IReadOnlyList<Documents.PartitionKeyRange>>> CreateCacheFromRange(
            IReadOnlyList<Documents.PartitionKeyRange> keyRanges,
            IReadOnlyList<Documents.PartitionKeyRange> afterSplit = null)
        {
            return (string containerRid, Documents.Routing.Range<string> ranges, bool forceRefresh) =>
            {
                if (ranges.Max.Equals(Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey))
                {
                    return Task.FromResult(keyRanges);
                }

                IReadOnlyList<Documents.PartitionKeyRange> nosplit = new List<Documents.PartitionKeyRange>(keyRanges.Where(range=> range.MinInclusive.CompareTo(ranges.Min) >= 0 && range.MaxExclusive.CompareTo(ranges.Max) <= 0));

                if (nosplit.Any())
                {
                    return Task.FromResult(nosplit);
                }

                IReadOnlyList<Documents.PartitionKeyRange> afterSplitResults = new List<Documents.PartitionKeyRange>(afterSplit.Where(range => range.MinInclusive.CompareTo(ranges.Min) >= 0 && range.MaxExclusive.CompareTo(ranges.Max) <= 0));
                return Task.FromResult(afterSplitResults);
            };
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
    }
}
