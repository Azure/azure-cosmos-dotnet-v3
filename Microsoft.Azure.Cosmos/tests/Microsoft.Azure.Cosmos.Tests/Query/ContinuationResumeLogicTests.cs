namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.PartitionMapper;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;

    [TestClass]
    public class ContinuationResumeLogicTests
    {
        [TestMethod]
        public void TestMatchRangesTocontinuationTokens_OneToOne()
        {
            FeedRangeEpk partitionKeyRange = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "FF",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            ParallelContinuationToken token = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "FF",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMapping = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { partitionKeyRange, token }
            };

            ContinuationResumeLogicTests.RunMatchRangesToContinuationTokens(
                expectedMapping,
                new FeedRangeEpk[] { partitionKeyRange },
                new ParallelContinuationToken[] { token });
        }

        [TestMethod]
        public void TestMatchRangesTocontinuationTokens_OneToMany()
        {
            FeedRangeEpk partitionKeyRange1 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "A",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            FeedRangeEpk partitionKeyRange2 = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: "A",
                    max: "B",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            ParallelContinuationToken token = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "B",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMapping = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { partitionKeyRange1, token },
                { partitionKeyRange2, token }
            };

            ContinuationResumeLogicTests.RunMatchRangesToContinuationTokens(
                expectedMapping,
                new FeedRangeEpk[] { partitionKeyRange1, partitionKeyRange2 },
                new ParallelContinuationToken[] { token });
        }

        [TestMethod]
        public void TestMatchRangesTocontinuationTokens_OneToNone()
        {
            FeedRangeEpk partitionKeyRange = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "A",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            ParallelContinuationToken token = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>(
                    min: "B",
                    max: "C",
                    isMinInclusive: true,
                    isMaxInclusive: false));

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMapping = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { partitionKeyRange, null },
            };

            ContinuationResumeLogicTests.RunMatchRangesToContinuationTokens(
                expectedMapping,
                new FeedRangeEpk[] { partitionKeyRange },
                new ParallelContinuationToken[] { token });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestMatchRangesTocontinuationTokens_ArgumentNullException()
        {
            ContinuationResumeLogicTests.RunMatchRangesToContinuationTokens(
                expectedMapping: null,
                partitionKeyRanges: new FeedRangeEpk[] { },
                partitionedTokens: null);
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
                    max: "B",
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
                { pkRange2, token },
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
                    min: "A",
                    max: string.Empty,
                    isMinInclusive: true,
                    isMaxInclusive: false));

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingLeftPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { pkRange1, null },
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingTargetPartition = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {
                { pkRange2, token },
            };

            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMappingRightPartitions = new Dictionary<FeedRangeEpk, IPartitionedToken>()
            {

                { pkRange3, token },
            };

            RunTryGetInitializationInfo(
                expectedMappingLeftPartitions,
                expectedMappingTargetPartition,
                expectedMappingRightPartitions,
                new FeedRangeEpk[] { pkRange1, pkRange2, pkRange3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void TestTryGetInitializationInfo_ResumeLeftMostPartition()
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
                { pkRange2, null},
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
                { pkRange1, null},
                { pkRange2, null},
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

        private static void RunMatchRangesToContinuationTokens(
            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> expectedMapping,
            IEnumerable<FeedRangeEpk> partitionKeyRanges,
            IEnumerable<IPartitionedToken> partitionedTokens)
        {
            IReadOnlyDictionary<FeedRangeEpk, IPartitionedToken> actualMapping = PartitionMapper.MatchRangesToContinuationTokens(
                partitionKeyRanges.OrderBy(x => Guid.NewGuid()).ToArray(),
                partitionedTokens.OrderBy(x => Guid.NewGuid()).ToList());

            ContinuationResumeLogicTests.AssertPartitionMappingAreEqual(
                expectedMapping,
                actualMapping);
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
    }
}
