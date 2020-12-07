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
        //#region match
        //[TestMethod]
        //public void TestMatchRangesTocontinuationTokens_OneToOne()
        //{
        //    FeedRangeEpk partitionKeyRange = new FeedRangeEpk(
        //        new Documents.Routing.Range<string>(
        //            min: string.Empty,
        //            max: "FF",
        //            isMinInclusive: true,
        //            isMaxInclusive: false));

        //    ParallelContinuationToken token = new ParallelContinuationToken(
        //        token: "asdf",
        //        range: new Documents.Routing.Range<string>(
        //            min: string.Empty,
        //            max: "FF",
        //            isMinInclusive: true,
        //            isMaxInclusive: false));

        //    IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMapping = new Dictionary<FeedRangeEpk, IPartitionedToken>()
        //    {
        //        { partitionKeyRange, token }
        //    };

        //    ContinuationResumeLogicTests.RunMatchRangesToContinuationTokens(
        //        expectedMapping,
        //        new FeedRangeEpk[] { partitionKeyRange },
        //        new ParallelContinuationToken[] { token });
        //}

        //[TestMethod]
        //public void TestMatchRangesTocontinuationTokens_OneToMany()
        //{
        //    FeedRangeEpk partitionKeyRange1 = new FeedRangeEpk(
        //        new Documents.Routing.Range<string>(
        //            min: string.Empty,
        //            max: "A",
        //            isMinInclusive: true,
        //            isMaxInclusive: false));

        //    FeedRangeEpk partitionKeyRange2 = new FeedRangeEpk(
        //        new Documents.Routing.Range<string>(
        //            min: "A",
        //            max: "B",
        //            isMinInclusive: true,
        //            isMaxInclusive: false));

        //    ParallelContinuationToken token = new ParallelContinuationToken(
        //        token: "asdf",
        //        range: new Documents.Routing.Range<string>(
        //            min: string.Empty,
        //            max: "B",
        //            isMinInclusive: true,
        //            isMaxInclusive: false));

        //    IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMapping = new Dictionary<FeedRangeEpk, IPartitionedToken>()
        //    {
        //        { partitionKeyRange1, token },
        //        { partitionKeyRange2, token }
        //    };

        //    ContinuationResumeLogicTests.RunMatchRangesToContinuationTokens(
        //        expectedMapping,
        //        new FeedRangeEpk[] { partitionKeyRange1, partitionKeyRange2 },
        //        new ParallelContinuationToken[] { token });
        //}

        //[TestMethod]
        //public void TestMatchRangesTocontinuationTokens_OneToNone()
        //{
        //    FeedRangeEpk partitionKeyRange = new FeedRangeEpk(
        //        new Documents.Routing.Range<string>(
        //            min: string.Empty,
        //            max: "A",
        //            isMinInclusive: true,
        //            isMaxInclusive: false));

        //    ParallelContinuationToken token = new ParallelContinuationToken(
        //        token: "asdf",
        //        range: new Documents.Routing.Range<string>(
        //            min: "B",
        //            max: "C",
        //            isMinInclusive: true,
        //            isMaxInclusive: false));

        //    IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMapping = new Dictionary<FeedRangeEpk, IPartitionedToken>()
        //    {
        //        { partitionKeyRange, null },
        //    };

        //    ContinuationResumeLogicTests.RunMatchRangesToContinuationTokens(
        //        expectedMapping,
        //        new FeedRangeEpk[] { partitionKeyRange },
        //        new ParallelContinuationToken[] { token });
        //}

        //[TestMethod]
        //[ExpectedException(typeof(ArgumentNullException))]
        //public void TestMatchRangesTocontinuationTokens_ArgumentNullException()
        //{
        //    ContinuationResumeLogicTests.RunMatchRangesToContinuationTokens(
        //        expectedMapping: null,
        //        partitionKeyRanges: new FeedRangeEpk[] { },
        //        partitionedTokens: null);
        //}
        //#endregion

        [TestMethod]
        public void TestTryGetInitializationInfo_ResumeOnAMerge()
        {
            // Suppose that we read from range 1
            FeedRangeEpk partitionKeyRange1 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "A",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            // Then Range 1 Merged with Range 2
            FeedRangeEpk partitionKeyRange2 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: "A",
                    max: "B",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            // To make Range 3
            FeedRangeEpk partitionKeyRange3 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "B",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            // And we have a continuation token for range 1
            ParallelContinuationToken token = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "A",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            // Then we should resume on range 1 with epk range filtering 
            // and still have range 2 with null continuation.
            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingLeftPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingTargetPartition = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { partitionKeyRange1, token },
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingRightPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {

                { partitionKeyRange2, null },
            };

            RunTryGetInitializationInfo(
                expectedMappingLeftPartitions,
                expectedMappingTargetPartition,
                expectedMappingRightPartitions,
                new FeedRangeEpk[] { partitionKeyRange3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void TestTryGetInitializationInfo_ResumeEmptyStart()
        {
            FeedRangeEpk pkRange1 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "A",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            FeedRangeEpk pkRange2 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: "A",
                    max: "B",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            FeedRangeEpk pkRange3 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: "B",
                    max: string.Empty,
                    isMinInclusive: true,
                    isMaxInclusive: false));

            ParallelContinuationToken token = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "A",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingLeftPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingTargetPartition = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { pkRange1, token },
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingRightPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { CombineRanges(pkRange2, pkRange3), null },
            };

            RunTryGetInitializationInfo(
                expectedMappingLeftPartitions,
                expectedMappingTargetPartition,
                expectedMappingRightPartitions,
                new FeedRangeEpk[] { pkRange1, pkRange2, pkRange3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void TestTryGetInitializationInfo_ResumeEmptyEnd()
        {
            FeedRangeEpk pkRange1 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "A",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            FeedRangeEpk pkRange2 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: "A",
                    max: "B",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            FeedRangeEpk pkRange3 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: "B",
                    max: string.Empty,
                    isMinInclusive: true,
                    isMaxInclusive: false));

            ParallelContinuationToken token = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>(
                    min: "B",
                    max: string.Empty,
                    isMinInclusive: true,
                    isMaxInclusive: false));

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingLeftPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { CombineRanges(pkRange1, pkRange2), null },
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingTargetPartition = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { pkRange3, token },
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingRightPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
            };

            RunTryGetInitializationInfo(
                expectedMappingLeftPartitions,
                expectedMappingTargetPartition,
                expectedMappingRightPartitions,
                new FeedRangeEpk[] { pkRange1, pkRange2, pkRange3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void TestTryGetInitializationInfo_ResumeLeftPartition()
        {
            FeedRangeEpk pkRange1 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "A",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            FeedRangeEpk pkRange2 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: "A",
                    max: "B",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            FeedRangeEpk pkRange3 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: "B",
                    max: "C",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            ParallelContinuationToken token = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "A",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingLeftPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingTargetPartition = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { pkRange1, token}
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingRightPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { CombineRanges(pkRange2, pkRange3), null},
            };

            RunTryGetInitializationInfo(
                expectedMappingLeftPartitions,
                expectedMappingTargetPartition,
                expectedMappingRightPartitions,
                new FeedRangeEpk[] { pkRange1, pkRange2, pkRange3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void TestTryGetInitializationInfo_ResumeMiddlePartition()
        {
            FeedRangeEpk pkRange1 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "A",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            FeedRangeEpk pkRange2 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: "A",
                    max: "B",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            FeedRangeEpk pkRange3 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: "B",
                    max: "C",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            ParallelContinuationToken token = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>(
                    min: "A",
                    max: "B",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingLeftPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { pkRange1, null}
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingTargetPartition = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { pkRange2, token},
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingRightPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { pkRange3, null},
            };

            RunTryGetInitializationInfo(
                expectedMappingLeftPartitions,
                expectedMappingTargetPartition,
                expectedMappingRightPartitions,
                new FeedRangeEpk[] { pkRange1, pkRange2, pkRange3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void TestTryGetInitializationInfo_ResumeRightPartition()
        {
            FeedRangeEpk pkRange1 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "A",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            FeedRangeEpk pkRange2 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: "A",
                    max: "B",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            FeedRangeEpk pkRange3 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: "B",
                    max: "C",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            ParallelContinuationToken token = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>(
                    min: "B",
                    max: "C",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingLeftPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { CombineRanges(pkRange1, pkRange2), null},
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingTargetPartition = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { pkRange3, token},
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingRightPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
            };

            RunTryGetInitializationInfo(
                expectedMappingLeftPartitions,
                expectedMappingTargetPartition,
                expectedMappingRightPartitions,
                new FeedRangeEpk[] { pkRange1, pkRange2, pkRange3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void TestTryGetInitializationInfo_ResumeLogicalParition_OnSplit()
        {
            // Suppose the partition spans epk range A to E
            // And the user send a query with partition key that hashes to C
            // The the token will look like:
            ParallelContinuationToken token = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>(
                    min: "A",
                    max: "E",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            // Now suppose there is a split that creates two partitions A to B and B to E
            // Now C will map to the partition that goes from B to E
            FeedRangeEpk pkRange = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: "B",
                    max: "E",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingLeftPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingTargetPartition = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { pkRange, token},
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingRightPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
            };

            RunTryGetInitializationInfo(
                expectedMappingLeftPartitions,
                expectedMappingTargetPartition,
                expectedMappingRightPartitions,
                new FeedRangeEpk[] { pkRange },
                new IPartitionedToken[] { token });
        }

        private static void RunTryGetInitializationInfo(
            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingLeftPartitions,
            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingTargetPartition,
            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingRightPartitions,
            IEnumerable<FeedRangeEpk> partitionKeyRanges,
            IEnumerable<IPartitionedToken> partitionedTokens)
        {
            TryCatch<PartitionMapping<IPartitionedToken>> tryGetInitializationInfo = PartitionMapper.MonadicGetPartitionMapping<IPartitionedToken>(
                partitionKeyRanges.OrderBy(x => Guid.NewGuid()).ToArray(),
                partitionedTokens.OrderBy(x => Guid.NewGuid()).ToList());
            Assert.IsTrue(tryGetInitializationInfo.Succeeded);
            PartitionMapping<IPartitionedToken> partitionMapping = tryGetInitializationInfo.Result;

            AssertPartitionMappingAreEqual(expectedMappingLeftPartitions, partitionMapping.PartitionsLeftOfTarget);
            AssertPartitionMappingAreEqual(expectedMappingTargetPartition, partitionMapping.TargetPartition);
            AssertPartitionMappingAreEqual(expectedMappingRightPartitions, partitionMapping.PartitionsRightOfTarget);
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
