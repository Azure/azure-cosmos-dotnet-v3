// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Configs;

    public enum ScenarioType
    {
        Stream = 0,
    }

    [MemoryDiagnoser]
    [Config(typeof(ItemBenchmarkConfig))]
    public class MockedItemBenchmark : IItemBenchmark
    {
        public static readonly IItemBenchmark[] IterParameters = new IItemBenchmark[]
            {
                new MockedItemStreamBenchmark(),
            };

        [Params(ScenarioType.Stream)]
        public ScenarioType Type
        {
            get;
            set;
        }

        private IItemBenchmark CurrentBenchmark
        {
            get
            {
                return MockedItemBenchmark.IterParameters[(int)this.Type];
            }
        }

        public async Task CreateItem()
        {
            await this.CurrentBenchmark.CreateItem();
        }

        public async Task DeleteItemExists()
        {
            await this.CurrentBenchmark.DeleteItemExists();
        }

        public async Task DeleteItemNotExists()
        {
            await this.CurrentBenchmark.DeleteItemNotExists();
        }

        public async Task ReadFeed()
        {
            await this.CurrentBenchmark.ReadFeed();
        }

        [Benchmark]
        public async Task ReadItemExists()
        {
            await this.CurrentBenchmark.ReadItemExists();
        }

        public async Task ReadItemNotExists()
        {
            await this.CurrentBenchmark.ReadItemNotExists();
        }

        public async Task UpdateItem()
        {
            await this.CurrentBenchmark.UpdateItem();
        }

        public async Task UpsertItem()
        {
            await this.CurrentBenchmark.UpsertItem();
        }

        private class ItemBenchmarkConfig : ManualConfig
        {
            public ItemBenchmarkConfig()
            {
                this.Add(StatisticColumn.Q3);
                this.Add(StatisticColumn.P80);
                this.Add(StatisticColumn.P85);
                this.Add(StatisticColumn.P90);
                this.Add(StatisticColumn.P95);
                this.Add(StatisticColumn.P100);
            }
        }
    }
}
