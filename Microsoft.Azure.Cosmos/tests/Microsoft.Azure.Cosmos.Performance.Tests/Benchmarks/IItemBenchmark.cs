// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System.Threading.Tasks;

    public interface IItemBenchmark
    {
        public Task CreateItem();

        public Task UpsertItem();

        public Task ReadItemNotExists();

        public Task ReadItemExists();

        public Task UpdateItem();

        public Task DeleteItemExists();

        public Task DeleteItemNotExists();

        public Task ReadFeed();
    }
}