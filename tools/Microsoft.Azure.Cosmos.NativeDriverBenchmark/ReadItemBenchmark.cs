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
    /// Three paths read the SAME (id, partitionKey) and return raw bytes:
    ///   * V3 SDK Gateway uses <c>Container.ReadItemStreamAsync</c> with ConnectionMode.Gateway.
    ///     This is the BASELINE — apples-to-apples with the native driver (also gateway today).
    ///   * V3 SDK Direct uses <c>Container.ReadItemStreamAsync</c> with ConnectionMode.Direct.
    ///     Production-typical TCP-direct path; shows the headroom the native driver
    ///     eventually needs to close once it grows a direct-mode transport.
    ///   * Native driver uses <c>NativeCosmosClient.ReadItemAsync</c> (bytes via cosmos_response_body).
    ///     PR #4515 Phase 6 is gateway-only today.
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
        private CosmosClient sdkClientGateway = null!;
        private Container sdkContainerGateway = null!;
        private CosmosClient sdkClientDirect = null!;
        private Container sdkContainerDirect = null!;
        private PartitionKey sdkPartitionKey;
        private NativeCosmosClient nativeClient = null!;

        [GlobalSetup]
        public void Setup()
        {
            this.settings = BenchmarkSettings.FromEnvironment();
            Console.WriteLine($"[setup] {this.settings.Describe()}");

            // --- V3 SDK (Gateway) — apples-to-apples baseline for native -
            this.sdkClientGateway = new CosmosClient(
                this.settings.Endpoint,
                this.settings.Key,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ApplicationName = "cosmos-bench-sdk-gw",
                    EnableContentResponseOnWrite = false,
                });

            this.sdkContainerGateway = this.sdkClientGateway
                .GetDatabase(this.settings.Database)
                .GetContainer(this.settings.Container);

            // --- V3 SDK (Direct) — production-typical TCP-direct path ----
            this.sdkClientDirect = new CosmosClient(
                this.settings.Endpoint,
                this.settings.Key,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct,
                    ApplicationName = "cosmos-bench-sdk-dir",
                    EnableContentResponseOnWrite = false,
                });

            this.sdkContainerDirect = this.sdkClientDirect
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

            // --- Warm up all three: DNS, TCP, TLS, gateway routing, +
            //     (Direct mode) the address cache + TCP-direct connect.
            //     Without the extra Direct warm-ups, the first measured
            //     iteration is ~10x slower than steady state because
            //     Direct mode has to do a one-time gateway round-trip to
            //     learn replica addresses before opening TCP sockets.
            for (int i = 0; i < 5; i++)
            {
                this.WarmSdkGatewayOnce().GetAwaiter().GetResult();
                this.WarmSdkDirectOnce().GetAwaiter().GetResult();
                this.WarmNativeOnce().GetAwaiter().GetResult();
            }
        }

        private async Task WarmSdkGatewayOnce()
        {
            using ResponseMessage rm = await this.sdkContainerGateway.ReadItemStreamAsync(
                this.settings.ItemId, this.sdkPartitionKey).ConfigureAwait(false);
            if ((int)rm.StatusCode != 200)
            {
                throw new InvalidOperationException(
                    $"SDK Gateway warm-up read failed: HTTP {(int)rm.StatusCode} for item id='{this.settings.ItemId}' pk='{this.settings.PartitionKey}'");
            }
        }

        private async Task WarmSdkDirectOnce()
        {
            using ResponseMessage rm = await this.sdkContainerDirect.ReadItemStreamAsync(
                this.settings.ItemId, this.sdkPartitionKey).ConfigureAwait(false);
            if ((int)rm.StatusCode != 200)
            {
                throw new InvalidOperationException(
                    $"SDK Direct warm-up read failed: HTTP {(int)rm.StatusCode} for item id='{this.settings.ItemId}' pk='{this.settings.PartitionKey}'");
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
            this.sdkClientDirect?.Dispose();
            this.sdkClientGateway?.Dispose();
        }

        // -----------------------------------------------------------------
        // The three benchmarks. Same item, same payload — only the
        // transport / driver implementation differs.
        //
        // Baseline = V3 SDK Gateway. That's the apples-to-apples comparison
        // for the native driver (also gateway-only today). The V3 SDK
        // Direct number is supplementary context: it shows what TCP-direct
        // buys you over gateway, which is the headroom the native driver
        // eventually needs to close.
        // -----------------------------------------------------------------

        [Benchmark(Baseline = true, Description = "V3 SDK — Gateway (ReadItemStreamAsync)")]
        public async Task<long> ReadItem_V3Sdk_Gateway()
        {
            using ResponseMessage rm = await this.sdkContainerGateway
                .ReadItemStreamAsync(this.settings.ItemId, this.sdkPartitionKey)
                .ConfigureAwait(false);
            return rm.Content?.Length ?? 0L;
        }

        [Benchmark(Description = "V3 SDK — Direct (ReadItemStreamAsync)")]
        public async Task<long> ReadItem_V3Sdk_Direct()
        {
            using ResponseMessage rm = await this.sdkContainerDirect
                .ReadItemStreamAsync(this.settings.ItemId, this.sdkPartitionKey)
                .ConfigureAwait(false);
            return rm.Content?.Length ?? 0L;
        }

        [Benchmark(Description = "Native driver — Gateway (NativeCosmosClient.ReadItemAsync)")]
        public async Task<long> ReadItem_NativeDriver()
        {
            CosmosNativeResponse r = await this.nativeClient
                .ReadItemAsync(this.settings.ItemId)
                .ConfigureAwait(false);
            return r.Body.LongLength;
        }
    }
}
