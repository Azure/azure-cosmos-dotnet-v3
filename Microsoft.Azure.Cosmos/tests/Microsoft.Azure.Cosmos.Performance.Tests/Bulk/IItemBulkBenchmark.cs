// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System.Threading.Tasks;

    public interface IItemBulkBenchmark
    {
        public Task CreateItem();

        public Task UpsertItem();

        public Task ReadItem();

        public Task UpdateItem();

        public Task DeleteItem();
    }
}