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
        [DataRow("10FCC300A87A68CEAE01B332126DF841", "10FCC300A87A68CEAE01B332126DF841")]
        [DataRow("10FCC300A87A68CEAE01B332126DF841", "10FCC300A87A68CEAE01B332126DF841FF")]
        [DataRow("10FCC300A87A68CEAE01B332126DF84100", "10FCC300A87A68CEAE01B332126DF841FF")]
        [DataRow("10FCC300A87A68CEAE01B332126DF84100000000000000000000000000000000", "10FCC300A87A68CEAE01B332126DF841FF")]
        // [DataRow("10FCC300A87A68CEAE01B332126DF84100", "10FCC300A87A68CEAE01B332126DF841FF")]
        public void TestIncompleteRoutingMap1(string min, string max)
        {
            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(new PartitionKeyRange{ Id = "0", MinInclusive = "", MaxExclusive = "03559A67F2724111B5E565DFA8711A00"}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{ Id = "1", MinInclusive = "03559A67F2724111B5E565DFA8711A00", MaxExclusive = "06AB34CFE4E482236BCACBBF50E2340014FFFD1448906439F4C00C0C9E4E5761"}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{ Id = "2", MinInclusive = "06AB34CFE4E482236BCACBBF50E2340014FFFD1448906439F4C00C0C9E4E5761", MaxExclusive = "0BD3FBE846AF75790CE63F78B1A81620"}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{ Id = "3", MinInclusive = "0BD3FBE846AF75790CE63F78B1A81620", MaxExclusive = "10FCC300A87A68CEAE01B332126DF84100000000000000000000000000000000"}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{ Id = "4", MinInclusive = "10FCC300A87A68CEAE01B332126DF84100000000000000000000000000000000", MaxExclusive = "110898EF2A4F576938F30DA70F59630700000000000000000000000000000000"}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{ Id = "5", MinInclusive = "110898EF2A4F576938F30DA70F59630700000000000000000000000000000000", MaxExclusive = "1DB2341C52E0BD28FE906DE2C038892F00000000000000000000000000000000"}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{ Id = "6", MinInclusive = "1DB2341C52E0BD28FE906DE2C038892F00000000000000000000000000000000", MaxExclusive = "2263C2179D569B36624FE3BDADF48A9D"}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{ Id = "7", MinInclusive = "2263C2179D569B36624FE3BDADF48A9D", MaxExclusive = "27155012E7CC7943C60F59989BB08C0C00000000000000000000000000000000"}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{ Id = "8", MinInclusive = "27155012E7CC7943C60F59989BB08C0C00000000000000000000000000000000", MaxExclusive = "338AA80973E63CA1E307ACCC4DD84605"}, (ServiceIdentity)null),
                        Tuple.Create(new PartitionKeyRange{ Id = "9", MinInclusive = "338AA80973E63CA1E307ACCC4DD84605", MaxExclusive = "FF"}, (ServiceIdentity)null),
                    },
                string.Empty);

            IReadOnlyList<PartitionKeyRange> result = routingMap.GetOverlappingRanges(new Documents.Routing.Range<string>(min, max, isMaxInclusive: true, isMinInclusive: true));

            foreach (PartitionKeyRange pkRange in result)
            {
                Console.WriteLine(pkRange.ToString());
            }
        }

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