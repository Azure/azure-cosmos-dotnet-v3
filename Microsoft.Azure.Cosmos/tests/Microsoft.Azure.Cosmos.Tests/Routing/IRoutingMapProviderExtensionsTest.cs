//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests <see cref="IRoutingMapProviderExtensions"/> class.
    /// </summary>
    [TestClass]
    public class IRoutingMapProviderExtensionsTest
    {
        private class MockRoutingMapProvider : IRoutingMapProvider
        {
            readonly CollectionRoutingMap routingMap;

            public MockRoutingMapProvider(IList<PartitionKeyRange> ranges)
            {
                this.routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(ranges.Select(r => Tuple.Create(r, (ServiceIdentity)null)), "", false);
            }

            public Task<IReadOnlyList<PartitionKeyRange>> TryGetOverlappingRangesAsync(
                string collectionIdOrNameBasedLink,
                Range<string> range,
                ITrace trace,
                bool forceRefresh = false)
            {
                return Task.FromResult(this.routingMap.GetOverlappingRanges(range));
            }

            public Task<PartitionKeyRange> TryGetPartitionKeyRangeByIdAsync(
                string collectionResourceId,
                string partitionKeyRangeId,
                ITrace trace,
                bool forceRefresh = false)
            {
                return Task.FromResult(this.routingMap.TryGetRangeByPartitionKeyRangeId(partitionKeyRangeId));
            }
        }

        private readonly MockRoutingMapProvider routingMapProvider =
            new MockRoutingMapProvider(
                new[]
                    {
                        new PartitionKeyRange{MinInclusive = "",   MaxExclusive = "000A", Id="0"},
                        new PartitionKeyRange{MinInclusive = "000A", MaxExclusive = "000D", Id="1"},
                        new PartitionKeyRange{MinInclusive = "000D", MaxExclusive = "0012", Id="2"},
                        new PartitionKeyRange{MinInclusive = "0012", MaxExclusive = "0015", Id="3"},
                        new PartitionKeyRange{MinInclusive = "0015", MaxExclusive = "0020", Id="4"},
                        new PartitionKeyRange{MinInclusive = "0020", MaxExclusive = "0040", Id="5"},
                        new PartitionKeyRange{MinInclusive = "0040", MaxExclusive = "FF", Id="6"},
                    });

        /// <summary>
        /// Tests case when input is not sorted.
        /// </summary>
        [TestMethod]
        [Owner("padmaa")]
        public async Task TestNonSortedRanges()
        {
            IList<PartitionKeyRange> ranges = await this.routingMapProvider.TryGetOverlappingRangesAsync(
                "dbs/db1/colls/coll1",
                new[] { new Range<string>("0B", "0B", true, true), new Range<string>("0A", "0A", true, true) },
                NoOpTrace.Singleton);

            Assert.AreEqual("6", string.Join(",", ranges.Select(r => r.Id)));
        }

        /// <summary>
        /// Tests case when input contains overlapping ranges.
        /// </summary>
        [TestMethod]
        [Owner("padmaa")]
        public async Task TestOverlappingRanges1()
        {
            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => this.routingMapProvider.TryGetOverlappingRangesAsync(
                "dbs/db1/colls/coll1",
                new[] { new Range<string>("0A", "0D", true, true), new Range<string>("0B", "0E", true, true) },
                NoOpTrace.Singleton));

            AssertOverlapMessage(exception, "[0A,0D]", "[0B,0E]");
        }

        /// <summary>
        /// Tests case when input contains overlapping ranges.
        /// </summary>
        [TestMethod]
        [Owner("padmaa")]
        public async Task TestOverlappingRanges2()
        {
            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => this.routingMapProvider.TryGetOverlappingRangesAsync(
                "dbs/db1/colls/coll1",
                new[] { new Range<string>("0A", "0D", true, true), new Range<string>("0D", "0E", true, true) },
                NoOpTrace.Singleton));

            AssertOverlapMessage(exception, "[0A,0D]", "[0D,0E]");
        }

        [DataTestMethod]
        [DataRow(true, true, "[000A,0020]")]
        [DataRow(true, false, "[000A,0020)")]
        [DataRow(false, true, "(000A,0020]")]
        [DataRow(false, false, "(000A,0020)")]
        public async Task TestContainedPointIncludesConflictingRanges(bool isMinInclusive, bool isMaxInclusive, string expectedOuterRange)
        {
            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => this.routingMapProvider.TryGetOverlappingRangesAsync(
                "dbs/db1/colls/coll1",
                new[]
                {
                    Range<string>.GetPointRange("0040"),
                    Range<string>.GetPointRange("0010"),
                    new Range<string>("000A", "0020", isMinInclusive, isMaxInclusive),
                },
                NoOpTrace.Singleton));

            AssertOverlapMessage(exception, expectedOuterRange, "[0010,0010]");
            Assert.IsFalse(exception.Message.Contains("0040"), "Only the first conflicting pair should be reported.");
        }

        [DataTestMethod]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(false, false)]
        public async Task TestTouchingRangesWithoutSharedInclusivePoint(bool isFirstMaxInclusive, bool isSecondMinInclusive)
        {
            List<PartitionKeyRange> ranges = await this.routingMapProvider.TryGetOverlappingRangesAsync(
                "dbs/db1/colls/coll1",
                new[]
                {
                    new Range<string>("000B", "000D", true, isFirstMaxInclusive),
                    new Range<string>("000D", "0010", isSecondMinInclusive, false),
                },
                NoOpTrace.Singleton);

            Assert.AreEqual("1,2", string.Join(",", ranges.Select(range => range.Id)));
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task TestPointAtRangeMaximum(bool isMaxInclusive)
        {
            Range<string>[] queryRanges =
            {
                new Range<string>("000B", "000D", true, isMaxInclusive),
                Range<string>.GetPointRange("000D"),
            };
            if (isMaxInclusive)
            {
                ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => this.routingMapProvider.TryGetOverlappingRangesAsync(
                    "dbs/db1/colls/coll1", queryRanges, NoOpTrace.Singleton));

                AssertOverlapMessage(exception, "[000B,000D]", "[000D,000D]");
            }
            else
            {
                List<PartitionKeyRange> ranges = await this.routingMapProvider.TryGetOverlappingRangesAsync(
                    "dbs/db1/colls/coll1", queryRanges, NoOpTrace.Singleton);

                Assert.AreEqual("1,2", string.Join(",", ranges.Select(range => range.Id)));
            }
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task TestInputIsEnumeratedOnce(bool hasOverlap)
        {
            int enumerationCount = 0;
            IEnumerable<Range<string>> EnumerateOnce()
            {
                Assert.AreEqual(0, enumerationCount++, "The input ranges must not be enumerated a second time.");
                yield return new Range<string>("000B", "000D", true, false);
                yield return hasOverlap
                    ? Range<string>.GetPointRange("000C")
                    : new Range<string>("0021", "0030", true, false);
            }

            if (hasOverlap)
            {
                ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => this.routingMapProvider.TryGetOverlappingRangesAsync(
                    "dbs/db1/colls/coll1", EnumerateOnce(), NoOpTrace.Singleton));

                AssertOverlapMessage(exception, "[000B,000D)", "[000C,000C]");
            }
            else
            {
                List<PartitionKeyRange> ranges = await this.routingMapProvider.TryGetOverlappingRangesAsync(
                    "dbs/db1/colls/coll1", EnumerateOnce(), NoOpTrace.Singleton);

                Assert.AreEqual("1,5", string.Join(",", ranges.Select(range => range.Id)));
            }

            Assert.AreEqual(1, enumerationCount);
        }

        private static void AssertOverlapMessage(ArgumentException exception, string firstRange, string secondRange)
        {
            StringAssert.StartsWith(exception.Message, "sortedRanges had overlaps.");
            StringAssert.Contains(exception.Message, firstRange);
            StringAssert.Contains(exception.Message, secondRange);
        }

        /// <summary>
        /// Tests case with various overlapping options.
        /// </summary>
        [TestMethod]
        [Owner("padmaa")]
        public async Task TestDuplicates()
        {
            {
                // Deep Copy Duplicate
                IList<PartitionKeyRange> ranges = await this.routingMapProvider.TryGetOverlappingRangesAsync(
                "dbs/db1/colls/coll1",
                new[]
                {
                    new Range<string>("", "FF", true, false),
                    // Duplicate
                    new Range<string>("", "FF", true, false),
                },
                NoOpTrace.Singleton);

                Assert.AreEqual("0,1,2,3,4,5,6", string.Join(",", ranges.Select(r => r.Id)));
            }

            {
                // Shallow Copy Duplicate
                List<Range<string>> queryRanges = new List<Range<string>>()
                {
                    new Range<string>("", "FF", true, false),
                };
                queryRanges.Add(queryRanges.Last());

                IList<PartitionKeyRange> ranges = await this.routingMapProvider.TryGetOverlappingRangesAsync(
                    "dbs/db1/colls/coll1",
                    queryRanges,
                    NoOpTrace.Singleton);

                Assert.AreEqual("0,1,2,3,4,5,6", string.Join(",", ranges.Select(r => r.Id)));
            }
        }

        /// <summary>
        /// Tests case with various overlapping options.
        /// </summary>
        [TestMethod]
        [Owner("padmaa")]
        public async Task TestGetOverlappingRanges()
        {
            IList<PartitionKeyRange> ranges = await this.routingMapProvider.TryGetOverlappingRangesAsync(
                "dbs/db1/colls/coll1",
                new[]
                    {
                        new Range<string>("000B", "000E", true, false),
                        new Range<string>("000E", "000F", true, false),
                        new Range<string>("000F", "0010", true, true),
                        new Range<string>("0015", "0015", true, true)
                    },
                NoOpTrace.Singleton);

            Assert.AreEqual("1,2,4", string.Join(",", ranges.Select(r => r.Id)));

            // query for minimal point
            ranges = await this.routingMapProvider.TryGetOverlappingRangesAsync(
                "dbs/db1/colls/coll1",
                new[]
                    {
                        new Range<string>("", "", true, true),
                    },
                NoOpTrace.Singleton);

            Assert.AreEqual("0", string.Join(",", ranges.Select(r => r.Id)));

            // query for empty range
            ranges = await this.routingMapProvider.TryGetOverlappingRangesAsync(
                "dbs/db1/colls/coll1",
                new[]
                    {
                        new Range<string>("", "", true, false),
                    },
                NoOpTrace.Singleton);

            Assert.AreEqual("", string.Join(",", ranges.Select(r => r.Id)));

            // entire range
            ranges = await this.routingMapProvider.TryGetOverlappingRangesAsync(
                "dbs/db1/colls/coll1",
                new[]
                    {
                        new Range<string>("", "FF", true, false),
                    },
                NoOpTrace.Singleton);

            Assert.AreEqual("0,1,2,3,4,5,6", string.Join(",", ranges.Select(r => r.Id)));

            // matching range
            ranges = await this.routingMapProvider.TryGetOverlappingRangesAsync(
                "dbs/db1/colls/coll1",
                new[]
                    {
                        new Range<string>("0012", "0015", true, false),
                    },
                NoOpTrace.Singleton);

            Assert.AreEqual("3", string.Join(",", ranges.Select(r => r.Id)));

            // matching range and a little bit more.
            ranges = await this.routingMapProvider.TryGetOverlappingRangesAsync(
                "dbs/db1/colls/coll1",
                new[]
                    {
                        new Range<string>("0012", "0015", false, true),
                    },
                NoOpTrace.Singleton);

            Assert.AreEqual("3,4", string.Join(",", ranges.Select(r => r.Id)));
        }

    }
}