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


                    }, string.Empty, null);

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
        /// when using length-aware range comparators for containers with Hierarchical Partition Keys (HPK).
        /// This test ensures that EPK advanced comparison logic are applied as expected,
        /// and that the routing map's behavior is consistent with the new design for HPK-enabled containers.
        /// The test covers scenarios where input EPKs are partial or fall on range boundaries,
        /// verifying that the correct partition key ranges are returned when using the new LengthAware comparators.
        /// </summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TestCollectionRoutingMapWithLengthAwareRangeComparators(bool isRoutingMapFullySpecified)
        {
            // Setup routing map with HPK (Hierarchical Partition Key) enabled.
            CollectionRoutingMap routingMap = this.GenerateRoutingMap(isRoutingMapFullySpecified, this.GeneratePartitionKeyDefinition(2));

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

            // Setup routing map without HPK (single hash key).
            CollectionRoutingMap routingMapWithNoHpk = this.GenerateRoutingMap(isRoutingMapFullySpecified, this.GeneratePartitionKeyDefinition(1));

            // Expected outcome: Both partition key ranges with ids 1 and 2 overlap, as the legacy comparator does not distinguish partial/full EPKs.
            IReadOnlyList<PartitionKeyRange> partitionKeyRanges2 = routingMapWithNoHpk.GetOverlappingRanges(inputPkRange);
            Assert.AreEqual(2, partitionKeyRanges2.Count);
            CollectionAssert.AreEquivalent(new[] { "1", "2" }, partitionKeyRanges2.Select(r => r.Id).ToArray());

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

            // Expected outcome: Both partition key ranges with ids 3 and 11 overlap, as the legacy comparator does not handle partial/full EPKs.
            partitionKeyRanges2 = routingMapWithNoHpk.GetOverlappingRanges(inputPkRange);
            Assert.AreEqual(2, partitionKeyRanges2.Count);
            CollectionAssert.AreEquivalent(new[] { "11", "3" }, partitionKeyRanges2.Select(r => r.Id).ToArray());

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


            partitionKeyRanges1 = routingMapWithNoHpk.GetOverlappingRanges(inputPkRange);
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

                // Legacy comparator yields two ranges due to lack of length awareness.
                partitionKeyRanges1 = routingMapWithNoHpk.GetOverlappingRanges(inputPkRange);
                Assert.AreEqual(2, partitionKeyRanges1.Count);
                CollectionAssert.AreEquivalent(new[] { "4", "44" }, partitionKeyRanges1.Select(r => r.Id).ToArray());

                // LengthAware comparator yields only the correct range.
                partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange);
                Assert.AreEqual(1, partitionKeyRanges1.Count);
                CollectionAssert.AreEquivalent(new[] { "4" }, partitionKeyRanges1.Select(r => r.Id).ToArray());
            }

        }

        // Test GetOverlappingRanges behavior when the UseLengthAwareRangeComparator environment flag is set to false,
        // which forces the use of legacy Min/Max comparators even for HPK containers.
        [TestMethod]
        public void TestLegacyComparatorsUsedWhenLengthAwareComparatorFlagIsFalse()
        {
            // Arrange: Set environment variable to force legacy comparator usage.
            Environment.SetEnvironmentVariable(ConfigurationManager.UseLengthAwareRangeComparator, "false");
            CollectionRoutingMap routingMap = this.GenerateRoutingMap(false, this.GeneratePartitionKeyDefinition(2));


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

            // Also verify the same behavior for a routing map with a single hash key (no HPK).
            CollectionRoutingMap routingMap1 = this.GenerateRoutingMap(false, this.GeneratePartitionKeyDefinition(1));
            IReadOnlyList<PartitionKeyRange> partitionKeyRanges2 = routingMap1.GetOverlappingRanges(inputPkRange);
            Assert.AreEqual(2, partitionKeyRanges2.Count);
            CollectionAssert.AreEquivalent(new[] { "1", "2" }, partitionKeyRanges2.Select(r => r.Id).ToArray());

            Environment.SetEnvironmentVariable(ConfigurationManager.UseLengthAwareRangeComparator, null);
        }

        private CollectionRoutingMap GenerateRoutingMap(bool isFullySpecified, PartitionKeyDefinition partitionKeyDefinition)
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
                partitionKeyDefinition);

            return routingMap;
        }

        private PartitionKeyDefinition GeneratePartitionKeyDefinition(int levels)
        {
            System.Collections.ObjectModel.Collection<string> paths = new System.Collections.ObjectModel.Collection<string>();
            for (int i = 0; i < levels; i++)
            {
                paths.Add($"/{"id" + i}");
            }

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition
            {
                Paths = paths,
                Kind = levels > 1 ? PartitionKind.MultiHash : PartitionKind.Hash
            };
            return partitionKeyDefinition;
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
                string.Empty, null);
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
                string.Empty, null);

            Assert.IsNull(routingMap);

            routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(new PartitionKeyRange{Id = "2", MinInclusive = "", MaxExclusive = "0000000030"}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{Id = "3", MinInclusive = "0000000030", MaxExclusive = "FF"}, (ServiceIdentity)null),
                    },
                string.Empty, null);

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
              string.Empty, null);

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
                    }, string.Empty, null);

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
                    null, null);

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
                    null, null);

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
                    null, null);

            Assert.IsNull(newRoutingMap);
        }
    }
}