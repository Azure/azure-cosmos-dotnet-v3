// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverBenchmark
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Jobs;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.NativeDriverPoc;

    /// <summary>
    /// Apples-to-apples single-item read benchmark.
    ///
    /// Both paths read the SAME (id, partitionKey) and return raw bytes:
    ///   * V3 SDK uses <c>Container.ReadItemStreamAsync</c> (no typed deserialization).
    ///   * Native driver uses <c>NativeCosmosClient.ReadItemAsync</c> (bytes via cosmos_response_body).
    /// SDK uses ConnectionMode.Gateway to match the native driver's
    /// HTTPS-via-reqwest transport (Phase 6 of PR #4515 is gateway-only).
    /// </summary>
    [MemoryDiagnoser]
    [Config(typeof(Config))]
    public class ReadItemBenchmark
    {
        private sealed class Config : ManualConfig
        {
            public Config()
            {
                // Short job with explicit warmup + 10 measured iterations.
                // BDN's default is generous — we deliberately match the
                // Rust criterion-style "single happy-path measurement" so
                // the report is small and team-shareable.
                this.AddJob(Job.ShortRun
                    .WithWarmupCount(3)
                    .WithIterationCount(10)
                    .WithInvocationCount(16)
                    .WithUnrollFactor(16));
            }
        }

        private BenchmarkSettings settings = null!;
        private CosmosClient sdkClient = null!;
        private Container sdkContainer = null!;
        private PartitionKey sdkPartitionKey;
        private NativeCosmosClient nativeClient = null!;

        [GlobalSetup]
        public void Setup()
        {
            this.settings = BenchmarkSettings.FromEnvironment();
            Console.WriteLine($"[setup] {this.settings.Describe()}");

            // --- V3 SDK path ----------------------------------------------
            this.sdkClient = new CosmosClient(
                this.settings.Endpoint,
                this.settings.Key,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ApplicationName = "cosmos-nativedriver-benchmark-sdk",
                    EnableContentResponseOnWrite = false,
                });

            this.sdkContainer = this.sdkClient
                .GetDatabase(this.settings.Database)
                .GetContainer(this.settings.Container);

            this.sdkPartitionKey = new PartitionKey(this.settings.PartitionKey);

            // --- Native driver path ---------------------------------------
            this.nativeClient = new NativeCosmosClient(
                this.settings.Endpoint,
                this.settings.Key,
                this.settings.Database,
                this.settings.Container,
                this.settings.PartitionKey,
                userAgentSuffix: "cosmos-bench");

            // --- Warm up both: prime DNS, TCP, TLS, gateway routing -------
            for (int i = 0; i < 5; i++)
            {
                this.WarmSdkOnce().GetAwaiter().GetResult();
                this.WarmNativeOnce().GetAwaiter().GetResult();
            }
        }

        private async Task WarmSdkOnce()
        {
            using ResponseMessage rm = await this.sdkContainer.ReadItemStreamAsync(
                this.settings.ItemId, this.sdkPartitionKey).ConfigureAwait(false);
            if ((int)rm.StatusCode != 200)
            {
                throw new InvalidOperationException(
                    $"SDK warm-up read failed: HTTP {(int)rm.StatusCode} for item id='{this.settings.ItemId}' pk='{this.settings.PartitionKey}'");
            }
        }

        private async Task WarmNativeOnce()
        {
            CosmosNativeResponse r = await this.nativeClient
                .ReadItemAsync(this.settings.ItemId).ConfigureAwait(false);
            if (r.HttpStatusCode != 200)
            {
                throw new InvalidOperationException(
                    $"Native warm-up read failed: HTTP {r.HttpStatusCode} for item id='{this.settings.ItemId}' pk='{this.settings.PartitionKey}'");
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.nativeClient?.Dispose();
            this.sdkClient?.Dispose();
        }

        // -----------------------------------------------------------------
        // The two benchmarks. Same item, same transport mode (gateway),
        // both returning raw bytes — apples-to-apples.
        // -----------------------------------------------------------------

        [Benchmark(Baseline = true, Description = "V3 SDK — Container.ReadItemStreamAsync (gateway)")]
        public async Task<long> ReadItem_V3Sdk()
        {
            using ResponseMessage rm = await this.sdkContainer
                .ReadItemStreamAsync(this.settings.ItemId, this.sdkPartitionKey)
                .ConfigureAwait(false);
            // Drain the stream to be honest about bytes-in-hand; the SDK
            // streams the content lazily.
            return rm.Content?.Length ?? 0L;
        }

        [Benchmark(Description = "Native driver — NativeCosmosClient.ReadItemAsync (gateway)")]
        public async Task<long> ReadItem_NativeDriver()
        {
            CosmosNativeResponse r = await this.nativeClient
                .ReadItemAsync(this.settings.ItemId)
                .ConfigureAwait(false);
            return r.Body.LongLength;
        }
    }
}
