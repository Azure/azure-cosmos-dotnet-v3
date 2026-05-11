//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Benchmarks for <see cref="CollectionRoutingMap.GetOverlappingRanges"/>
    /// to measure the impact of avoiding repeated JsonSerializable.GetValue calls.
    /// See: https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5747
    /// </summary>
    [MemoryDiagnoser]
    public class CollectionRoutingMapBenchmark
    {
        private CollectionRoutingMap routingMap;
        private Range<string> singleRange;
        private Range<string>[] multipleRanges;

        [Params(10, 100, 1000)]
        public int PartitionCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            List<Tuple<PartitionKeyRange, ServiceIdentity>> ranges = new List<Tuple<PartitionKeyRange, ServiceIdentity>>();

            for (int i = 0; i < this.PartitionCount; i++)
            {
                string min = i == 0
                    ? string.Empty
                    : i.ToString("X32");
                string max = i == this.PartitionCount - 1
                    ? "FF"
                    : (i + 1).ToString("X32");

                ranges.Add(Tuple.Create(
                    new PartitionKeyRange
                    {
                        Id = i.ToString(),
                        MinInclusive = min,
                        MaxExclusive = max
                    },
                    (ServiceIdentity)null));
            }

            this.routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                ranges,
                collectionUniqueId: "test-collection",
                useLengthAwareRangeComparer: false);

            // Single range that overlaps with a few partitions in the middle
            int midStart = this.PartitionCount / 4;
            int midEnd = this.PartitionCount / 2;
            this.singleRange = new Range<string>(
                midStart.ToString("X32"),
                midEnd.ToString("X32"),
                isMinInclusive: true,
                isMaxInclusive: false);

            // Multiple non-overlapping ranges
            this.multipleRanges = new Range<string>[]
            {
                new Range<string>(
                    (this.PartitionCount / 8).ToString("X32"),
                    (this.PartitionCount / 4).ToString("X32"),
                    isMinInclusive: true,
                    isMaxInclusive: false),
                new Range<string>(
                    (this.PartitionCount / 2).ToString("X32"),
                    (3 * this.PartitionCount / 4).ToString("X32"),
                    isMinInclusive: true,
                    isMaxInclusive: false),
            };
        }

        [Benchmark]
        public int GetOverlappingRanges_SingleRange()
        {
            return this.routingMap.GetOverlappingRanges(this.singleRange).Count;
        }

        [Benchmark]
        public int GetOverlappingRanges_MultipleRanges()
        {
            return this.routingMap.GetOverlappingRanges(this.multipleRanges).Count;
        }

        [Benchmark]
        public string GetRangeByEffectivePartitionKey()
        {
            string epk = (this.PartitionCount / 2).ToString("X32");
            return this.routingMap.GetRangeByEffectivePartitionKey(epk).Id;
        }
    }
}
