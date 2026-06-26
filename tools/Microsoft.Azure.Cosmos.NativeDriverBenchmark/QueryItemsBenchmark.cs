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
    /// Apples-to-apples single-partition QUERY benchmark across three drivers:
    /// V3 SDK Gateway (baseline), V3 SDK Direct, native driver (Gateway).
    ///
    /// Two query SHAPES are measured per driver (so 6 [Benchmark] methods total):
    ///
    ///   * "SinglePage"  — <c>SELECT * FROM c WHERE c.tag = @tag</c>
    ///                     with MaxItemCount=20. Result set (10 docs) fits
    ///                     in ONE page. This is the cheapest meaningful
    ///                     measurement: one request, one response, no
    ///                     continuation-token chasing. Models the "small
    ///                     filtered lookup" workload.
    ///
    ///   * "Paginated"   — same query, MaxItemCount=2 forces the driver
    ///                     to walk the result set in 5 round-trips. This
    ///                     measures per-page overhead + continuation-token
    ///                     plumbing through the FFI. Models the "page
    ///                     through everything" workload.
    ///
    /// Both shapes are SINGLE-PARTITION: the WHERE clause restricts to one
    /// PK so the V3 SDK and the native driver can use the
    /// ctor-fixed-partition constraint identically. Cross-partition queries
    /// are out of scope for this iteration (native client pins PK at ctor;
    /// the driver itself currently returns only the first part of a
    /// multi-part feed body — header §1700).
    ///
    /// All three drivers return RAW BYTES — V3 uses
    /// <c>GetItemQueryStreamIterator</c> (Stream-based, no typed
    /// deserialization); native uses <c>QueryItemsAsync</c> /
    /// <c>QueryItemsPageAsync</c> returning <c>byte[]</c>. The benchmark
    /// just reports total bytes read so BDN doesn't dead-code-eliminate
    /// the read.
    ///
    /// Per-iteration state isolation
    /// -----------------------------
    /// Reads-only — no per-iteration state mutation. The full document
    /// set is created ONCE in <see cref="GlobalSetup"/> into a fresh
    /// per-run partition key (<c>bench-query-pk-{runGuid}</c>), and
    /// deleted in <see cref="GlobalCleanup"/>. The pre-seeded read item
    /// from <c>COSMOS_ITEM_ID</c> is left untouched.
    /// </summary>
    [MemoryDiagnoser]
    [MinColumn, MaxColumn]
    [Config(typeof(Config))]
    public class QueryItemsBenchmark
    {
        private sealed class Config : ManualConfig
        {
            public Config()
            {
                // Queries hit ~5 RU per call. Match the read-benchmark
                // sample size to keep the run short and the RU spend
                // bounded. Paginated walks multiply that by 5 pages.
                this.AddJob(Job.ShortRun
                    .WithWarmupCount(3)
                    .WithIterationCount(10)
                    .WithInvocationCount(8)
                    .WithUnrollFactor(8));
            }
        }

        // Documents seeded into a single PK. Both shapes use this set.
        private const int SeedCount = 10;

        // Page-size hints. SinglePage > SeedCount so the whole result
        // fits in one page; Paginated is small enough to force exactly
        // ceil(SeedCount / PageSize) pages.
        private const int SinglePageMaxItems = 20;
        private const int PaginatedMaxItems = 2;

        private const string QueryText = "SELECT * FROM c WHERE c.tag = @tag";

        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        private BenchmarkSettings settings = null!;

        private string runPartitionKey = null!;
        private string runTag = null!;
        private PartitionKey sdkPartitionKey;

        private CosmosClient sdkClientGateway = null!;
        private Container sdkContainerGateway = null!;
        private CosmosClient sdkClientDirect = null!;
        private Container sdkContainerDirect = null!;
        private NativeCosmosClient nativeClient = null!;

        // V3 SDK query parameter set is rebuilt per call (cheap value-
        // type), but the parameter list reference for the native driver
        // is shared because it's immutable across calls.
        private (string Name, object? Value)[] nativeParams = null!;

        // Ids we created in setup, for the cleanup loop.
        private string[] seededIds = null!;

        [GlobalSetup]
        public void Setup()
        {
            this.settings = BenchmarkSettings.FromEnvironment();
            string runGuid = Guid.NewGuid().ToString("N").Substring(0, 8);
            this.runPartitionKey = $"bench-query-pk-{runGuid}";
            this.runTag = $"bench-query-{runGuid}";
            this.sdkPartitionKey = new PartitionKey(this.runPartitionKey);
            this.nativeParams = new (string, object?)[] { ("@tag", this.runTag) };

            Console.WriteLine(
                $"[QueryItemsBenchmark] pk={this.runPartitionKey} tag={this.runTag} " +
                $"seed={SeedCount} pageSizes=(single={SinglePageMaxItems}, paginated={PaginatedMaxItems})");

            // --- V3 SDK Gateway (baseline) ----------------------------
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

            // --- V3 SDK Direct ----------------------------------------
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

            // --- Native driver ----------------------------------------
            this.nativeClient = new NativeCosmosClient(
                this.settings.Endpoint, this.settings.Key,
                this.settings.Database, this.settings.Container,
                this.runPartitionKey,
                userAgentSuffix: "cosmos-bench-query");

            // --- Seed N docs into runPartitionKey via SDK Gateway. ----
            // Idempotent: if any seed already exists (re-run with same
            // guid is unlikely but possible), upsert via 409→skip.
            this.seededIds = new string[SeedCount];
            for (int i = 0; i < SeedCount; i++)
            {
                string id = $"{this.runTag}-doc-{i:D2}";
                this.seededIds[i] = id;
                string body =
                    "{\"id\":\"" + id + "\"," +
                    "\"pk\":\"" + this.runPartitionKey + "\"," +
                    "\"tag\":\"" + this.runTag + "\"," +
                    "\"ordinal\":" + i + "}";
                using var ms = new MemoryStream(Utf8.GetBytes(body));
                using ResponseMessage rm = this.sdkContainerGateway
                    .CreateItemStreamAsync(ms, this.sdkPartitionKey)
                    .GetAwaiter().GetResult();
                if ((int)rm.StatusCode != 201 && (int)rm.StatusCode != 409)
                {
                    throw new InvalidOperationException(
                        $"Seed failed for id='{id}': HTTP {(int)rm.StatusCode}");
                }
            }
            Console.WriteLine($"[QueryItemsBenchmark] seeded {SeedCount} docs");

            // --- Warm: TLS, address cache, replica connect, planner ---
            for (int i = 0; i < 5; i++)
            {
                this.WarmSdkGatewaySinglePageOnce().GetAwaiter().GetResult();
                this.WarmSdkDirectSinglePageOnce().GetAwaiter().GetResult();
                this.WarmNativeSinglePageOnce().GetAwaiter().GetResult();
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            // Best-effort delete every seeded id. Use SDK Gateway because
            // we don't care about per-mode parity for cleanup.
            if (this.seededIds != null)
            {
                for (int i = 0; i < this.seededIds.Length; i++)
                {
                    try
                    {
                        this.sdkContainerGateway
                            .DeleteItemStreamAsync(this.seededIds[i], this.sdkPartitionKey)
                            .GetAwaiter().GetResult().Dispose();
                    }
                    catch
                    {
                        // Leaked docs share the bench-query-* prefix.
                    }
                }
            }

            this.nativeClient?.Dispose();
            this.sdkClientDirect?.Dispose();
            this.sdkClientGateway?.Dispose();
        }

        // -----------------------------------------------------------------
        // Warm-up helpers — one call per mode in the single-page shape.
        // The paginated shape is just the single-page shape walked N
        // times, so warming single-page also warms paginated's transport
        // (TLS, address cache, replica connects). The planner itself is
        // re-invoked per call so there's no planner cache to seed.
        // -----------------------------------------------------------------

        private QueryDefinition NewSdkQueryDef() =>
            new QueryDefinition(QueryText).WithParameter("@tag", this.runTag);

        private QueryRequestOptions NewSdkOptions(int maxItemCount) =>
            new QueryRequestOptions
            {
                PartitionKey = this.sdkPartitionKey,
                MaxItemCount = maxItemCount,
            };

        private async Task WarmSdkGatewaySinglePageOnce()
        {
            long total = await SdkSinglePageAsync(
                this.sdkContainerGateway, this.NewSdkQueryDef(),
                this.NewSdkOptions(SinglePageMaxItems)).ConfigureAwait(false);
            if (total <= 0)
            {
                throw new InvalidOperationException(
                    "SDK Gateway warm-up single-page returned 0 bytes — seed missing?");
            }
        }

        private async Task WarmSdkDirectSinglePageOnce()
        {
            long total = await SdkSinglePageAsync(
                this.sdkContainerDirect, this.NewSdkQueryDef(),
                this.NewSdkOptions(SinglePageMaxItems)).ConfigureAwait(false);
            if (total <= 0)
            {
                throw new InvalidOperationException(
                    "SDK Direct warm-up single-page returned 0 bytes — seed missing?");
            }
        }

        private async Task WarmNativeSinglePageOnce()
        {
            CosmosNativeResponse r = await this.nativeClient.QueryItemsPageAsync(
                QueryText,
                continuationToken: null,
                maxItemCount: SinglePageMaxItems,
                parameters: this.nativeParams).ConfigureAwait(false);
            if (r.HttpStatusCode != 200 || r.Body.LongLength <= 0)
            {
                throw new InvalidOperationException(
                    $"Native warm-up single-page failed: HTTP {r.HttpStatusCode} bytes={r.Body.LongLength}");
            }
        }

        // -----------------------------------------------------------------
        // SDK helper — single-page fetch via FeedIterator.
        // Returns total bytes from the FIRST page only (matches the
        // native QueryItemsPageAsync semantics).
        // -----------------------------------------------------------------
        private static async Task<long> SdkSinglePageAsync(
            Container container, QueryDefinition q, QueryRequestOptions opts)
        {
            using FeedIterator iter = container.GetItemQueryStreamIterator(q, null, opts);
            if (!iter.HasMoreResults) return 0L;
            using ResponseMessage rm = await iter.ReadNextAsync().ConfigureAwait(false);
            return rm.Content?.Length ?? 0L;
        }

        // -----------------------------------------------------------------
        // SDK helper — full paginated walk via FeedIterator.
        // Sums bytes across every page until HasMoreResults is false.
        // -----------------------------------------------------------------
        private static async Task<long> SdkPaginatedAsync(
            Container container, QueryDefinition q, QueryRequestOptions opts)
        {
            using FeedIterator iter = container.GetItemQueryStreamIterator(q, null, opts);
            long total = 0L;
            while (iter.HasMoreResults)
            {
                using ResponseMessage rm = await iter.ReadNextAsync().ConfigureAwait(false);
                total += rm.Content?.Length ?? 0L;
            }
            return total;
        }

        // -----------------------------------------------------------------
        // The six benchmarks. SAME query text, SAME PK, SAME parameter —
        // only the transport / driver implementation and the page-size
        // hint differ. Baseline = V3 SDK Gateway, SinglePage shape (the
        // cheapest of the six).
        // -----------------------------------------------------------------

        [Benchmark(Baseline = true,
                   Description = "V3 SDK — Gateway — SinglePage (GetItemQueryStreamIterator MaxItemCount=20)")]
        public Task<long> QuerySinglePage_V3Sdk_Gateway() =>
            SdkSinglePageAsync(this.sdkContainerGateway,
                this.NewSdkQueryDef(), this.NewSdkOptions(SinglePageMaxItems));

        [Benchmark(Description = "V3 SDK — Direct — SinglePage (GetItemQueryStreamIterator MaxItemCount=20)")]
        public Task<long> QuerySinglePage_V3Sdk_Direct() =>
            SdkSinglePageAsync(this.sdkContainerDirect,
                this.NewSdkQueryDef(), this.NewSdkOptions(SinglePageMaxItems));

        [Benchmark(Description = "Native driver — Gateway — SinglePage (NativeCosmosClient.QueryItemsPageAsync MaxItemCount=20)")]
        public async Task<long> QuerySinglePage_NativeDriver()
        {
            CosmosNativeResponse r = await this.nativeClient.QueryItemsPageAsync(
                QueryText,
                continuationToken: null,
                maxItemCount: SinglePageMaxItems,
                parameters: this.nativeParams).ConfigureAwait(false);
            return r.Body.LongLength;
        }

        [Benchmark(Description = "V3 SDK — Gateway — Paginated (GetItemQueryStreamIterator MaxItemCount=2, ~5 pages)")]
        public Task<long> QueryPaginated_V3Sdk_Gateway() =>
            SdkPaginatedAsync(this.sdkContainerGateway,
                this.NewSdkQueryDef(), this.NewSdkOptions(PaginatedMaxItems));

        [Benchmark(Description = "V3 SDK — Direct — Paginated (GetItemQueryStreamIterator MaxItemCount=2, ~5 pages)")]
        public Task<long> QueryPaginated_V3Sdk_Direct() =>
            SdkPaginatedAsync(this.sdkContainerDirect,
                this.NewSdkQueryDef(), this.NewSdkOptions(PaginatedMaxItems));

        [Benchmark(Description = "Native driver — Gateway — Paginated (NativeCosmosClient.QueryItemsAsync MaxItemCount=2, ~5 pages)")]
        public async Task<long> QueryPaginated_NativeDriver()
        {
            long total = 0L;
            await foreach (CosmosNativeResponse page in this.nativeClient.QueryItemsAsync(
                QueryText,
                maxItemCount: PaginatedMaxItems,
                parameters: this.nativeParams).ConfigureAwait(false))
            {
                total += page.Body.LongLength;
            }
            return total;
        }
    }
}
