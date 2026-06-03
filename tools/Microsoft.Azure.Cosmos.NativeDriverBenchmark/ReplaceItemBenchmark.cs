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
    /// Apples-to-apples single-item REPLACE benchmark across three drivers.
    ///
    /// Per-iteration state isolation strategy
    /// ---------------------------------------
    /// Replace is the easy CRUD op: same doc, repeatedly overwritten. No
    /// pool needed, no per-call id generation. <see cref="GlobalSetup"/>
    /// creates ONE document per mode (so the three modes don't fight over
    /// etags), and each benchmark method just calls Replace on its own
    /// doc with a bumped <c>version</c> field.
    ///
    /// All three drivers write into the SAME class-local partition key.
    /// </summary>
    [MemoryDiagnoser]
    [Config(typeof(Config))]
    [MinColumn, MaxColumn]
    public class ReplaceItemBenchmark
    {
        private sealed class Config : ManualConfig
        {
            public Config()
            {
                // See CreateItemBenchmark for the rationale on the
                // lower InvocationCount for write benchmarks.
                this.AddJob(Job.ShortRun
                    .WithWarmupCount(3)
                    .WithIterationCount(10)
                    .WithInvocationCount(4)
                    .WithUnrollFactor(4));
            }
        }

        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        private BenchmarkSettings settings = null!;
        private string runPartitionKey = null!;
        private PartitionKey sdkPartitionKey;

        private CosmosClient sdkClientGateway = null!;
        private Container sdkContainerGateway = null!;
        private CosmosClient sdkClientDirect = null!;
        private Container sdkContainerDirect = null!;
        private NativeCosmosClient nativeClient = null!;

        // One target doc per mode — keeps the three benchmarks
        // etag-independent. (If we shared one doc, the second mode's
        // first Replace would see stale state from the first mode.)
        private string targetIdGateway = null!;
        private string targetIdDirect = null!;
        private string targetIdNative = null!;

        // Per-mode version counters. Incremented inside the [Benchmark]
        // method so the body is unique each call (avoids a no-op trip
        // through any server-side de-dup that might exist).
        private int versionGateway;
        private int versionDirect;
        private int versionNative;

        [GlobalSetup]
        public void Setup()
        {
            this.settings = BenchmarkSettings.FromEnvironment();
            string runGuid = Guid.NewGuid().ToString("N").Substring(0, 8);
            this.runPartitionKey = $"bench-replace-pk-{runGuid}";
            this.sdkPartitionKey = new PartitionKey(this.runPartitionKey);
            this.targetIdGateway = $"bench-replace-gw-{runGuid}";
            this.targetIdDirect = $"bench-replace-dir-{runGuid}";
            this.targetIdNative = $"bench-replace-nat-{runGuid}";
            Console.WriteLine(
                $"[ReplaceItemBenchmark] pk={this.runPartitionKey} " +
                $"targets=[{this.targetIdGateway},{this.targetIdDirect},{this.targetIdNative}]");

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
                userAgentSuffix: "cosmos-bench-replace");

            // Pre-create the three target docs via the gateway path.
            // Any error here is fatal — we cannot benchmark Replace if
            // the target doesn't exist.
            CreateTarget(this.targetIdGateway);
            CreateTarget(this.targetIdDirect);
            CreateTarget(this.targetIdNative);

            void CreateTarget(string id)
            {
                string body = "{\"id\":\"" + id + "\",\"pk\":\"" + this.runPartitionKey +
                              "\",\"payload\":\"benchmark\",\"version\":0}";
                using var ms = new MemoryStream(Utf8.GetBytes(body));
                using ResponseMessage rm = this.sdkContainerGateway
                    .CreateItemStreamAsync(ms, this.sdkPartitionKey).GetAwaiter().GetResult();
                if ((int)rm.StatusCode != 201)
                {
                    throw new InvalidOperationException(
                        $"Pre-seed CREATE for replace target failed: id={id} http={(int)rm.StatusCode}");
                }
            }

            // Warm each mode — first call on each path is ~10x slow.
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
            DeleteOne(this.targetIdGateway);
            DeleteOne(this.targetIdDirect);
            DeleteOne(this.targetIdNative);

            void DeleteOne(string id)
            {
                try
                {
                    this.sdkContainerGateway
                        .DeleteItemStreamAsync(id, this.sdkPartitionKey)
                        .GetAwaiter().GetResult().Dispose();
                }
                catch
                {
                    // Best-effort.
                }
            }

            this.nativeClient?.Dispose();
            this.sdkClientDirect?.Dispose();
            this.sdkClientGateway?.Dispose();
        }

        private string BuildBody(string id, int version) =>
            "{\"id\":\"" + id + "\",\"pk\":\"" + this.runPartitionKey +
            "\",\"payload\":\"benchmark\",\"version\":" + version.ToString() + "}";

        // -----------------------------------------------------------------
        // Warm-up — one Replace per mode, bumping the version counter.
        // -----------------------------------------------------------------

        private async Task WarmGatewayOnce()
        {
            int v = ++this.versionGateway;
            using var ms = new MemoryStream(Utf8.GetBytes(this.BuildBody(this.targetIdGateway, v)));
            using ResponseMessage rm = await this.sdkContainerGateway
                .ReplaceItemStreamAsync(ms, this.targetIdGateway, this.sdkPartitionKey)
                .ConfigureAwait(false);
            if ((int)rm.StatusCode != 200)
            {
                throw new InvalidOperationException(
                    $"SDK Gateway warm-up replace failed: HTTP {(int)rm.StatusCode}");
            }
        }

        private async Task WarmDirectOnce()
        {
            int v = ++this.versionDirect;
            using var ms = new MemoryStream(Utf8.GetBytes(this.BuildBody(this.targetIdDirect, v)));
            using ResponseMessage rm = await this.sdkContainerDirect
                .ReplaceItemStreamAsync(ms, this.targetIdDirect, this.sdkPartitionKey)
                .ConfigureAwait(false);
            if ((int)rm.StatusCode != 200)
            {
                throw new InvalidOperationException(
                    $"SDK Direct warm-up replace failed: HTTP {(int)rm.StatusCode}");
            }
        }

        private async Task WarmNativeOnce()
        {
            int v = ++this.versionNative;
            CosmosNativeResponse r = await this.nativeClient
                .ReplaceItemAsync(this.targetIdNative, this.BuildBody(this.targetIdNative, v))
                .ConfigureAwait(false);
            if (r.HttpStatusCode != 200)
            {
                throw new InvalidOperationException(
                    $"Native warm-up replace failed: HTTP {r.HttpStatusCode}");
            }
        }

        // -----------------------------------------------------------------
        // The three benchmarks.
        // -----------------------------------------------------------------

        [Benchmark(Baseline = true, Description = "V3 SDK — Gateway (ReplaceItemStreamAsync)")]
        public async Task<long> ReplaceItem_V3Sdk_Gateway()
        {
            int v = ++this.versionGateway;
            using var ms = new MemoryStream(Utf8.GetBytes(this.BuildBody(this.targetIdGateway, v)));
            using ResponseMessage rm = await this.sdkContainerGateway
                .ReplaceItemStreamAsync(ms, this.targetIdGateway, this.sdkPartitionKey)
                .ConfigureAwait(false);
            return rm.Content?.Length ?? 0L;
        }

        [Benchmark(Description = "V3 SDK — Direct (ReplaceItemStreamAsync)")]
        public async Task<long> ReplaceItem_V3Sdk_Direct()
        {
            int v = ++this.versionDirect;
            using var ms = new MemoryStream(Utf8.GetBytes(this.BuildBody(this.targetIdDirect, v)));
            using ResponseMessage rm = await this.sdkContainerDirect
                .ReplaceItemStreamAsync(ms, this.targetIdDirect, this.sdkPartitionKey)
                .ConfigureAwait(false);
            return rm.Content?.Length ?? 0L;
        }

        [Benchmark(Description = "Native driver — Gateway (NativeCosmosClient.ReplaceItemAsync)")]
        public async Task<long> ReplaceItem_NativeDriver()
        {
            int v = ++this.versionNative;
            CosmosNativeResponse r = await this.nativeClient
                .ReplaceItemAsync(this.targetIdNative, this.BuildBody(this.targetIdNative, v))
                .ConfigureAwait(false);
            return r.Body.LongLength;
        }
    }
}
