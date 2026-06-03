// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverBenchmark
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Jobs;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.NativeDriverPoc;

    /// <summary>
    /// Apples-to-apples single-item CREATE benchmark across three drivers:
    /// V3 SDK Gateway (baseline), V3 SDK Direct, native driver (Gateway).
    ///
    /// Per-iteration state isolation strategy
    /// ---------------------------------------
    ///   * <see cref="GlobalSetup"/> pre-generates a pool of unique ids per
    ///     mode (so the timed window does NOT include id generation).
    ///   * Each benchmark method pops one id from its pool per invocation.
    ///   * <see cref="GlobalCleanup"/> best-effort deletes every id that
    ///     was consumed by any of the three counters, so no documents
    ///     leak between runs.
    ///
    /// All three drivers write into the SAME class-local partition key
    /// (<c>bench-create-pk-{runGuid}</c>) — this matches the native driver
    /// constraint (pk is fixed at ctor time) and keeps the pre-seeded
    /// read item (<c>COSMOS_ITEM_ID</c>) untouched.
    /// </summary>
    [MemoryDiagnoser]
    [Config(typeof(Config))]
    public class CreateItemBenchmark
    {
        private sealed class Config : ManualConfig
        {
            public Config()
            {
                // Writes cost RU. Halve the read-benchmark sample size
                // (InvocationCount=4 vs 16) to keep the full-matrix run
                // under ~3 minutes / ~7,000 RU on a 400 RU/s container.
                this.AddJob(Job.ShortRun
                    .WithWarmupCount(3)
                    .WithIterationCount(10)
                    .WithInvocationCount(4)
                    .WithUnrollFactor(4));
            }
        }

        // BDN per-method calls = (warmup 3 + measured 10) * InvocationCount 4 = 52.
        // We also do 5 manual warm-ups in GlobalSetup. Pool of 256 leaves
        // a comfortable safety margin for any future knob changes.
        private const int PoolSize = 256;

        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        private BenchmarkSettings settings = null!;

        // All Creates in this class go to one PK. This is intentional:
        //   * matches the native client's ctor-fixed-pk constraint
        //   * keeps the read-benchmark's pre-seeded doc unaffected
        //   * isolates this benchmark from any other CRUD class running
        //     in the same suite
        private string runPartitionKey = null!;
        private PartitionKey sdkPartitionKey;

        private CosmosClient sdkClientGateway = null!;
        private Container sdkContainerGateway = null!;
        private CosmosClient sdkClientDirect = null!;
        private Container sdkContainerDirect = null!;
        private NativeCosmosClient nativeClient = null!;

        // Pre-generated id pools. Counter-indexed, single-threaded by BDN.
        private string[] idsGateway = null!;
        private string[] idsDirect = null!;
        private string[] idsNative = null!;
        private int counterGateway;
        private int counterDirect;
        private int counterNative;

        [GlobalSetup]
        public void Setup()
        {
            this.settings = BenchmarkSettings.FromEnvironment();
            string runGuid = Guid.NewGuid().ToString("N").Substring(0, 8);
            this.runPartitionKey = $"bench-create-pk-{runGuid}";
            this.sdkPartitionKey = new PartitionKey(this.runPartitionKey);
            Console.WriteLine($"[CreateItemBenchmark] pk={this.runPartitionKey} pool/mode={PoolSize}");

            this.idsGateway = GeneratePool("gw");
            this.idsDirect = GeneratePool("dir");
            this.idsNative = GeneratePool("nat");

            string[] GeneratePool(string tag)
            {
                var arr = new string[PoolSize];
                for (int i = 0; i < PoolSize; i++)
                {
                    arr[i] = $"bench-create-{tag}-{runGuid}-{i:D4}";
                }
                return arr;
            }

            this.sdkClientGateway = new CosmosClient(
                this.settings.Endpoint, this.settings.Key,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ApplicationName = "cosmos-bench-sdk-gw",
                    EnableContentResponseOnWrite = false,
                });
            this.sdkContainerGateway = this.sdkClientGateway
                .GetDatabase(this.settings.Database)
                .GetContainer(this.settings.Container);

            this.sdkClientDirect = new CosmosClient(
                this.settings.Endpoint, this.settings.Key,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct,
                    ApplicationName = "cosmos-bench-sdk-dir",
                    EnableContentResponseOnWrite = false,
                });
            this.sdkContainerDirect = this.sdkClientDirect
                .GetDatabase(this.settings.Database)
                .GetContainer(this.settings.Container);

            this.nativeClient = new NativeCosmosClient(
                this.settings.Endpoint, this.settings.Key,
                this.settings.Database, this.settings.Container,
                this.runPartitionKey,
                userAgentSuffix: "cosmos-bench-create");

            // Warm: DNS, TCP, TLS, gateway routing, Direct's address cache.
            // First call on each mode otherwise burns ~10x steady-state time.
            for (int i = 0; i < 5; i++)
            {
                this.WarmGatewayOnce().GetAwaiter().GetResult();
                this.WarmDirectOnce().GetAwaiter().GetResult();
                this.WarmNativeOnce().GetAwaiter().GetResult();
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            // Best-effort delete every id we consumed (warm + measured).
            // Use the SDK gateway client because it's the most permissive
            // path and we don't care about per-mode parity for cleanup.
            DeleteRange(this.idsGateway, this.counterGateway);
            DeleteRange(this.idsDirect, this.counterDirect);
            DeleteRange(this.idsNative, this.counterNative);

            void DeleteRange(string[] pool, int countConsumed)
            {
                for (int i = 0; i < countConsumed; i++)
                {
                    try
                    {
                        this.sdkContainerGateway
                            .DeleteItemStreamAsync(pool[i], this.sdkPartitionKey)
                            .GetAwaiter().GetResult().Dispose();
                    }
                    catch
                    {
                        // Swallow — best-effort. Leaked docs share the
                        // bench-create-* prefix and are easy to scrub
                        // out-of-band if needed.
                    }
                }
            }

            this.nativeClient?.Dispose();
            this.sdkClientDirect?.Dispose();
            this.sdkClientGateway?.Dispose();
        }

        private string BuildBody(string id) =>
            "{\"id\":\"" + id + "\",\"pk\":\"" + this.runPartitionKey +
            "\",\"payload\":\"benchmark\",\"version\":1}";

        // -----------------------------------------------------------------
        // Warm-up — one call per mode, consumes one id from each pool.
        // -----------------------------------------------------------------

        private async Task WarmGatewayOnce()
        {
            string id = this.idsGateway[this.counterGateway++];
            using var ms = new MemoryStream(Utf8.GetBytes(this.BuildBody(id)));
            using ResponseMessage rm = await this.sdkContainerGateway
                .CreateItemStreamAsync(ms, this.sdkPartitionKey).ConfigureAwait(false);
            if ((int)rm.StatusCode != 201)
            {
                throw new InvalidOperationException(
                    $"SDK Gateway warm-up create failed: HTTP {(int)rm.StatusCode}");
            }
        }

        private async Task WarmDirectOnce()
        {
            string id = this.idsDirect[this.counterDirect++];
            using var ms = new MemoryStream(Utf8.GetBytes(this.BuildBody(id)));
            using ResponseMessage rm = await this.sdkContainerDirect
                .CreateItemStreamAsync(ms, this.sdkPartitionKey).ConfigureAwait(false);
            if ((int)rm.StatusCode != 201)
            {
                throw new InvalidOperationException(
                    $"SDK Direct warm-up create failed: HTTP {(int)rm.StatusCode}");
            }
        }

        private async Task WarmNativeOnce()
        {
            string id = this.idsNative[this.counterNative++];
            CosmosNativeResponse r = await this.nativeClient
                .CreateItemAsync(id, this.BuildBody(id)).ConfigureAwait(false);
            if (r.HttpStatusCode != 201)
            {
                throw new InvalidOperationException(
                    $"Native warm-up create failed: HTTP {r.HttpStatusCode}");
            }
        }

        // -----------------------------------------------------------------
        // The three benchmarks. Same payload shape, same PK — only the
        // transport / driver implementation differs. Baseline = Gateway.
        // -----------------------------------------------------------------

        [Benchmark(Baseline = true, Description = "V3 SDK — Gateway (CreateItemStreamAsync)")]
        public async Task<long> CreateItem_V3Sdk_Gateway()
        {
            string id = this.idsGateway[this.counterGateway++];
            using var ms = new MemoryStream(Utf8.GetBytes(this.BuildBody(id)));
            using ResponseMessage rm = await this.sdkContainerGateway
                .CreateItemStreamAsync(ms, this.sdkPartitionKey).ConfigureAwait(false);
            return rm.Content?.Length ?? 0L;
        }

        [Benchmark(Description = "V3 SDK — Direct (CreateItemStreamAsync)")]
        public async Task<long> CreateItem_V3Sdk_Direct()
        {
            string id = this.idsDirect[this.counterDirect++];
            using var ms = new MemoryStream(Utf8.GetBytes(this.BuildBody(id)));
            using ResponseMessage rm = await this.sdkContainerDirect
                .CreateItemStreamAsync(ms, this.sdkPartitionKey).ConfigureAwait(false);
            return rm.Content?.Length ?? 0L;
        }

        [Benchmark(Description = "Native driver — Gateway (NativeCosmosClient.CreateItemAsync)")]
        public async Task<long> CreateItem_NativeDriver()
        {
            string id = this.idsNative[this.counterNative++];
            CosmosNativeResponse r = await this.nativeClient
                .CreateItemAsync(id, this.BuildBody(id)).ConfigureAwait(false);
            return r.Body.LongLength;
        }
    }
}
