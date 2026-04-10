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
            FeedRangeEpk range1 = Range(min: string.Empty, max: "A");
            FeedRangeEpk range2 = Range(min: "A", max: "B");
            FeedRangeEpk range3 = Range(min: "B", max: string.Empty);
            ParallelContinuationToken token = Token(min: string.Empty, max: "A");

            RunTryGetInitializationInfo(
                Mapping(),
                Mapping((range1, token)),
                Mapping((CombineRanges(range2, range3), null)),
                new FeedRangeEpk[] { range1, range2, range3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeEmptyEnd()
        {
            FeedRangeEpk range1 = Range(min: string.Empty, max: "A");
            FeedRangeEpk range2 = Range(min: "A", max: "B");
            FeedRangeEpk range3 = Range(min: "B", max: string.Empty);
            ParallelContinuationToken token = Token(min: "B", max: string.Empty);

            RunTryGetInitializationInfo(
                Mapping((CombineRanges(range1, range2), null)),
                Mapping((range3, token)),
                Mapping(),
                new FeedRangeEpk[] { range1, range2, range3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeLeftPartition()
        {
            FeedRangeEpk range1 = Range(min: string.Empty, max: "A");
            FeedRangeEpk range2 = Range(min: "A", max: "B");
            FeedRangeEpk range3 = Range(min: "B", max: "C");
            ParallelContinuationToken token = Token(min: string.Empty, max: "A");

            RunTryGetInitializationInfo(
                Mapping(),
                Mapping((range1, token)),
                Mapping((CombineRanges(range2, range3), null)),
                new FeedRangeEpk[] { range1, range2, range3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeMiddlePartition()
        {
            FeedRangeEpk range1 = Range(min: string.Empty, max: "A");
            FeedRangeEpk range2 = Range(min: "A", max: "B");
            FeedRangeEpk range3 = Range(min: "B", max: "C");
            ParallelContinuationToken token = Token(min: "A", max: "B");

            RunTryGetInitializationInfo(
                Mapping((range1, null)),
                Mapping((range2, token)),
                Mapping((range3, null)),
                new FeedRangeEpk[] { range1, range2, range3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeRightPartition()
        {
            FeedRangeEpk range1 = Range(min: string.Empty, max: "A");
            FeedRangeEpk range2 = Range(min: "A", max: "B");
            FeedRangeEpk range3 = Range(min: "B", max: "C");
            ParallelContinuationToken token = Token(min: "B", max: "C");

            RunTryGetInitializationInfo(
                Mapping((CombineRanges(range1, range2), null)),
                Mapping((range3, token)),
                Mapping(),
                new FeedRangeEpk[] { range1, range2, range3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeOnAMerge()
        {
            // Suppose that we read from range 1
            FeedRangeEpk range1 = Range(min: string.Empty, max: "A");

            // Then Range 1 Merged with Range 2
            FeedRangeEpk range2 = Range(min: "A", max: "B");

            // And we have a continuation token for range 1
            ParallelContinuationToken token = Token(min: string.Empty, max: "A");

            // Then we should resume on range 1 with epk range filtering 
            // and still have range 2 with null continuation.
            RunTryGetInitializationInfo(
                Mapping(),
                Mapping((range1, token)),
                Mapping((range2, null)),
                new FeedRangeEpk[] { CombineRanges(range1, range2) },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeOnAMerge_LogicalPartition()
        {
            // Suppose that we read from range 2 with a logical partiton key that hashes to D
            FeedRangeEpk range2 = Range(min: "C", max: "E");

            // Then Range 1
            FeedRangeEpk range1 = Range(min: "A", max: "C");

            // and Range 3 merge with range 2
            FeedRangeEpk range3 = Range(min: "E", max: "G");

            // And we have a continuation token for range 2
            ParallelContinuationToken token = Token(min: "C", max: "E");

            // Then we should resume on range 2 with epk range filtering 
            // and still have range 1 and 3 with null continuation (but, since there is a logical partition key it won't match any results).
            RunTryGetInitializationInfo(
                Mapping((range1, null)),
                Mapping((range2, token)),
                Mapping((range3, null)),
                new FeedRangeEpk[] { CombineRanges(CombineRanges(range1, range2), range3) },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeOnASplit()
        {
            FeedRangeEpk range1 = Range(min: "A", max: "C");
            FeedRangeEpk range2 = Range(min: "C", max: "E");
            FeedRangeEpk range3 = Range(min: "E", max: "F");
            ParallelContinuationToken token = Token(min: "A", max: "E");

            RunTryGetInitializationInfo(
                Mapping(),
                Mapping((CombineRanges(range1, range2), token)),
                Mapping((range3, null)),
                new FeedRangeEpk[] { range1, range2, range3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void ResumeOnMultipleTokens()
        {
            FeedRangeEpk range = Range(min: "A", max: "F");
            ParallelContinuationToken token1 = Token(min: "A", max: "C");
            ParallelContinuationToken token2 = Token(min: "C", max: "E");

            RunTryGetInitializationInfo(
                Mapping(),
                Mapping((Range(min: "A", max: "C"), token1)),
                Mapping((Range(min: "C", max: "E"), token2), (Range(min: "E", max: "F"), null)),
                new FeedRangeEpk[] { range, },
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
            FeedRangeEpk range = Range(min: "B", max: "E");

            RunTryGetInitializationInfo(
                Mapping(),
                Mapping((range, token)),
                Mapping(),
                new FeedRangeEpk[] { range },
                new IPartitionedToken[] { token });
        }

        private static void RunTryGetInitializationInfo(
            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedLeftMapping,
            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedTargetMapping,
            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedRightMapping,
            IEnumerable<FeedRangeEpk> partitionKeyRanges,
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
            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMapping,
            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> actualMapping)
        {
            Assert.IsNotNull(expectedMapping);
            Assert.IsNotNull(actualMapping);

            Assert.AreEqual(expected: expectedMapping.Count, actual: actualMapping.Count);

            foreach (KeyValuePair<FeedRangeEpk, IPartitionedToken> kvp in expectedMapping)
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

        private static FeedRangeEpk Range(string min, string max)
        {
            return new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: min,
                    max: max,
                    isMinInclusive: true,
                    isMaxInclusive: false));
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

        private static Dictionary<FeedRangeEpk, IPartitionedToken> Mapping(params (FeedRangeEpk, IPartitionedToken)[] rangeAndTokens)
        {
            Dictionary<FeedRangeEpk, IPartitionedToken> mapping = new Dictionary<FeedRangeEpk, IPartitionedToken>();
            foreach ((FeedRangeEpk range, IPartitionedToken token) in rangeAndTokens)
            {
                mapping[range] = token;
            };

            return mapping;
        }

        private static FeedRangeEpk CombineRanges(FeedRangeEpk range1, FeedRangeEpk range2)
        {
            Assert.IsNotNull(range1);
            Assert.IsNotNull(range2);

            Assert.IsTrue(range1.Range.Min.CompareTo(range2.Range.Min) < 0);
            Assert.AreEqual(range1.Range.Max, range2.Range.Min);

            return new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: range1.Range.Min,
                    max: range2.Range.Max,
                    isMinInclusive: true,
                    isMaxInclusive: false));
        }
    }
}