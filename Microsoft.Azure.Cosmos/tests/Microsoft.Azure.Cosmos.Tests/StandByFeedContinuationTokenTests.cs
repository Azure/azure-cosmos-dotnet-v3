//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class StandByFeedContinuationTokenTests
    {
        [TestMethod]
        public void InitializeThroughPKRanges()
        {
            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "C", MaxExclusive ="D" },
            };

            StandByFeedContinuationToken compositeToken = new StandByFeedContinuationToken(keyRanges);
            Assert.AreEqual(keyRanges[0].MinInclusive, compositeToken.MinInclusiveRange);
            Assert.AreEqual(keyRanges[0].MaxExclusive, compositeToken.MaxExclusiveRange);
            compositeToken.PushCurrentToBack();
            Assert.AreEqual(keyRanges[1].MinInclusive, compositeToken.MinInclusiveRange);
            Assert.AreEqual(keyRanges[1].MaxExclusive, compositeToken.MaxExclusiveRange);
        }

        [TestMethod]
        public void InitializeThroughString()
        {
            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "C", MaxExclusive ="D" },
            };

            StandByFeedContinuationToken compositeTokenInitial = new StandByFeedContinuationToken(keyRanges);
            string serialized = compositeTokenInitial.ToString();

            StandByFeedContinuationToken compositeToken = new StandByFeedContinuationToken(serialized);
            Assert.AreEqual(keyRanges[0].MinInclusive, compositeToken.MinInclusiveRange);
            Assert.AreEqual(keyRanges[0].MaxExclusive, compositeToken.MaxExclusiveRange);
            compositeToken.PushCurrentToBack();
            Assert.AreEqual(keyRanges[1].MinInclusive, compositeToken.MinInclusiveRange);
            Assert.AreEqual(keyRanges[1].MaxExclusive, compositeToken.MaxExclusiveRange);
        }

        [TestMethod]
        public void SerializationIsExpected()
        {
            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
            {
                StandByFeedContinuationToken.BuildTokenForRange("A", "B", "C"),
                StandByFeedContinuationToken.BuildTokenForRange("D", "E", "F")
            };

            string expected = JsonConvert.SerializeObject(compositeContinuationTokens);

            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "D", MaxExclusive ="E" },
            };
            StandByFeedContinuationToken compositeToken = new StandByFeedContinuationToken(keyRanges);
            compositeToken.UpdateCurrentToken("C");
            compositeToken.PushCurrentToBack();
            compositeToken.UpdateCurrentToken("F");
            compositeToken.PushCurrentToBack();

            Assert.AreEqual(expected, compositeToken.ToString());
        }

        [TestMethod]
        public void UpdateCurrenTokenUpdatesFirst()
        {
            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
            {
                StandByFeedContinuationToken.BuildTokenForRange("A", "B", "C"),
                StandByFeedContinuationToken.BuildTokenForRange("D", "E", "")
            };

            string expected = JsonConvert.SerializeObject(compositeContinuationTokens);

            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "D", MaxExclusive ="E" },
            };
            StandByFeedContinuationToken compositeToken = new StandByFeedContinuationToken(keyRanges);
            compositeToken.UpdateCurrentToken("C");
            
            Assert.AreEqual(expected, compositeToken.ToString());
        }

        [TestMethod]
        public void PushToBackCircles()
        {
            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "D", MaxExclusive ="E" },
            };
            StandByFeedContinuationToken compositeToken = new StandByFeedContinuationToken(keyRanges);
            Assert.AreEqual(keyRanges[0].MinInclusive, compositeToken.MinInclusiveRange);
            compositeToken.PushCurrentToBack();
            Assert.AreEqual(keyRanges[1].MinInclusive, compositeToken.MinInclusiveRange);
            compositeToken.PushCurrentToBack();
            Assert.AreEqual(keyRanges[0].MinInclusive, compositeToken.MinInclusiveRange);
            compositeToken.PushCurrentToBack();
            Assert.AreEqual(keyRanges[1].MinInclusive, compositeToken.MinInclusiveRange);
        }

        [TestMethod]
        public void PushRangeWithTokenAddsAtTheEnd()
        {
            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" }
            };
            StandByFeedContinuationToken compositeToken = new StandByFeedContinuationToken(keyRanges);
            Assert.AreEqual(keyRanges[0].MinInclusive, compositeToken.MinInclusiveRange);

            compositeToken.PushRangeWithToken("C", "D", "E");
            compositeToken.PushCurrentToBack();
            Assert.AreEqual("C", compositeToken.MinInclusiveRange);
            Assert.AreEqual("D", compositeToken.MaxExclusiveRange);
            Assert.AreEqual("E", compositeToken.NextToken);
        }

        [TestMethod]
        public void HandleSplitGeneratesChildren()
        {
            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "B", MaxExclusive ="C" }
            };

            StandByFeedContinuationToken compositeToken = new StandByFeedContinuationToken(keyRanges);

            List<Documents.PartitionKeyRange> keyRangesAfterSplit = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="X" },
                new Documents.PartitionKeyRange() { MinInclusive = "X", MaxExclusive ="Z" },
                new Documents.PartitionKeyRange() { MinInclusive = "Z", MaxExclusive ="B" },

            };

            Assert.AreEqual(keyRanges[0].MinInclusive, compositeToken.MinInclusiveRange);
            Assert.AreEqual(keyRanges[0].MaxExclusive, compositeToken.MaxExclusiveRange);

            compositeToken.HandleSplit(keyRangesAfterSplit);
            // Current should be updated
            Assert.AreEqual(keyRangesAfterSplit[0].MinInclusive, compositeToken.MinInclusiveRange);
            Assert.AreEqual(keyRangesAfterSplit[0].MaxExclusive, compositeToken.MaxExclusiveRange);
            compositeToken.PushCurrentToBack();
            // Next should be the original second
            Assert.AreEqual(keyRanges[1].MinInclusive, compositeToken.MinInclusiveRange);
            Assert.AreEqual(keyRanges[1].MaxExclusive, compositeToken.MaxExclusiveRange);
            compositeToken.PushCurrentToBack();
            // Finally the new children
            Assert.AreEqual(keyRangesAfterSplit[1].MinInclusive, compositeToken.MinInclusiveRange);
            Assert.AreEqual(keyRangesAfterSplit[1].MaxExclusive, compositeToken.MaxExclusiveRange);
            compositeToken.PushCurrentToBack();
            Assert.AreEqual(keyRangesAfterSplit[2].MinInclusive, compositeToken.MinInclusiveRange);
            Assert.AreEqual(keyRangesAfterSplit[2].MaxExclusive, compositeToken.MaxExclusiveRange);
            // And go back to the beginning
            compositeToken.PushCurrentToBack();
            Assert.AreEqual(keyRangesAfterSplit[0].MinInclusive, compositeToken.MinInclusiveRange);
            Assert.AreEqual(keyRangesAfterSplit[0].MaxExclusive, compositeToken.MaxExclusiveRange);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void ConstructorWithInvalidTokenFormat()
        {
            new StandByFeedContinuationToken("notatoken");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorWithNullRangesThrows()
        {
            new StandByFeedContinuationToken(keyRanges: null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void ConstructorWithEmptyRangesThrows()
        {
            new StandByFeedContinuationToken(new List<Documents.PartitionKeyRange>());
        }

        [TestMethod]
        [ExpectedException(typeof(NullReferenceException))]
        public void ConstructorWithNullStringCreatesEmptyToken()
        {
            StandByFeedContinuationToken token = new StandByFeedContinuationToken(initialStandByFeedContinuationToken: null);
            string tokenString = token.NextToken;
        }

        [TestMethod]
        public void BuildTokenForRangeCreatesCorrectObject()
        {
            CompositeContinuationToken expected = new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>("A", "B", true, false),
                Token = "C"
            };

            CompositeContinuationToken built = StandByFeedContinuationToken.BuildTokenForRange(expected.Range.Min, expected.Range.Max, expected.Token);
            Assert.AreEqual(expected.Range.Min, built.Range.Min);
            Assert.AreEqual(expected.Range.Max, built.Range.Max);
            Assert.AreEqual(expected.Range.IsMinInclusive, built.Range.IsMinInclusive);
            Assert.AreEqual(expected.Range.IsMaxInclusive, built.Range.IsMaxInclusive);
            Assert.AreEqual(expected.Token, built.Token);
        }
    }
}
