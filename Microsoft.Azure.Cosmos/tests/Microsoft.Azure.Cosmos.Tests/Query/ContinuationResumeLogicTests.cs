namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.CosmosCrossPartitionQueryExecutionContext;

    [TestClass]
    public class ContinuationResumeLogicTests
    {
        [TestMethod]
        public void TestMatchRangesTocontinuationTokens_OneToOne()
        {
            PartitionKeyRange partitionKeyRange = new PartitionKeyRange()
            {
                MinInclusive = string.Empty,
                MaxExclusive = "FF",
                Id = "0"
            };

            CompositeContinuationToken token = new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "FF",
                    isMinInclusive: true,
                    isMaxInclusive: false),
                Token = "asdf"
            };

            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMapping = new Dictionary<PartitionKeyRange, IPartitionedToken>()
            {
                { partitionKeyRange, token }
            };

            ContinuationResumeLogicTests.RunMatchRangesToContinuationTokens(
                expectedMapping,
                new PartitionKeyRange[] { partitionKeyRange },
                new CompositeContinuationToken[] { token });
        }

        [TestMethod]
        public void TestMatchRangesTocontinuationTokens_OneToMany()
        {
            PartitionKeyRange partitionKeyRange1 = new PartitionKeyRange()
            {
                MinInclusive = string.Empty,
                MaxExclusive = "A",
                Id = "1"
            };

            PartitionKeyRange partitionKeyRange2 = new PartitionKeyRange()
            {
                MinInclusive = "A",
                MaxExclusive = "B",
                Id = "1"
            };

            CompositeContinuationToken token = new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "B",
                    isMinInclusive: true,
                    isMaxInclusive: false),
                Token = "asdf"
            };

            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMapping = new Dictionary<PartitionKeyRange, IPartitionedToken>()
            {
                { partitionKeyRange1, token },
                { partitionKeyRange2, token }
            };

            ContinuationResumeLogicTests.RunMatchRangesToContinuationTokens(
                expectedMapping,
                new PartitionKeyRange[] { partitionKeyRange1, partitionKeyRange2 },
                new CompositeContinuationToken[] { token });
        }

        [TestMethod]
        public void TestMatchRangesTocontinuationTokens_OneToNone()
        {
            PartitionKeyRange partitionKeyRange = new PartitionKeyRange()
            {
                MinInclusive = string.Empty,
                MaxExclusive = "A",
                Id = "1"
            };

            CompositeContinuationToken token = new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>(
                    min: "B",
                    max: "C",
                    isMinInclusive: true,
                    isMaxInclusive: false),
                Token = "asdf"
            };

            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMapping = new Dictionary<PartitionKeyRange, IPartitionedToken>()
            {
                { partitionKeyRange, null },
            };

            ContinuationResumeLogicTests.RunMatchRangesToContinuationTokens(
                expectedMapping,
                new PartitionKeyRange[] { partitionKeyRange },
                new CompositeContinuationToken[] { token });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestMatchRangesTocontinuationTokens_ArgumentNullException()
        {
            ContinuationResumeLogicTests.RunMatchRangesToContinuationTokens(
                expectedMapping: null,
                partitionKeyRanges: new PartitionKeyRange[] { },
                partitionedTokens: null);
        }

        [TestMethod]
        public void TestTryGetInitializationInfo_ResumeLeftMostPartition()
        {
            PartitionKeyRange pkRange1 = new PartitionKeyRange()
            {
                MinInclusive = string.Empty,
                MaxExclusive = "A",
                Id = "1"
            };

            PartitionKeyRange pkRange2 = new PartitionKeyRange()
            {
                MinInclusive = "A",
                MaxExclusive = "B",
                Id = "2"
            };

            PartitionKeyRange pkRange3 = new PartitionKeyRange()
            {
                MinInclusive = "B",
                MaxExclusive = "C",
                Id = "3"
            };

            CompositeContinuationToken token = new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>(
                    min: string.Empty,
                    max: "A",
                    isMinInclusive: true,
                    isMaxInclusive: false),
                Token = "asdf"
            };

            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMappingLeftPartitions = new Dictionary<PartitionKeyRange, IPartitionedToken>()
            {
            };

            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMappingTargetPartition = new Dictionary<PartitionKeyRange, IPartitionedToken>()
            {
                { pkRange1, token}
            };

            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMappingRightPartitions = new Dictionary<PartitionKeyRange, IPartitionedToken>()
            {
                { pkRange2, null},
                { pkRange3, null},
            };

            RunTryGetInitializationInfo(
                expectedMappingLeftPartitions,
                expectedMappingTargetPartition,
                expectedMappingRightPartitions,
                new PartitionKeyRange[] { pkRange1, pkRange2, pkRange3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void TestTryGetInitializationInfo_ResumeMiddlePartition()
        {
            PartitionKeyRange pkRange1 = new PartitionKeyRange()
            {
                MinInclusive = string.Empty,
                MaxExclusive = "A",
                Id = "1"
            };

            PartitionKeyRange pkRange2 = new PartitionKeyRange()
            {
                MinInclusive = "A",
                MaxExclusive = "B",
                Id = "2"
            };

            PartitionKeyRange pkRange3 = new PartitionKeyRange()
            {
                MinInclusive = "B",
                MaxExclusive = "C",
                Id = "3"
            };

            CompositeContinuationToken token = new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>(
                    min: "A",
                    max: "B",
                    isMinInclusive: true,
                    isMaxInclusive: false),
                Token = "asdf"
            };

            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMappingLeftPartitions = new Dictionary<PartitionKeyRange, IPartitionedToken>()
            {
                { pkRange1, null}
            };

            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMappingTargetPartition = new Dictionary<PartitionKeyRange, IPartitionedToken>()
            {
                { pkRange2, token},
            };

            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMappingRightPartitions = new Dictionary<PartitionKeyRange, IPartitionedToken>()
            {
                { pkRange3, null},
            };

            RunTryGetInitializationInfo(
                expectedMappingLeftPartitions,
                expectedMappingTargetPartition,
                expectedMappingRightPartitions,
                new PartitionKeyRange[] { pkRange1, pkRange2, pkRange3 },
                new IPartitionedToken[] { token });
        }

        [TestMethod]
        public void TestTryGetInitializationInfo_ResumeRightPartition()
        {
            PartitionKeyRange pkRange1 = new PartitionKeyRange()
            {
                MinInclusive = string.Empty,
                MaxExclusive = "A",
                Id = "1"
            };

            PartitionKeyRange pkRange2 = new PartitionKeyRange()
            {
                MinInclusive = "A",
                MaxExclusive = "B",
                Id = "2"
            };

            PartitionKeyRange pkRange3 = new PartitionKeyRange()
            {
                MinInclusive = "B",
                MaxExclusive = "C",
                Id = "3"
            };

            CompositeContinuationToken token = new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>(
                    min: "B",
                    max: "C",
                    isMinInclusive: true,
                    isMaxInclusive: false),
                Token = "asdf"
            };

            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMappingLeftPartitions = new Dictionary<PartitionKeyRange, IPartitionedToken>()
            {
                { pkRange1, null},
                { pkRange2, null},
            };

            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMappingTargetPartition = new Dictionary<PartitionKeyRange, IPartitionedToken>()
            {
                { pkRange3, token},
            };

            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMappingRightPartitions = new Dictionary<PartitionKeyRange, IPartitionedToken>()
            {
            };

            RunTryGetInitializationInfo(
                expectedMappingLeftPartitions,
                expectedMappingTargetPartition,
                expectedMappingRightPartitions,
                new PartitionKeyRange[] { pkRange1, pkRange2, pkRange3 },
                new IPartitionedToken[] { token });
        }

        private static void RunMatchRangesToContinuationTokens(
            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMapping,
            IEnumerable<PartitionKeyRange> partitionKeyRanges,
            IEnumerable<IPartitionedToken> partitionedTokens)
        {
            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> actualMapping = CosmosCrossPartitionQueryExecutionContext.MatchRangesToContinuationTokens(
                partitionKeyRanges.OrderBy(x => Guid.NewGuid()).ToArray(),
                partitionedTokens.OrderBy(x => Guid.NewGuid()).ToList());

            ContinuationResumeLogicTests.AssertPartitionMappingAreEqual(
                expectedMapping,
                actualMapping);
        }

        private static void RunTryGetInitializationInfo(
            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMappingLeftPartitions,
            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMappingTargetPartition,
            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMappingRightPartitions,
            IEnumerable<PartitionKeyRange> partitionKeyRanges,
            IEnumerable<IPartitionedToken> partitionedTokens)
        {
            TryCatch<PartitionMapping<IPartitionedToken>> tryGetInitializationInfo = CosmosCrossPartitionQueryExecutionContext.TryGetInitializationInfo(
                partitionKeyRanges.OrderBy(x => Guid.NewGuid()).ToArray(),
                partitionedTokens.OrderBy(x => Guid.NewGuid()).ToList());
            Assert.IsTrue(tryGetInitializationInfo.Succeeded);
            PartitionMapping<IPartitionedToken> partitionMapping = tryGetInitializationInfo.Result;

            AssertPartitionMappingAreEqual(expectedMappingLeftPartitions, partitionMapping.PartitionsLeftOfTarget);
            AssertPartitionMappingAreEqual(expectedMappingTargetPartition, partitionMapping.TargetPartition);
            AssertPartitionMappingAreEqual(expectedMappingRightPartitions, partitionMapping.PartitionsRightOfTarget);
        }

        private static void AssertPartitionMappingAreEqual(
            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> expectedMapping,
            IReadOnlyDictionary<PartitionKeyRange, IPartitionedToken> actualMapping)
        {
            Assert.IsNotNull(expectedMapping);
            Assert.IsNotNull(actualMapping);

            Assert.AreEqual(expected: expectedMapping.Count, actual: actualMapping.Count);

            foreach (KeyValuePair<PartitionKeyRange, IPartitionedToken> kvp in expectedMapping)
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
