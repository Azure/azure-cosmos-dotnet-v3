namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.PartitionMapper;

    [TestClass]
    public class ContinuationResumeLogicTests
    {
        [TestMethod]
        public void ResumeEmptyStart()
        {
            FeedRangeEpkRange range1 = Range(min: string.Empty, max: "A");
            FeedRangeEpkRange range2 = Range(min: "A", max: "B");
            FeedRangeEpkRange range3 = Range(min: "B", max: string.Empty);
            ParallelContinuationToken token = Token(min: string.Empty, max: "A");

            RunTryGetInitializationInfo(
                Mapping(),
                Mapping((range1, token)),
                Mapping((CombineRanges(range2, range3), null)),
                new FeedRangeEpkRange[] { range1, range2, range3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeEmptyEnd()
        {
            FeedRangeEpkRange range1 = Range(min: string.Empty, max: "A");
            FeedRangeEpkRange range2 = Range(min: "A", max: "B");
            FeedRangeEpkRange range3 = Range(min: "B", max: string.Empty);
            ParallelContinuationToken token = Token(min: "B", max: string.Empty);

            RunTryGetInitializationInfo(
                Mapping((CombineRanges(range1, range2), null)),
                Mapping((range3, token)),
                Mapping(),
                new FeedRangeEpkRange[] { range1, range2, range3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeLeftPartition()
        {
            FeedRangeEpkRange range1 = Range(min: string.Empty, max: "A");
            FeedRangeEpkRange range2 = Range(min: "A", max: "B");
            FeedRangeEpkRange range3 = Range(min: "B", max: "C");
            ParallelContinuationToken token = Token(min: string.Empty, max: "A");

            RunTryGetInitializationInfo(
                Mapping(),
                Mapping((range1, token)),
                Mapping((CombineRanges(range2, range3), null)),
                new FeedRangeEpkRange[] { range1, range2, range3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeMiddlePartition()
        {
            FeedRangeEpkRange range1 = Range(min: string.Empty, max: "A");
            FeedRangeEpkRange range2 = Range(min: "A", max: "B");
            FeedRangeEpkRange range3 = Range(min: "B", max: "C");
            ParallelContinuationToken token = Token(min: "A", max: "B");

            RunTryGetInitializationInfo(
                Mapping((range1, null)),
                Mapping((range2, token)),
                Mapping((range3, null)),
                new FeedRangeEpkRange[] { range1, range2, range3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeRightPartition()
        {
            FeedRangeEpkRange range1 = Range(min: string.Empty, max: "A");
            FeedRangeEpkRange range2 = Range(min: "A", max: "B");
            FeedRangeEpkRange range3 = Range(min: "B", max: "C");
            ParallelContinuationToken token = Token(min: "B", max: "C");

            RunTryGetInitializationInfo(
                Mapping((CombineRanges(range1, range2), null)),
                Mapping((range3, token)),
                Mapping(),
                new FeedRangeEpkRange[] { range1, range2, range3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeOnAMerge()
        {
            // Suppose that we read from range 1
            FeedRangeEpkRange range1 = Range(min: string.Empty, max: "A");

            // Then Range 1 Merged with Range 2
            FeedRangeEpkRange range2 = Range(min: "A", max: "B");

            // And we have a continuation token for range 1
            ParallelContinuationToken token = Token(min: string.Empty, max: "A");

            // Then we should resume on range 1 with epk range filtering 
            // and still have range 2 with null continuation.
            RunTryGetInitializationInfo(
                Mapping(),
                Mapping((range1, token)),
                Mapping((range2, null)),
                new FeedRangeEpkRange[] { CombineRanges(range1, range2) },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeOnAMerge_LogicalPartition()
        {
            // Suppose that we read from range 2 with a logical partiton key that hashes to D
            FeedRangeEpkRange range2 = Range(min: "C", max: "E");

            // Then Range 1
            FeedRangeEpkRange range1 = Range(min: "A", max: "C");

            // and Range 3 merge with range 2
            FeedRangeEpkRange range3 = Range(min: "E", max: "G");

            // And we have a continuation token for range 2
            ParallelContinuationToken token = Token(min: "C", max: "E");

            // Then we should resume on range 2 with epk range filtering 
            // and still have range 1 and 3 with null continuation (but, since there is a logical partition key it won't match any results).
            RunTryGetInitializationInfo(
                Mapping((range1, null)),
                Mapping((range2, token)),
                Mapping((range3, null)),
                new FeedRangeEpkRange[] { CombineRanges(CombineRanges(range1, range2), range3) },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeOnASplit()
        {
            FeedRangeEpkRange range1 = Range(min: "A", max: "C");
            FeedRangeEpkRange range2 = Range(min: "C", max: "E");
            FeedRangeEpkRange range3 = Range(min: "E", max: "F");
            ParallelContinuationToken token = Token(min: "A", max: "E");

            RunTryGetInitializationInfo(
                Mapping(),
                Mapping((CombineRanges(range1, range2), token)),
                Mapping((range3, null)),
                new FeedRangeEpkRange[] { range1, range2, range3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeOnMultipleTokens()
        {
            FeedRangeEpkRange range = Range(min: "A", max: "F");
            ParallelContinuationToken token1 = Token(min: "A", max: "C");
            ParallelContinuationToken token2 = Token(min: "C", max: "E");

            RunTryGetInitializationInfo(
                Mapping(),
                Mapping((Range(min: "A", max: "C"), token1)),
                Mapping((Range(min: "C", max: "E"), token2), (Range(min: "E", max: "F"), null)),
                new FeedRangeEpkRange[] { range, },
                new IPartitionedToken[] { token1, token2 });
        }

        [TestMethod]
        public void ResumeOnSplit_LogicalParition()
        {
            // Suppose the partition spans epk range A to E
            // And the user send a query with partition key that hashes to C
            // The the token will look like:
            ParallelContinuationToken token = Token(min: "A", "E");

            // Now suppose there is a split that creates two partitions A to B and B to E
            // Now C will map to the partition that goes from B to E
            FeedRangeEpkRange range = Range(min: "B", max: "E");

            RunTryGetInitializationInfo(
                Mapping(),
                Mapping((range, token)),
                Mapping(),
                new FeedRangeEpkRange[] { range },
                new IPartitionedToken[] { token });
        }

        private static void RunTryGetInitializationInfo(
            IReadOnlyDictionary<FeedRangeEpkRange, IPartitionedToken> expectedLeftMapping,
            IReadOnlyDictionary<FeedRangeEpkRange, IPartitionedToken> expectedTargetMapping,
            IReadOnlyDictionary<FeedRangeEpkRange, IPartitionedToken> expectedRightMapping,
            IEnumerable<FeedRangeEpkRange> partitionKeyRanges,
            IEnumerable<IPartitionedToken> partitionedTokens)
        {
            TryCatch<PartitionMapping<IPartitionedToken>> tryGetInitializationInfo = PartitionMapper.MonadicGetPartitionMapping<IPartitionedToken>(
                partitionKeyRanges.OrderBy(x => Guid.NewGuid()).ToArray(),
                partitionedTokens.OrderBy(x => Guid.NewGuid()).ToList());
            Assert.IsTrue(tryGetInitializationInfo.Succeeded);
            PartitionMapping<IPartitionedToken> partitionMapping = tryGetInitializationInfo.Result;

            AssertPartitionMappingAreEqual(expectedLeftMapping, partitionMapping.MappingLeftOfTarget);
            AssertPartitionMappingAreEqual(expectedTargetMapping, partitionMapping.TargetMapping);
            AssertPartitionMappingAreEqual(expectedRightMapping, partitionMapping.MappingRightOfTarget);
        }

        private static void AssertPartitionMappingAreEqual(
            IReadOnlyDictionary<FeedRangeEpkRange, IPartitionedToken> expectedMapping,
            IReadOnlyDictionary<FeedRangeEpkRange, IPartitionedToken> actualMapping)
        {
            Assert.IsNotNull(expectedMapping);
            Assert.IsNotNull(actualMapping);

            Assert.AreEqual(expected: expectedMapping.Count, actual: actualMapping.Count);

            foreach (KeyValuePair<FeedRangeEpkRange, IPartitionedToken> kvp in expectedMapping)
            {
                Assert.IsTrue(
                    actualMapping.TryGetValue(
                        kvp.Key,
                        out IPartitionedToken partitionedToken));
                Assert.AreEqual(
                    expected: JsonConvert.SerializeObject(kvp.Value),
                    actual: JsonConvert.SerializeObject(partitionedToken));
            }
        }

        private static FeedRangeEpkRange Range(string min, string max)
        {
            return new FeedRangeEpkRange(min, max);
        }

        private static ParallelContinuationToken Token(string min, string max)
        {
            return new ParallelContinuationToken(
                token: Guid.NewGuid().ToString(),
                range: new Documents.Routing.Range<string>(
                    min: min,
                    max: max,
                    isMinInclusive: true,
                    isMaxInclusive: false));
        }

        private static Dictionary<FeedRangeEpkRange, IPartitionedToken> Mapping(params (FeedRangeEpkRange, IPartitionedToken)[] rangeAndTokens)
        {
            Dictionary<FeedRangeEpkRange, IPartitionedToken> mapping = new Dictionary<FeedRangeEpkRange, IPartitionedToken>();
            foreach ((FeedRangeEpkRange range, IPartitionedToken token) in rangeAndTokens)
            {
                mapping[range] = token;
            };

            return mapping;
        }

        private static FeedRangeEpkRange CombineRanges(FeedRangeEpkRange range1, FeedRangeEpkRange range2)
        {
            Assert.IsNotNull(range1);
            Assert.IsNotNull(range2);

            Assert.IsTrue(range1.Range.Min.CompareTo(range2.Range.Min) < 0);
            Assert.AreEqual(range1.Range.Max, range2.Range.Min);

            return new FeedRangeEpkRange(range1.Range.Min, range2.Range.Max);
        }
    }
}
