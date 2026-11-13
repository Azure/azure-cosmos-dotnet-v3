//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if PREVIEW
namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Documents.Routing;

    [MemoryDiagnoser]
    public class FeedRangeOwnershipBenchmark
    {
        private CosmosClient client;
        private Container container;
        private IReadOnlyList<FeedRange> ranges;
        private PartitionKey partitionKey;
        private FeedRange logicalRange;

        [Params(100, 4000)]
        public int RangeCount { get; set; }

        [Params("First", "Middle", "Last", "Missing")]
        public string Position { get; set; }

        [GlobalSetup]
        public async Task Setup()
        {
            this.client = MockDocumentClient.CreateMockCosmosClient();
            this.container = this.client.GetContainer("myDB", "myColl");
            this.partitionKey = new PartitionKey("feed-range-ownership");
            this.logicalRange = FeedRange.FromPartitionKey(this.partitionKey);
            Documents.PartitionKeyDefinition definition = await ((ContainerInternal)this.container).GetPartitionKeyDefinitionAsync(default);
            string epk = this.partitionKey.InternalKey.GetEffectivePartitionKeyString(definition);
            int ownerIndex = this.Position switch
            {
                "First" => 0,
                "Middle" => this.RangeCount / 2,
                _ => this.RangeCount - 1,
            };
            List<FeedRange> ranges = new List<FeedRange>(this.RangeCount);
            for (int i = 0; i < this.RangeCount; i++)
            {
                string min = i == 0 ? "" : (i <= ownerIndex ? i.ToString("X32") : epk + i.ToString("X8"));
                string max = i == this.RangeCount - 1 ? "FF" : (i < ownerIndex ? (i + 1).ToString("X32") : epk + (i + 1).ToString("X8"));
                ranges.Add(new FeedRangeEpk(new Range<string>(min, max, true, false)));
            }

            if (this.Position == "Missing")
            {
                ranges[ownerIndex] = new FeedRangeEpk(new Range<string>(epk + "00000001", "FF", true, false));
            }

            this.ranges = ranges;
            FeedRange expected = this.Position == "Missing" ? null : ranges[ownerIndex];
            if (!object.ReferenceEquals(expected, await this.container.TryGetFeedRangeForPartitionKeyAsync(this.ranges, this.partitionKey)) ||
                !object.ReferenceEquals(expected, await this.ContainmentScan()))
            {
                throw new InvalidOperationException("Benchmark ranges do not have the expected owner.");
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.client.Dispose();
        }

        [Benchmark(Baseline = true)]
        public async Task<FeedRange> ContainmentScan()
        {
            foreach (FeedRange range in this.ranges)
            {
                if (await this.container.IsFeedRangePartOfAsync(range, this.logicalRange))
                {
                    return range;
                }
            }

            return null;
        }

        [Benchmark]
        public Task<FeedRange> ListLookup()
        {
            return this.container.TryGetFeedRangeForPartitionKeyAsync(this.ranges, this.partitionKey);
        }
    }
}
#endif
