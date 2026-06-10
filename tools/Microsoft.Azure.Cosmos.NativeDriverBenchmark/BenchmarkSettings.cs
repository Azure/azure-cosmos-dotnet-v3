// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverBenchmark
{
    using System;

    /// <summary>
    /// Read-only env-var bundle. Fail-fast at startup with a single
    /// actionable message if anything is missing — beats discovering it
    /// halfway through a 90-second benchmark run.
    /// </summary>
    internal sealed record BenchmarkSettings(
        string Endpoint,
        string Key,
        string Database,
        string Container,
        string ItemId,
        string PartitionKey)
    {
        public static BenchmarkSettings FromEnvironment()
        {
            string Required(string name) =>
                Environment.GetEnvironmentVariable(name)
                    ?? throw new InvalidOperationException(
                        $"Required environment variable {name} is not set. " +
                        "See README.md for the full env-var contract.");

            return new BenchmarkSettings(
                Endpoint:     Required("COSMOS_ENDPOINT"),
                Key:          Required("COSMOS_KEY"),
                Database:     Required("COSMOS_DATABASE"),
                Container:    Required("COSMOS_CONTAINER"),
                ItemId:       Required("COSMOS_ITEM_ID"),
                PartitionKey: Required("COSMOS_PARTITION_KEY"));
        }

        public string Describe() =>
            $"endpoint={this.Endpoint}, db={this.Database}, " +
            $"container={this.Container}, item={this.ItemId}, pk={this.PartitionKey}";
    }
}
