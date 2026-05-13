//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CollectionRoutingMapTest
    {
        [TestMethod]
        public void TestCollectionRoutingMap()
        {
            ServiceIdentity serviceIdentity0 = new ServiceIdentity("1", new Uri("http://1"), false);
            ServiceIdentity serviceIdentity1 = new ServiceIdentity("2", new Uri("http://2"), false);
            ServiceIdentity serviceIdentity2 = new ServiceIdentity("3", new Uri("http://3"), false);
            ServiceIdentity serviceIdentity3 = new ServiceIdentity("4", new Uri("http://4"), false);
            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(
                            new PartitionKeyRange{
                            Id = "2",
                            MinInclusive = "0000000050",
                            MaxExclusive = "0000000070"},
                            serviceIdentity2),

                        Tuple.Create(
                            new PartitionKeyRange{
                            Id = "0",
                            MinInclusive = "",
                            MaxExclusive = "0000000030"},
                            serviceIdentity0),

                        Tuple.Create(
                            new PartitionKeyRange{
                            Id = "1",
                            MinInclusive = "0000000030",
                            MaxExclusive = "0000000050"},
                            serviceIdentity1),

                         Tuple.Create(
                            new PartitionKeyRange{
                            Id = "3",
                            MinInclusive = "0000000070",
                            MaxExclusive = "FF"},
                            serviceIdentity3),


                    }, string.Empty, false);

            Assert.AreEqual("0", routingMap.OrderedPartitionKeyRanges[0].Id);
            Assert.AreEqual("1", routingMap.OrderedPartitionKeyRanges[1].Id);
            Assert.AreEqual("2", routingMap.OrderedPartitionKeyRanges[2].Id);
            Assert.AreEqual("3", routingMap.OrderedPartitionKeyRanges[3].Id);

            Assert.AreEqual(serviceIdentity0, routingMap.TryGetInfoByPartitionKeyRangeId("0"));
            Assert.AreEqual(serviceIdentity1, routingMap.TryGetInfoByPartitionKeyRangeId("1"));
            Assert.AreEqual(serviceIdentity2, routingMap.TryGetInfoByPartitionKeyRangeId("2"));
            Assert.AreEqual(serviceIdentity3, routingMap.TryGetInfoByPartitionKeyRangeId("3"));

            Assert.AreEqual("0", routingMap.GetRangeByEffectivePartitionKey("").Id);
            Assert.AreEqual("0", routingMap.GetRangeByEffectivePartitionKey("0000000000").Id);
            Assert.AreEqual("1", routingMap.GetRangeByEffectivePartitionKey("0000000030").Id);
            Assert.AreEqual("1", routingMap.GetRangeByEffectivePartitionKey("0000000031").Id);
            Assert.AreEqual("3", routingMap.GetRangeByEffectivePartitionKey("0000000071").Id);

            Assert.AreEqual("0", routingMap.TryGetRangeByPartitionKeyRangeId("0").Id);
            Assert.AreEqual("1", routingMap.TryGetRangeByPartitionKeyRangeId("1").Id);

            Assert.AreEqual(4, routingMap.GetOverlappingRanges(new[] { new Range<string>(PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey, true, false) }).Count);
            Assert.AreEqual(0, routingMap.GetOverlappingRanges(new[] { new Range<string>(PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, false, false) }).Count);
            IReadOnlyList<PartitionKeyRange> partitionKeyRanges =
                routingMap.GetOverlappingRanges(new[]
                                                    {
                                                        new Range<string>(
                                                            "0000000040",
                                                            "0000000040",
                                                            true,
                                                            true)
                                                    });

            Assert.AreEqual(1, partitionKeyRanges.Count);
            Assert.AreEqual("1", partitionKeyRanges.ElementAt(0).Id);

            IReadOnlyList<PartitionKeyRange> partitionKeyRanges1 =
               routingMap.GetOverlappingRanges(new[]
                                                    {
                                                        new Range<string>(
                                                            "0000000040",
                                                            "0000000045",
                                                            true,
                                                            true),
                                                        new Range<string>(
                                                            "0000000045",
                                                            "0000000046",
                                                            true,
                                                            true),
                                                       new Range<string>(
                                                            "0000000046",
                                                            "0000000050",
                                                            true,
                                                            true)
                                                    });

            Assert.AreEqual(2, partitionKeyRanges1.Count);
            Assert.AreEqual("1", partitionKeyRanges1.ElementAt(0).Id);
            Assert.AreEqual("2", partitionKeyRanges1.ElementAt(1).Id);
        }

        /// <summary>
        /// Validates that CollectionRoutingMap correctly identifies overlapping partition key ranges
        /// when using length-aware range comparators.
        /// This test ensures that EPK advanced comparison logic are applied as expected,
        /// and that the routing map's behavior is consistent regardless if the input EPK is fully or partially specified.
        /// The test covers scenarios where input EPKs are partial or fall on range boundaries,
        /// verifying that the correct partition key ranges are returned when using the new LengthAware comparators.
        /// </summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TestCollectionRoutingMapWithLengthAwareRangeComparators(bool isRoutingMapFullySpecified)
        {
            try
            {
                // Arrange: Set useLengthAwareComparer flag to "true" since the default is only true for Preview.
                CollectionRoutingMap routingMap = this.GenerateRoutingMap(isRoutingMapFullySpecified, true);

                // Test scenario 1.1: Input EPK is partial and falls on the boundary between two overlapping ranges.
                // The LengthAware comparators are able to correctly compare partial and full EPK ranges.Routing map is hybrid of fully specified and partially specified EPK ranges.
                // Input Min EPK 06AB34CFE4E482236BCACBBF50E234AB matches (significant bytes) with maxEPK of pkrangeid 1 and minEPK of pkrangeid 2.
                Range<string> inputPkRange = new Range<string>(
                "06AB34CFE4E482236BCACBBF50E234AB",
                "06AB34CFE4E482236BCACBBF50E234ABFF",
                true,
                false);

                // Expected outcome: Only partition key range with id 2 overlaps, as the LengthAware comparator correctly handles the partial EPK.
                IReadOnlyList<PartitionKeyRange> partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange);
                Assert.AreEqual(1, partitionKeyRanges1.Count);
                Assert.AreEqual("2", partitionKeyRanges1[0].Id);

                // Test scenario 1.2: Input EPK falls on a boundary and maxEPK also matches the next range's max.
                // The LengthAware comparator should return only the correct overlapping range.
                inputPkRange = new Range<string>(
                "0BD3FBE846AF75790CE63F78B1A81631",
                "0BD3FBE846AF75790CE63F78B1A81631FF",
                true,
                false);

                partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange);
                Assert.AreEqual(1, partitionKeyRanges1.Count);
                CollectionAssert.AreEquivalent(new[] { "11" }, partitionKeyRanges1.Select(r => r.Id).ToArray());

                inputPkRange = new Range<string>(
                "0D4DC2CD8F49C65A8E0C5306B61B43440D4DC2CD8F49C65A8E0C5306B61B4343",
                "0D4DC2CD8F49C65A8E0C5306B61B43440D4DC2CD8F49C65A8E0C5306B61B4344",
                true,
                false);

                partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange);
                Assert.AreEqual(1, partitionKeyRanges1.Count);
                CollectionAssert.AreEquivalent(new[] { "4" }, partitionKeyRanges1.Select(r => r.Id).ToArray());

                // Test scenario 1.2 (continued): Input EPK falls in boundary and maxEPK also matches the next range's max.
                inputPkRange = new Range<string>(
                "0BD3FBE846AF75790CE63F78B1A81620",
                "0BD3FBE846AF75790CE63F78B1A81631",
                true,
                false);

                partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange);
                Assert.AreEqual(1, partitionKeyRanges1.Count);
                CollectionAssert.AreEquivalent(new[] { "3" }, partitionKeyRanges1.Select(r => r.Id).ToArray());

                // Test scenario 1.3: Input EPK is partial and spans two overlapping ranges.
                /// Input Min EPK 0DCEB8CE51C6BFE84F4BD9409F69B9BB falls in both pkrangeid 4 and pkrangeid 5.
                inputPkRange = new Range<string>(
                "0DCEB8CE51C6BFE84F4BD9409F69B9BB",
                "0DCEB8CE51C6BFE84F4BD9409F69B9BBFF",
                true,
                false);

                partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange);
                Assert.AreEqual(2, partitionKeyRanges1.Count);
                CollectionAssert.AreEquivalent(new[] { "24", "5" }, partitionKeyRanges1.Select(r => r.Id).ToArray());


                ///Test scenario 1.4: Input EPK is partial and falls in a single range in the middle. Routing map is hybrid of fully specified and partially specified ranges.
                inputPkRange = new Range<string>(
                "02559A67F2724111B5E565DFA8711A00",
                "02559A67F2724111B5E565DFA8711A00",
                true,
                true);

                partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange);
                Assert.AreEqual(1, partitionKeyRanges1.Count);
                Assert.AreEqual("0", partitionKeyRanges1[0].Id);


                ///Test scenario 1.5: Input EPK is partial and falls in a single range in the middle. Routing map targeted range has partial EPK values only.
                inputPkRange = new Range<string>(
                "0D4DC2CD8F49C65A8E0C5306B61B4345",
                "0D4DC2CD8F49C65A8E0C5306B61B4345",
                true,
                true);

                partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange);
                Assert.AreEqual(1, partitionKeyRanges1.Count);
                Assert.AreEqual("4", partitionKeyRanges1[0].Id);


                // The following part of the test case verifies the routing map values i.e.backend ranges when they are not fully specified.
                if (!isRoutingMapFullySpecified)
                {
                    // Test scenario 1.6: Input EPK is fully specified and backend range is partially specified.
                    // The LengthAware comparator correctly matches the fully specified input to the partially specified backend range.
                    inputPkRange = new Range<string>(
                    "0D4DC2CD8F49C65A8E0C5306B61B434300000000000000000000000000000000",
                    "0D4EC2CD8F49C65A8E0C5306B61B434300000000000000000000000000000000",
                    true,
                    false);

                    // LengthAware comparator yields only the correct range.
                    partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange);
                    Assert.AreEqual(1, partitionKeyRanges1.Count);
                    CollectionAssert.AreEquivalent(new[] { "4" }, partitionKeyRanges1.Select(r => r.Id).ToArray());
                }
            }
            finally
            {
                // Clean up: Remove the environment variable after the test.
                Environment.SetEnvironmentVariable(ConfigurationManager.UseLengthAwareRangeComparator, null);
            }
        }

        /// <summary>
        /// Regression test for: HPK partial partition key query silently returns empty results.
        /// 
        /// Scenario: A container with a single pre-split PKRange [0, FF) (i.e., PKRange0, no children).
        /// A query uses a 2-of-3 HPK partial key, producing a 64-char EPK
        /// "00EB57A7EE7D5CAFE2751C18938111BC33F19FD36AD3B8EFC4A4AA4A09878654"
        /// and a query range [EPK, EPK+"FF"].
        /// 
        /// GetOverlappingRanges must return PKRange0 (count=1) since the EPK clearly falls
        /// within [0, FF). Returning empty (count=0) would cause EmptyQueryPipelineStage
        /// and silent zero results with no backend contact.
        /// 
        /// Confirmed via memory dump: single PKRange in orderedPartitionKeyRanges._size=1,
        /// goneRanges._count=0, useLengthAwareRangeComparer=false (SDK 3.58.0).
        /// </summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TestGetOverlappingRanges_PresplitMap_PartialHpkEpk_MustReturnOneRange(bool useLengthAwareComparer)
        {
            // Arrange: Single pre-split PKRange0 covering the full keyspace [0, FF)
            // This matches the memory dump: orderedPartitionKeyRanges._size=1, goneRanges._count=0
            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                {
                    Tuple.Create(
                        new PartitionKeyRange
                        {
                            Id = "0",
                            MinInclusive = "",
                            MaxExclusive = "FF"
                        },
                        (ServiceIdentity)null)
                },
                string.Empty,
                useLengthAwareComparer);

            // Customer's 64-char partial EPK (2-of-3 HPK components)
            // Confirmed from production diagnostic: EPK 00EB57A7...654 
            string epk = "00EB57A7EE7D5CAFE2751C18938111BC33F19FD36AD3B8EFC4A4AA4A09878654";
            Range<string> queryRange = new Range<string>(
                epk,
                epk + "FF",
                isMinInclusive: true,
                isMaxInclusive: true);

            // Act
            IReadOnlyList<PartitionKeyRange> overlappingRanges = routingMap.GetOverlappingRanges(queryRange);

            // Assert: EPK "00EB57A7..." is clearly within [0, FF) — must return PKRange0
            // Returning count=0 would cause silent empty query results (EmptyQueryPipelineStage)
            Assert.AreEqual(1, overlappingRanges.Count,
                $"GetOverlappingRanges returned {overlappingRanges.Count} ranges for EPK '{epk}' " +
                $"against PKRange0 [0,FF) with useLengthAwareComparer={useLengthAwareComparer}. " +
                "Expected 1 — empty result causes silent query failure.");
            Assert.AreEqual("0", overlappingRanges[0].Id);
        }

        /// <summary>
        /// Regression test: post-split routing map (2 ranges, 128-char boundaries) must correctly
        /// route a 96-char full-3-component HPK EPK to PKRange1.
        /// The 128-char split boundary ends in 32 zero chars (padding for the 3rd hash component).
        /// With both ordinal and LengthAware comparators, the EPK must resolve to PKRange1
        /// because the Cosmos DB service uses ordinal ordering: EPK_96 &lt; boundary_128 (ordinal)
        /// since EPK_96 is a prefix of boundary_128. Production evidence confirms the document
        /// is found in PKRange1. With the bug (before fix), LengthAware returned 0 ranges because
        /// the MinComparer placed the EPK at the start of PKRange2, but ordinal CheckOverlapping
        /// then rejected PKRange2, yielding an empty result and a silent EmptyQueryPipelineStage.
        /// </summary>
        [TestMethod]
        [DataRow(false, "00EB57A7EE7D5CAFE2751C18938111BC33F19FD36AD3B8EFC4A4AA4A09878654AABBCCDDAABBCCDDAABBCCDD00000000", 1, "1", DisplayName = "Ordinal: EPK clearly before boundary → in PKRange1")]
        [DataRow(true,  "00EB57A7EE7D5CAFE2751C18938111BC33F19FD36AD3B8EFC4A4AA4A09878654AABBCCDDAABBCCDDAABBCCDD00000000", 1, "1", DisplayName = "LengthAware: EPK clearly before boundary → in PKRange1")]
        [DataRow(false, "20D8EBCDF57AFA2EF9C0E7058BC2A6352816441F2F87193E9FCC5F9B374B0A582816441F2F87193E9FCC5F9B374B0A58", 1, "1", DisplayName = "Ordinal: EPK=trimmed-boundary (EPK_96 < boundary_128 ordinal) → in PKRange1")]
        [DataRow(true,  "20D8EBCDF57AFA2EF9C0E7058BC2A6352816441F2F87193E9FCC5F9B374B0A582816441F2F87193E9FCC5F9B374B0A58", 1, "1", DisplayName = "LengthAware: EPK=trimmed-boundary, fallback to ordinal → in PKRange1")]
        public void TestGetOverlappingRanges_PostSplitMap_FullHpkEpk_MustNotReturnEmpty(bool useLengthAwareComparer, string epk96, int expectedCount, string expectedRangeId)
        {
            // Customer split boundary from production diagnostic trace (128 chars = 3×32 hash + 32 zeros)
            string splitBoundary = "20D8EBCDF57AFA2EF9C0E7058BC2A6352816441F2F87193E9FCC5F9B374B0A582816441F2F87193E9FCC5F9B374B0A5800000000000000000000000000000000";

            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                {
                    Tuple.Create(
                        new PartitionKeyRange { Id = "1", MinInclusive = "",            MaxExclusive = splitBoundary },
                        (ServiceIdentity)null),
                    Tuple.Create(
                        new PartitionKeyRange { Id = "2", MinInclusive = splitBoundary, MaxExclusive = "FF" },
                        (ServiceIdentity)null),
                },
                string.Empty,
                useLengthAwareComparer);

            Assert.IsNotNull(routingMap, "Routing map creation failed");

            // Build a point range for the EPK (full 3-component HPK lookup)
            Range<string> queryRange = new Range<string>(epk96, epk96, isMinInclusive: true, isMaxInclusive: true);

            IReadOnlyList<PartitionKeyRange> overlappingRanges = routingMap.GetOverlappingRanges(queryRange);

            Assert.AreNotEqual(0, overlappingRanges.Count,
                $"GetOverlappingRanges returned 0 ranges for EPK '{epk96}' " +
                $"against post-split routing map with useLengthAwareComparer={useLengthAwareComparer}. " +
                "A count of 0 causes silent EmptyQueryPipelineStage — this is a bug.");
            Assert.AreEqual(expectedCount, overlappingRanges.Count,
                $"Expected {expectedCount} range(s) for EPK '{epk96}' with useLengthAwareComparer={useLengthAwareComparer}");
            Assert.AreEqual(expectedRangeId, overlappingRanges[0].Id,
                $"Expected range ID '{expectedRangeId}' but got '{overlappingRanges[0].Id}' for EPK '{epk96}' with useLengthAwareComparer={useLengthAwareComparer}");
        }

        /// <summary>
        /// Tests that with the legacy ordinal comparator (useLengthAwareRangeComparer=false), querying
        /// a full 3-component HPK partition whose EPK is exactly at the 128-char split boundary
        /// (EPK_96 == TrimEnd(boundary_128)) still routes to SOME range and NEVER returns 0 ranges.
        /// 
        /// Note: with the legacy comparator, the query might go to the WRONG range (PKRange1 instead 
        /// of PKRange2 for an EPK at the boundary), but that results in a backend 410 Gone + retry,
        /// NOT a silent EmptyQueryPipelineStage. This test guards against the silent empty result.
        /// </summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TestGetOverlappingRanges_PostSplitMap_EpkAtBoundary_MustNotReturnEmpty(bool useLengthAwareComparer)
        {
            // The split boundary (128 chars = H1+H2+H3 concatenated + 32 trailing zeros)
            string splitBoundary = "20D8EBCDF57AFA2EF9C0E7058BC2A6352816441F2F87193E9FCC5F9B374B0A582816441F2F87193E9FCC5F9B374B0A5800000000000000000000000000000000";

            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                {
                    Tuple.Create(
                        new PartitionKeyRange { Id = "1", MinInclusive = "",            MaxExclusive = splitBoundary },
                        (ServiceIdentity)null),
                    Tuple.Create(
                        new PartitionKeyRange { Id = "2", MinInclusive = splitBoundary, MaxExclusive = "FF" },
                        (ServiceIdentity)null),
                },
                string.Empty,
                useLengthAwareComparer);

            Assert.IsNotNull(routingMap, "Routing map creation failed");

            // EPK = first 96 chars of the 128-char boundary (i.e., TrimEnd(boundary) == EPK)
            // This is the case where hash(comp1)+hash(comp2)+hash(comp3) == splitBoundary[0..95]
            string epkAtBoundary = splitBoundary.Substring(0, 96);
            Range<string> queryRange = new Range<string>(epkAtBoundary, epkAtBoundary, isMinInclusive: true, isMaxInclusive: true);

            IReadOnlyList<PartitionKeyRange> overlappingRanges = routingMap.GetOverlappingRanges(queryRange);

            // Must NEVER return 0 ranges — the document must be in SOME range
            Assert.AreNotEqual(0, overlappingRanges.Count,
                $"GetOverlappingRanges returned 0 ranges for EPK '{epkAtBoundary}' at the boundary. " +
                $"useLengthAwareComparer={useLengthAwareComparer}. " +
                "A count of 0 causes silent EmptyQueryPipelineStage — this is a bug.");
        }

        /// <summary>
        /// Simulates multiple splits (8 ranges with 128-char boundaries) querying with a 96-char EPK.
        /// Verifies neither ordinal nor LengthAware comparator returns 0 ranges.
        /// Covers the case observed at 23:21 when the collection had PKRanges 3,4,5,7,8.
        /// </summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TestGetOverlappingRanges_MultiSplitMap_FullHpkEpk_MustNotReturnEmpty(bool useLengthAwareComparer)
        {
            // Simulated multi-split boundaries (128-char hex values, each ending with 32 zeros)
            string[] boundaries = new[]
            {
                "0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B00000000000000000000000000000000",
                "1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D00000000000000000000000000000000",
                "20D8EBCDF57AFA2EF9C0E7058BC2A6352816441F2F87193E9FCC5F9B374B0A582816441F2F87193E9FCC5F9B374B0A5800000000000000000000000000000000",
                "3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E00000000000000000000000000000000",
                "5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A2B3C4D5E6F0E6F1A00000000000000000000000000000000",
                "7A8B9C0D1E2F3A4B5C6D7E8F9A0B1C2D3E4F5A6B7C8D9E0F1A2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D7E8F9A0B00000000000000000000000000000000",
                "9C0D1E2F3A4B5C6D7E8F9A0B1C2D3E4F5A6B7C8D9E0F1A2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D7E8F9A0B1C2D3E00000000000000000000000000000000",
            };

            var ranges = new List<Tuple<PartitionKeyRange, ServiceIdentity>>();
            string prevMax = "";
            for (int i = 0; i < boundaries.Length; i++)
            {
                ranges.Add(Tuple.Create(
                    new PartitionKeyRange { Id = i.ToString(), MinInclusive = prevMax, MaxExclusive = boundaries[i] },
                    (ServiceIdentity)null));
                prevMax = boundaries[i];
            }
            ranges.Add(Tuple.Create(
                new PartitionKeyRange { Id = boundaries.Length.ToString(), MinInclusive = prevMax, MaxExclusive = "FF" },
                (ServiceIdentity)null));

            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                ranges,
                string.Empty,
                useLengthAwareComparer);

            Assert.IsNotNull(routingMap, "Routing map creation failed");

            // Customer's EPK for partition key Subscriptions:DefaultPartitionKey:DefaultPartitionKey
            // Clearly falls in the first range (starts with "00EB..." which is < first boundary "0E6F...")
            string epk = "00EB57A7EE7D5CAFE2751C18938111BC33F19FD36AD3B8EFC4A4AA4A09878654AABBCCDDAABBCCDDAABBCCDD00000000";
            Range<string> queryRange = new Range<string>(epk, epk, isMinInclusive: true, isMaxInclusive: true);

            IReadOnlyList<PartitionKeyRange> overlappingRanges = routingMap.GetOverlappingRanges(queryRange);

            Assert.AreNotEqual(0, overlappingRanges.Count,
                $"GetOverlappingRanges returned 0 ranges for EPK in multi-split map with useLengthAwareComparer={useLengthAwareComparer}. " +
                "A count of 0 causes silent EmptyQueryPipelineStage — this is a bug.");
            Assert.AreEqual(1, overlappingRanges.Count);
            Assert.AreEqual("0", overlappingRanges[0].Id);
        }

        /// <summary>
        /// Tests the "range query" variant: when EffectiveRangesForPartitionKey returns [EPK, EPK+"FF"]
        /// (the partial-key range format used in queries), GetOverlappingRanges must return at least 1 range
        /// from a post-split routing map. This is the exact range format produced by
        /// PartitionKeyInternal.GetEffectivePartitionKeyRange for partial HPK keys.
        /// </summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TestGetOverlappingRanges_PostSplitMap_PartialEpkSuffixRange_MustNotReturnEmpty(bool useLengthAwareComparer)
        {
            string splitBoundary = "20D8EBCDF57AFA2EF9C0E7058BC2A6352816441F2F87193E9FCC5F9B374B0A582816441F2F87193E9FCC5F9B374B0A5800000000000000000000000000000000";

            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                {
                    Tuple.Create(
                        new PartitionKeyRange { Id = "1", MinInclusive = "",            MaxExclusive = splitBoundary },
                        (ServiceIdentity)null),
                    Tuple.Create(
                        new PartitionKeyRange { Id = "2", MinInclusive = splitBoundary, MaxExclusive = "FF" },
                        (ServiceIdentity)null),
                },
                string.Empty,
                useLengthAwareComparer);

            Assert.IsNotNull(routingMap, "Routing map creation failed");

            // [EPK, EPK+"FF"] format: used for partial HPK queries (EffectiveRangesForPartitionKey)
            string epkMin = "00EB57A7EE7D5CAFE2751C18938111BC33F19FD36AD3B8EFC4A4AA4A09878654";
            string epkMax = epkMin + "FF";
            Range<string> queryRange = new Range<string>(epkMin, epkMax, isMinInclusive: true, isMaxInclusive: true);

            IReadOnlyList<PartitionKeyRange> overlappingRanges = routingMap.GetOverlappingRanges(queryRange);

            Assert.AreNotEqual(0, overlappingRanges.Count,
                $"GetOverlappingRanges returned 0 ranges for partial EPK range [{epkMin}, {epkMax}] " +
                $"against post-split map with useLengthAwareComparer={useLengthAwareComparer}. " +
                "A count of 0 causes silent EmptyQueryPipelineStage — this is a bug.");
        }

        // Test GetOverlappingRanges behavior when the UseLengthAwareRangeComparator environment flag is set to false,
        // which forces the use of legacy Min/Max comparators.
        [TestMethod]
        public void TestLegacyComparatorsUsedWhenLengthAwareComparatorFlagIsFalse()
        {
            try
            {

                // Arrange: Set useLengthAwareComparer to false to force legacy comparator usage.
                CollectionRoutingMap routingMap = this.GenerateRoutingMap(false, false);


                // Test scenario: Input EPK is partial and falls on the boundary between two overlapping ranges.
                // With the environment flag set, the routing map uses legacy Min/Max comparators, which do not distinguish
                // between partial and full EPKs. As a result, both partition key ranges with ids 1 and 2 are considered overlapping.
                // Input Min EPK 06AB34CFE4E482236BCACBBF50E234AB matches (significant bytes) with maxEPK of pkrangeid 1 and minEPK of pkrangeid 2.
                Range<string> inputPkRange = new Range<string>(
                "06AB34CFE4E482236BCACBBF50E234AB",
                "06AB34CFE4E482236BCACBBF50E234ABFF",
                true,
                false);
                IReadOnlyList<PartitionKeyRange> partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange);
                Assert.AreEqual(2, partitionKeyRanges1.Count);
                CollectionAssert.AreEquivalent(new[] { "1", "2" }, partitionKeyRanges1.Select(r => r.Id).ToArray());
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.UseLengthAwareRangeComparator, null);
            }
        }

        private CollectionRoutingMap GenerateRoutingMap(bool isFullySpecified, bool useLengthAwareComparer)
        {
            IEnumerable<Tuple<PartitionKeyRange, ServiceIdentity>> partitionKeyRangeTuples = new[]
                {
                    Tuple.Create(
                        new PartitionKeyRange
                        {
                            Id = "0",
                            MinInclusive = "",
                            MaxExclusive = "03559A67F2724111B5E565DFA8711A00"
                        },
                        (ServiceIdentity)null),

                    Tuple.Create(
                        new PartitionKeyRange
                        {
                            Id = "1",
                            MinInclusive = "03559A67F2724111B5E565DFA8711A00",
                            MaxExclusive = "06AB34CFE4E482236BCACBBF50E234AB00000000000000000000000000000000"
                        },
                        (ServiceIdentity)null),

                    Tuple.Create(
                        new PartitionKeyRange
                        {
                            Id = "2",
                            MinInclusive = "06AB34CFE4E482236BCACBBF50E234AB00000000000000000000000000000000",
                            MaxExclusive = "0BD3FBE846AF75790CE63F78B1A81620"
                        },
                        (ServiceIdentity)null),

                    Tuple.Create(
                        new PartitionKeyRange
                        {
                            Id = "3",
                            MinInclusive = "0BD3FBE846AF75790CE63F78B1A81620",
                            MaxExclusive = "0BD3FBE846AF75790CE63F78B1A8163100000000000000000000000000000000"
                        },
                        (ServiceIdentity)null),
                    Tuple.Create(
                        new PartitionKeyRange
                        {
                            Id = "11",
                            MinInclusive = "0BD3FBE846AF75790CE63F78B1A8163100000000000000000000000000000000",
                            MaxExclusive = "0BD3FBE846AF75790CE63F78B1A81631FF"
                        },
                        (ServiceIdentity)null),
                    Tuple.Create(
                        new PartitionKeyRange
                        {
                            Id = "12",
                            MinInclusive = "0BD3FBE846AF75790CE63F78B1A81631FF",
                            MaxExclusive = "0D4DC2CD8F49C65A8E0C5306B61B4343"
                        },
                        (ServiceIdentity)null),

                    Tuple.Create(
                        new PartitionKeyRange
                        {
                            Id = "4",
                            MinInclusive = "0D4DC2CD8F49C65A8E0C5306B61B4343",
                            MaxExclusive = "0D4EC2CD8F49C65A8E0C5306B61B4343"
                        },
                        (ServiceIdentity)null),

                    Tuple.Create(
                        new PartitionKeyRange
                        {
                            Id = "44",
                            MinInclusive = "0D4EC2CD8F49C65A8E0C5306B61B4343",
                            MaxExclusive = "0D5DC2CD8F49C65A8E0C5306B61B4343"
                        },
                        (ServiceIdentity)null),

                    Tuple.Create(
                        new PartitionKeyRange
                        {
                            Id = "24",
                            MinInclusive = "0D5DC2CD8F49C65A8E0C5306B61B4343",
                            MaxExclusive = "0DCEB8CE51C6BFE84F4BD9409F69B9BB2164DEBD78C50C850E0C1E3E3F0579ED"
                        },
                        (ServiceIdentity)null),

                    Tuple.Create(
                        new PartitionKeyRange
                        {
                            Id = "5",
                            MinInclusive = "0DCEB8CE51C6BFE84F4BD9409F69B9BB2164DEBD78C50C850E0C1E3E3F0579ED",
                            MaxExclusive = "1080F600C27CF98DC13F8639E94E7676"
                        },
                        (ServiceIdentity)null),
                    Tuple.Create(
                        new PartitionKeyRange
                        {
                            Id = "9",
                            MinInclusive = "1080F600C27CF98DC13F8639E94E7676",
                            MaxExclusive = "FF"
                        },
                        (ServiceIdentity)null),
                };

            if (isFullySpecified)
            {
                partitionKeyRangeTuples = partitionKeyRangeTuples
                    .Select(tuple =>
                    {
                        PartitionKeyRange range = tuple.Item1;
                        // Pad right to 64 bytes (128 hex chars) for MinInclusive and MaxExclusive if not empty
                        string PadTo64(string value)
                        {
                            if (string.IsNullOrEmpty(value) || value == "FF")
                                return value;
                            return value.PadRight(64, '0');
                        }
                        return Tuple.Create(
                            new PartitionKeyRange
                            {
                                Id = range.Id,
                                MinInclusive = PadTo64(range.MinInclusive),
                                MaxExclusive = PadTo64(range.MaxExclusive)
                            },
                            tuple.Item2
                        );
                    })
                    .ToList();
            }

            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                partitionKeyRangeTuples,
                string.Empty,
                useLengthAwareComparer);

            return routingMap;
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestInvalidRoutingMap()
        {
            CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(new PartitionKeyRange {Id = "1", MinInclusive = "0000000020", MaxExclusive = "0000000030"}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange { Id = "2", MinInclusive = "0000000025", MaxExclusive = "0000000035"}, (ServiceIdentity)null),
                    },
                string.Empty, false);
        }

        [TestMethod]
        public void TestIncompleteRoutingMap()
        {
            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(new PartitionKeyRange{ Id = "2", MinInclusive = "", MaxExclusive = "0000000030"}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{ Id = "3", MinInclusive = "0000000031", MaxExclusive = "FF"}, (ServiceIdentity)null),
                    },
                string.Empty, false);

            Assert.IsNull(routingMap);

            routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(new PartitionKeyRange{Id = "2", MinInclusive = "", MaxExclusive = "0000000030"}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{Id = "3", MinInclusive = "0000000030", MaxExclusive = "FF"}, (ServiceIdentity)null),
                    },
                string.Empty, false);

            Assert.IsNotNull(routingMap);
        }

        [TestMethod]
        public void TestGoneRanges()
        {
            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
              new[]
                    {
                        Tuple.Create(new PartitionKeyRange{ Id = "2", MinInclusive = "", MaxExclusive = "0000000030", Parents = new Collection<string>{"1", "0"}}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{ Id = "3", MinInclusive = "0000000030", MaxExclusive = "0000000032", Parents = new Collection<string>{"5"}}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{ Id = "4", MinInclusive = "0000000032", MaxExclusive = "FF"}, (ServiceIdentity)null),
                    },
              string.Empty, false);

            Assert.IsTrue(routingMap.IsGone("1"));
            Assert.IsTrue(routingMap.IsGone("0"));
            Assert.IsTrue(routingMap.IsGone("5"));

            Assert.IsFalse(routingMap.IsGone("2"));
            Assert.IsFalse(routingMap.IsGone("3"));
            Assert.IsFalse(routingMap.IsGone("4"));
            Assert.IsFalse(routingMap.IsGone("100"));
        }

        [TestMethod]
        public void TestTryCombineRanges()
        {
            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(
                            new PartitionKeyRange{
                            Id = "2",
                            MinInclusive = "0000000050",
                            MaxExclusive = "0000000070"},
                            (ServiceIdentity)null),

                        Tuple.Create(
                            new PartitionKeyRange{
                            Id = "0",
                            MinInclusive = "",
                            MaxExclusive = "0000000030"},
                            (ServiceIdentity)null),

                        Tuple.Create(
                            new PartitionKeyRange{
                            Id = "1",
                            MinInclusive = "0000000030",
                            MaxExclusive = "0000000050"},
                            (ServiceIdentity)null),

                         Tuple.Create(
                            new PartitionKeyRange{
                            Id = "3",
                            MinInclusive = "0000000070",
                            MaxExclusive = "FF"},
                            (ServiceIdentity)null),
                    }, string.Empty, false);

            CollectionRoutingMap newRoutingMap = routingMap.TryCombine(
                new[]
                    {
                        Tuple.Create(
                            new PartitionKeyRange{
                            Id = "4",
                            Parents = new Collection<string>{"0"},
                            MinInclusive = "",
                            MaxExclusive = "0000000010"},
                            (ServiceIdentity)null),

                         Tuple.Create(
                            new PartitionKeyRange{
                            Id = "5",
                            Parents = new Collection<string>{"0"},
                            MinInclusive = "0000000010",
                            MaxExclusive = "0000000030"},
                            (ServiceIdentity)null),
                    },
                    null, false);

            Assert.IsNotNull(newRoutingMap);

            newRoutingMap = routingMap.TryCombine(
                new[]
                    {
                        Tuple.Create(
                            new PartitionKeyRange{
                            Id = "6",
                            Parents = new Collection<string>{"0", "4"},
                            MinInclusive = "",
                            MaxExclusive = "0000000005"},
                            (ServiceIdentity)null),

                         Tuple.Create(
                            new PartitionKeyRange{
                            Id = "7",
                            Parents = new Collection<string>{"0", "4"},
                            MinInclusive = "0000000005",
                            MaxExclusive = "0000000010"},
                            (ServiceIdentity)null),

                         Tuple.Create(
                            new PartitionKeyRange{
                            Id = "8",
                            Parents = new Collection<string>{"0", "5"},
                            MinInclusive = "0000000010",
                            MaxExclusive = "0000000015"},
                            (ServiceIdentity)null),

                         Tuple.Create(
                            new PartitionKeyRange{
                            Id = "9",
                            Parents = new Collection<string>{"0", "5"},
                            MinInclusive = "0000000015",
                            MaxExclusive = "0000000030"},
                            (ServiceIdentity)null),
                    },
                    null, false);

            Assert.IsNotNull(newRoutingMap);

            newRoutingMap = routingMap.TryCombine(
                new[]
                    {
                        Tuple.Create(
                            new PartitionKeyRange{
                            Id = "10",
                            Parents = new Collection<string>{"0", "4", "6"},
                            MinInclusive = "",
                            MaxExclusive = "0000000002"},
                            (ServiceIdentity)null),
                    },
                    null, false);

            Assert.IsNull(newRoutingMap);
        }
    }
}