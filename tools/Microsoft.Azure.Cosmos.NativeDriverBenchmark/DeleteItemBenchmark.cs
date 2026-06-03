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
    /// Apples-to-apples single-item DELETE benchmark across three drivers.
    ///
    /// Per-iteration state isolation strategy
    /// ---------------------------------------
    /// Delete consumes the target doc, so we need a fresh doc per call.
    /// <see cref="GlobalSetup"/> pre-creates a pool of <see cref="PoolSize"/>
    /// docs per mode using the SDK gateway path (fastest pre-seed).
    /// Each benchmark method pops one doc from its pool per invocation.
    /// <see cref="GlobalCleanup"/> best-effort deletes any unused pool
    /// entries.
    ///
    /// All three drivers operate on the SAME class-local partition key.
    /// </summary>
    [MemoryDiagnoser]
    [Config(typeof(Config))]
    public class DeleteItemBenchmark
    {
        private sealed class Config : ManualConfig
        {
            public Config()
            {
                // Lower InvocationCount than reads (see CreateItemBenchmark
                // for rationale). Pre-seed cost is the dominant Cosmos
                // budget item for this class.
                this.AddJob(Job.ShortRun
                    .WithWarmupCount(3)
                    .WithIterationCount(10)
                    .WithInvocationCount(4)
                    .WithUnrollFactor(4));
            }
        }

        // BDN per-method calls = 52. Plus 5 manual warm-ups. Pool of 256
        // per mode = 768 docs pre-seeded per run (~5,400 RU on a 7 RU/op
        // basis — about 14s of throttle budget on a 400 RU/s container).
        private const int PoolSize = 256;

        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        private BenchmarkSettings settings = null!;
        private string runPartitionKey = null!;
        private PartitionKey sdkPartitionKey;

        private CosmosClient sdkClientGateway = null!;
        private Container sdkContainerGateway = null!;
        private CosmosClient sdkClientDirect = null!;
        private Container sdkContainerDirect = null!;
        private NativeCosmosClient nativeClient = null!;

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
            this.runPartitionKey = $"bench-delete-pk-{runGuid}";
            this.sdkPartitionKey = new PartitionKey(this.runPartitionKey);
            Console.WriteLine($"[DeleteItemBenchmark] pk={this.runPartitionKey} pool/mode={PoolSize}");

            this.idsGateway = GeneratePool("gw");
            this.idsDirect = GeneratePool("dir");
            this.idsNative = GeneratePool("nat");

            string[] GeneratePool(string tag)
            {
                var arr = new string[PoolSize];
                for (int i = 0; i < PoolSize; i++)
                {
                    arr[i] = $"bench-delete-{tag}-{runGuid}-{i:D4}";
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
                userAgentSuffix: "cosmos-bench-delete");

            // Pre-seed: create all the docs we'll delete. Done via the
            // gateway path because that's the simplest fast-path; mode
            // parity doesn't matter for setup.
            Console.WriteLine($"[DeleteItemBenchmark] pre-seeding {3 * PoolSize} docs...");
            PreSeed(this.idsGateway);
            PreSeed(this.idsDirect);
            PreSeed(this.idsNative);
            Console.WriteLine("[DeleteItemBenchmark] pre-seed done.");

            void PreSeed(string[] ids)
            {
                foreach (string id in ids)
                {
                    string body = "{\"id\":\"" + id + "\",\"pk\":\"" + this.runPartitionKey +
                                  "\",\"payload\":\"benchmark\",\"version\":1}";
                    using var ms = new MemoryStream(Utf8.GetBytes(body));
                    using ResponseMessage rm = this.sdkContainerGateway
                        .CreateItemStreamAsync(ms, this.sdkPartitionKey).GetAwaiter().GetResult();
                    if ((int)rm.StatusCode != 201)
                    {
                        throw new InvalidOperationException(
                            $"Pre-seed CREATE for delete pool failed: id={id} http={(int)rm.StatusCode}");
                    }
                }
            }

            // Warm: consumes 5 docs from each pool (still 251 left for the
            // measured 52 invocations per mode — plenty of headroom).
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
            // Best-effort delete any unused pool entries. Iterate from
            // the current counter to the end; entries before the counter
            // have already been deleted by the benchmark itself.
            DeleteRemainder(this.idsGateway, this.counterGateway);
            DeleteRemainder(this.idsDirect, this.counterDirect);
            DeleteRemainder(this.idsNative, this.counterNative);

            void DeleteRemainder(string[] pool, int countConsumed)
            {
                for (int i = countConsumed; i < pool.Length; i++)
                {
                    try
                    {
                        this.sdkContainerGateway
                            .DeleteItemStreamAsync(pool[i], this.sdkPartitionKey)
                            .GetAwaiter().GetResult().Dispose();
                    }
                    catch
                    {
                        // Best-effort.
                    }
                }
            }

            this.nativeClient?.Dispose();
            this.sdkClientDirect?.Dispose();
            this.sdkClientGateway?.Dispose();
        }

        // -----------------------------------------------------------------
        // Warm-up — one Delete per mode, consumes one id from each pool.
        // -----------------------------------------------------------------

        private async Task WarmGatewayOnce()
        {
            string id = this.idsGateway[this.counterGateway++];
            using ResponseMessage rm = await this.sdkContainerGateway
                .DeleteItemStreamAsync(id, this.sdkPartitionKey).ConfigureAwait(false);
            if ((int)rm.StatusCode != 204)
            {
                throw new InvalidOperationException(
                    $"SDK Gateway warm-up delete failed: HTTP {(int)rm.StatusCode}");
            }
        }

        private async Task WarmDirectOnce()
        {
            string id = this.idsDirect[this.counterDirect++];
            using ResponseMessage rm = await this.sdkContainerDirect
                .DeleteItemStreamAsync(id, this.sdkPartitionKey).ConfigureAwait(false);
            if ((int)rm.StatusCode != 204)
            {
                throw new InvalidOperationException(
                    $"SDK Direct warm-up delete failed: HTTP {(int)rm.StatusCode}");
            }
        }

        private async Task WarmNativeOnce()
        {
            string id = this.idsNative[this.counterNative++];
            CosmosNativeResponse r = await this.nativeClient
                .DeleteItemAsync(id).ConfigureAwait(false);
            if (r.HttpStatusCode != 204)
            {
                throw new InvalidOperationException(
                    $"Native warm-up delete failed: HTTP {r.HttpStatusCode}");
            }
        }

        // -----------------------------------------------------------------
        // The three benchmarks.
        // -----------------------------------------------------------------

        [Benchmark(Baseline = true, Description = "V3 SDK — Gateway (DeleteItemStreamAsync)")]
        public async Task<int> DeleteItem_V3Sdk_Gateway()
        {
            string id = this.idsGateway[this.counterGateway++];
            using ResponseMessage rm = await this.sdkContainerGateway
                .DeleteItemStreamAsync(id, this.sdkPartitionKey).ConfigureAwait(false);
            return (int)rm.StatusCode;
        }

        [Benchmark(Description = "V3 SDK — Direct (DeleteItemStreamAsync)")]
        public async Task<int> DeleteItem_V3Sdk_Direct()
        {
            string id = this.idsDirect[this.counterDirect++];
            using ResponseMessage rm = await this.sdkContainerDirect
                .DeleteItemStreamAsync(id, this.sdkPartitionKey).ConfigureAwait(false);
            return (int)rm.StatusCode;
        }

        [Benchmark(Description = "Native driver — Gateway (NativeCosmosClient.DeleteItemAsync)")]
        public async Task<int> DeleteItem_NativeDriver()
        {
            string id = this.idsNative[this.counterNative++];
            CosmosNativeResponse r = await this.nativeClient
                .DeleteItemAsync(id).ConfigureAwait(false);
            return r.HttpStatusCode;
        }
    }
}
