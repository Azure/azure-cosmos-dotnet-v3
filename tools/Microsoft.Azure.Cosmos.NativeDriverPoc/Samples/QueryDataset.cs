// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// One-time hydration helper for <see cref="QuerySample"/>. Uses the
    /// V3 SDK (NOT the native driver) to:
    ///   1. Ensure the run database exists.
    ///   2. Ensure the single-PK container <c>items</c> exists with
    ///      partition path <c>/pk</c>.
    ///   3. Ensure the HPK container <c>items-hpk</c> exists with paths
    ///      <c>/tenant, /region, /user</c> (kind = MultiHash, version 2).
    ///   4. Seed run-tagged documents shaped to exercise every query
    ///      family the matrix tests (single-PK, cross-partition, large
    ///      bodies, HPK).
    ///
    /// The V3 SDK is intentionally scoped to setup/teardown only — every
    /// query the matrix runs goes through the native FFI.
    /// </summary>
    internal sealed class QueryDataset : IAsyncDisposable
    {
        private readonly CosmosClient sdk;
        private readonly string database;
        private readonly string singleContainer;
        private readonly string hpkContainer;
        private readonly string runTag;

        public string RunTag => this.runTag;

        public string SinglePartitionKey { get; }

        public string SingleTag => this.runTag + "-sp";

        public string XpartTag => this.runTag + "-xp";

        public string LargeTag => this.runTag + "-large";

        public IReadOnlyList<string> CrossPartitionKeys { get; }

        public string LargeDocId { get; }

        public string LargeDocPartitionKey { get; }

        public IReadOnlyList<HpkSeed> HpkSeeds { get; }

        public string SingleContainer => this.singleContainer;

        public string HpkContainer => this.hpkContainer;

        public string Database => this.database;

        public QueryDataset(
            string endpoint,
            string masterKey,
            string database,
            string singleContainer,
            string hpkContainer,
            string runTag)
        {
            this.database = database;
            this.singleContainer = singleContainer;
            this.hpkContainer = hpkContainer;
            this.runTag = runTag;

            this.SinglePartitionKey = $"{runTag}-pk-single";
            this.CrossPartitionKeys = new[]
            {
                $"{runTag}-pk-x0",
                $"{runTag}-pk-x1",
                $"{runTag}-pk-x2",
            };
            this.LargeDocId = $"{runTag}-large-doc";
            this.LargeDocPartitionKey = $"{runTag}-pk-large";
            this.HpkSeeds = new[]
            {
                new HpkSeed("tenant-A", "region-east", "user-1", 3),
                new HpkSeed("tenant-A", "region-east", "user-2", 3),
                new HpkSeed("tenant-B", "region-west", "user-3", 3),
            };

            this.sdk = new CosmosClient(
                endpoint,
                masterKey,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    HttpClientFactory = () => new System.Net.Http.HttpClient(
                        new System.Net.Http.HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                        }),
                    LimitToEndpoint = true,
                });
        }

        /// <summary>
        /// Create database + containers if missing, then seed all
        /// run-tagged documents. Idempotent across containers, NOT
        /// across documents (a fresh runTag is generated per invocation
        /// so seeded ids never collide).
        /// </summary>
        public async Task SeedAsync()
        {
            DatabaseResponse dbResp = await this.sdk.CreateDatabaseIfNotExistsAsync(this.database).ConfigureAwait(false);
            Database db = dbResp.Database;

            await db.CreateContainerIfNotExistsAsync(new ContainerProperties
            {
                Id = this.singleContainer,
                PartitionKeyPath = "/pk",
            }).ConfigureAwait(false);

            await db.CreateContainerIfNotExistsAsync(new ContainerProperties
            {
                Id = this.hpkContainer,
                PartitionKeyPaths = new System.Collections.ObjectModel.Collection<string>
                {
                    "/tenant", "/region", "/user",
                },
            }).ConfigureAwait(false);

            Container single = db.GetContainer(this.singleContainer);
            Container hpk = db.GetContainer(this.hpkContainer);

            // --- Single-PK seed: 5 docs in SinglePartitionKey (used by Group A + B + C)
            //     Tagged SingleTag so single-partition queries can scope by tag
            //     and remain unambiguous even when the cross-partition seed
            //     (different tag) is also present in the same container.
            for (int i = 0; i < 5; i++)
            {
                string id = $"{this.runTag}-sp-{i:D2}";
                string category = (i % 2 == 0) ? "alpha" : "beta";
                await single.CreateItemAsync(
                    new
                    {
                        id,
                        pk = this.SinglePartitionKey,
                        tag = this.SingleTag,
                        category,
                        ordinal = i,
                        score = i * 10,
                        tags = new[] { "red", "blue" },
                    },
                    new PartitionKey(this.SinglePartitionKey)).ConfigureAwait(false);
            }

            // --- Cross-partition seed: 5 docs in each of 3 PKs (15 total; used by Group D)
            //     Tagged XpartTag (distinct from SingleTag) so D-tests can
            //     reliably target only the cross-partition seed.
            foreach (string pk in this.CrossPartitionKeys)
            {
                for (int i = 0; i < 5; i++)
                {
                    string id = $"{this.runTag}-xp-{pk.Substring(pk.Length - 3)}-{i:D2}";
                    await single.CreateItemAsync(
                        new
                        {
                            id,
                            pk,
                            tag = this.XpartTag,
                            ordinal = i,
                            score = i,
                        },
                        new PartitionKey(pk)).ConfigureAwait(false);
                }
            }

            // --- Large-body seed: 1 doc with ~600KB padding (used by Group E)
            string padding = new string('x', 600 * 1024);
            await single.CreateItemAsync(
                new
                {
                    id = this.LargeDocId,
                    pk = this.LargeDocPartitionKey,
                    tag = this.LargeTag,
                    payload = padding,
                },
                new PartitionKey(this.LargeDocPartitionKey)).ConfigureAwait(false);

            // --- HPK seed: N docs per HPK (used by Group F)
            //     Reuses the bare runTag for tag — HPK container is isolated
            //     from the single-PK container so no overlap risk.
            foreach (HpkSeed seed in this.HpkSeeds)
            {
                for (int i = 0; i < seed.DocCount; i++)
                {
                    string id = $"{this.runTag}-hpk-{seed.Tenant}-{seed.Region}-{seed.User}-{i:D2}";
                    PartitionKey pk = new PartitionKeyBuilder()
                        .Add(seed.Tenant)
                        .Add(seed.Region)
                        .Add(seed.User)
                        .Build();
                    await hpk.CreateItemAsync(
                        new
                        {
                            id,
                            tenant = seed.Tenant,
                            region = seed.Region,
                            user = seed.User,
                            tag = this.runTag,
                            ordinal = i,
                        },
                        pk).ConfigureAwait(false);
                }
            }
        }

        /// <summary>Best-effort delete all docs seeded for this runTag.</summary>
        public async Task CleanupAsync()
        {
            Database db = this.sdk.GetDatabase(this.database);
            Container single = db.GetContainer(this.singleContainer);
            Container hpk = db.GetContainer(this.hpkContainer);

            await BestEffortDeleteByTagAsync(single, this.SingleTag).ConfigureAwait(false);
            await BestEffortDeleteByTagAsync(single, this.XpartTag).ConfigureAwait(false);
            await BestEffortDeleteByTagAsync(single, this.LargeTag).ConfigureAwait(false);
            await BestEffortDeleteHpkByTagAsync(hpk, this.runTag).ConfigureAwait(false);
        }

        private static async Task BestEffortDeleteByTagAsync(Container c, string tag)
        {
            try
            {
                using FeedIterator<DocRef> it = c.GetItemQueryIterator<DocRef>(
                    new QueryDefinition("SELECT c.id, c.pk FROM c WHERE c.tag = @tag")
                        .WithParameter("@tag", tag));
                while (it.HasMoreResults)
                {
                    FeedResponse<DocRef> page = await it.ReadNextAsync().ConfigureAwait(false);
                    foreach (DocRef r in page)
                    {
                        try
                        {
                            await c.DeleteItemAsync<dynamic>(r.id, new PartitionKey(r.pk)).ConfigureAwait(false);
                        }
                        catch (CosmosException) { /* best-effort */ }
                    }
                }
            }
            catch (CosmosException) { /* container may not exist on first run */ }
        }

        private static async Task BestEffortDeleteHpkByTagAsync(Container c, string tag)
        {
            try
            {
                using FeedIterator<HpkDocRef> it = c.GetItemQueryIterator<HpkDocRef>(
                    new QueryDefinition("SELECT c.id, c.tenant, c.region, c.user FROM c WHERE c.tag = @tag")
                        .WithParameter("@tag", tag));
                while (it.HasMoreResults)
                {
                    FeedResponse<HpkDocRef> page = await it.ReadNextAsync().ConfigureAwait(false);
                    foreach (HpkDocRef r in page)
                    {
                        try
                        {
                            PartitionKey pk = new PartitionKeyBuilder()
                                .Add(r.tenant).Add(r.region).Add(r.user).Build();
                            await c.DeleteItemAsync<dynamic>(r.id, pk).ConfigureAwait(false);
                        }
                        catch (CosmosException) { /* best-effort */ }
                    }
                }
            }
            catch (CosmosException) { /* container may not exist on first run */ }
        }

        public ValueTask DisposeAsync()
        {
            this.sdk.Dispose();
            return ValueTask.CompletedTask;
        }

        internal sealed record HpkSeed(string Tenant, string Region, string User, int DocCount);

        private sealed class DocRef
        {
            public string id { get; set; } = string.Empty;
            public string pk { get; set; } = string.Empty;
        }

        private sealed class HpkDocRef
        {
            public string id { get; set; } = string.Empty;
            public string tenant { get; set; } = string.Empty;
            public string region { get; set; } = string.Empty;
            public string user { get; set; } = string.Empty;
        }
    }
}
