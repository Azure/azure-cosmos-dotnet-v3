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


                    }, string.Empty);

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

        ///// This test is designed to validate the normalization logic in the CollectionRoutingMap when requested with an input partition key definition that has multiple levels of partition keys.
        /// The test uses a prebuilt routing map with overlapping ranges and checks the behavior of the overlapping ranges with or without the normalization logic.
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void TestCollectionRoutingMapWithNormalizationLogic(bool isRoutingMapFullySpecified)
        {
            //Setup.
            CollectionRoutingMap routingMap = this.generateRoutingMap(isRoutingMapFullySpecified);
            PartitionKeyDefinition partitionKeyDefinition = this.GeneratePartitionKeyDefinition(2);


            /// Test scenario 1.1: Input EPK is partial and falls on the boundary between two overlapping ranges. Routing map is hybrid of fully specified and partially specified ranges.
            /// Input Min EPK 06AB34CFE4E482236BCACBBF50E234AB matches (significant bytes) with maxEPK of pkrangeid 1 and minEPK of pkrangeid 2.
            Range<string> inputPkRange = new Range<string>(
            "06AB34CFE4E482236BCACBBF50E234AB",
            "06AB34CFE4E482236BCACBBF50E234ABFF",
            true,
            false);

            /// Expected outcome with normalization : Resultant overlapping range will include only partition key range with id 2, since the input minEPK is normalized and searched.
            IReadOnlyList<PartitionKeyRange> partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange, partitionKeyDefinition);
            Assert.AreEqual(1, partitionKeyRanges1.Count);
            Assert.AreEqual("2", partitionKeyRanges1[0].Id);

            /// Expected outcome without normalization (existing behaviour) : Resultant overlapping range will include both partition key ranges with ids 1 and 2.
            IReadOnlyList<PartitionKeyRange> partitionKeyRanges2 = routingMap.GetOverlappingRanges(inputPkRange, null);
            Assert.AreEqual(2, partitionKeyRanges2.Count);
            CollectionAssert.AreEquivalent(new[] { "1", "2" }, partitionKeyRanges2.Select(r => r.Id).ToArray());

            /// Test scenario 1.2: Input EPK falls in boundary and maxEPK also matches the next range's max. The following case is a classic example of a scenario where the normalization logic is beneficial.
            inputPkRange = new Range<string>(
            "0BD3FBE846AF75790CE63F78B1A81631",
            "0BD3FBE846AF75790CE63F78B1A81631FF",
            true,
            false);

            //TODO: Verify this because it is considering 631FF00000000 as grethan 631FF. CHeck.
            partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange, partitionKeyDefinition);
            if (!isRoutingMapFullySpecified)
            {
                Assert.AreEqual(2, partitionKeyRanges1.Count);
                CollectionAssert.AreEquivalent(new[] { "11", "12" }, partitionKeyRanges1.Select(r => r.Id).ToArray());
            }
            else
            {
                Assert.AreEqual(1, partitionKeyRanges1.Count);
                CollectionAssert.AreEquivalent(new[] { "11"}, partitionKeyRanges1.Select(r => r.Id).ToArray());
            }

                /// Expected outcome without normalization (existing incorrect behaviour) : Resultant overlapping range will include both partition key ranges with ids 3 and 11 which is incorrect.
                partitionKeyRanges2 = routingMap.GetOverlappingRanges(inputPkRange);
            Assert.AreEqual(2, partitionKeyRanges2.Count);
            CollectionAssert.AreEquivalent(new[] { "11", "3" }, partitionKeyRanges2.Select(r => r.Id).ToArray());

            /// Test scenario 1.2: Input EPK falls in boundary and maxEPK also matches the next range's max. The following case is a classic example of a scenario where the normalization logic is beneficial.
            inputPkRange = new Range<string>(
            "0BD3FBE846AF75790CE63F78B1A81620",
            "0BD3FBE846AF75790CE63F78B1A81631",
            true,
            false);


            partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange, partitionKeyDefinition);
            Assert.AreEqual(1, partitionKeyRanges1.Count);
            CollectionAssert.AreEquivalent(new[] { "3" }, partitionKeyRanges1.Select(r => r.Id).ToArray());


            /// Test scenario 1.3: Input EPK is partial and scopes two overlapping ranges. Routing map is hybrid of fully specified and partially specified ranges.
            /// Input Min EPK 0DCEB8CE51C6BFE84F4BD9409F69B9BB falls in both pkrangeid 4 and pkrangeid 5.
            inputPkRange = new Range<string>(
            "0DCEB8CE51C6BFE84F4BD9409F69B9BB",
            "0DCEB8CE51C6BFE84F4BD9409F69B9BBFF",
            true,
            false);


            partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange, partitionKeyDefinition);
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


            partitionKeyRanges1 = routingMap.GetOverlappingRanges(inputPkRange, partitionKeyDefinition);
            Assert.AreEqual(1, partitionKeyRanges1.Count);
            Assert.AreEqual("4", partitionKeyRanges1[0].Id);
        }

        private CollectionRoutingMap generateRoutingMap(bool isFullySpecified)
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
                string.Empty);

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
                Kind = PartitionKind.MultiHash
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
                string.Empty);
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
                string.Empty);

            Assert.IsNull(routingMap);

            routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(new PartitionKeyRange{Id = "2", MinInclusive = "", MaxExclusive = "0000000030"}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{Id = "3", MinInclusive = "0000000030", MaxExclusive = "FF"}, (ServiceIdentity)null),
                    },
                string.Empty);

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
              string.Empty);

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
                    }, string.Empty);

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
                    null);

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
                    null);

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
                    null);

            Assert.IsNull(newRoutingMap);
        }
    }
}