// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Per-request partition-key query scenarios.
    ///
    /// Design note: real-world Cosmos clients DO NOT pin a partition key at
    /// construction — the partition key is supplied per operation (exactly as
    /// V3/V4 do with a <c>PartitionKey</c> argument). This sample uses
    /// <see cref="NativeCosmosClient.CreateCrossPartition"/> to build clients
    /// with NO pinned key, then passes the key (or none) on each call via
    /// <see cref="NativeCosmosClient.QueryPageAsync"/> /
    /// <see cref="NativeCosmosClient.QueryPagesAsync"/>.
    ///
    /// Four scenarios, two per key shape:
    ///
    ///   Single (non-hierarchical) partition key — container PK path /pk:
    ///     S1 SCOPED        query scoped to ONE logical PK -> one physical
    ///                      partition; expect only that partition's docs (5)
    ///     S2 CROSS         no PK -> fan out across all physical partitions;
    ///                      expect every matching doc (15 across 3 PKs)
    ///
    ///   Hierarchical partition key — container PK paths /tenant,/region,/user:
    ///     H1 SCOPED        query scoped to ONE full HPK -> one physical
    ///                      partition; expect only that partition's docs (3)
    ///     H2 CROSS         no PK -> fan out; expect all docs (9 across 3 keys)
    ///
    ///   Partition key expressed in the WHERE CLAUSE (not WithPartitionKey):
    ///     W1 single PK     SELECT * ... WHERE c.pk = @pk, NO WithPartitionKey;
    ///                      predicate scopes the result to 5 even though the
    ///                      driver fans out (server filters each partition).
    ///     W2 full HPK      WHERE c.tenant/region/user = ..., NO WithPartitionKey;
    ///                      predicate scopes the result to 3.
    ///   W1/W2 are the control for S1/H1: if W1/W2 PASS while S1/H1 FAIL, the
    ///   routing defect is isolated to the WithPartitionKey request-option path
    ///   (the query engine still applies the predicate correctly).
    ///
    /// The dataset is seeded by <see cref="QueryDataset"/>:
    ///   * single-PK container: 15 docs tagged XpartTag, 5 in each of 3 PKs
    ///   * HPK container: 9 docs tagged runTag, 3 in each of 3 full keys
    ///
    /// The SCOPED vs CROSS pair is what makes the partition-key routing
    /// observable: S1 should return strictly fewer docs (5) than S2 (15), and
    /// H1 (3) strictly fewer than H2 (9). If a SCOPED query returns the same
    /// count as its CROSS sibling, the driver ignored the per-request key and
    /// fanned out anyway (the known QueryItems routing gap).
    ///
    /// Configuration (env vars; emulator defaults if unset):
    ///   COSMOS_ENDPOINT / COSMOS_KEY / COSMOS_DATABASE
    ///   COSMOS_CONTAINER / COSMOS_HPK_CONTAINER
    /// </summary>
    internal static class QueryPartitionKeySample
    {
        private const string EmulatorEndpoint = "https://localhost:8081/";
        private const string EmulatorKey =
            "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        private const string DefaultDatabase = "pocdb";
        private const string DefaultContainer = "items";
        private const string DefaultHpkContainer = "items-hpk";

        public static async Task<int> RunAsync()
        {
            string endpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT") ?? EmulatorEndpoint;
            string masterKey = Environment.GetEnvironmentVariable("COSMOS_KEY") ?? EmulatorKey;
            string database = Environment.GetEnvironmentVariable("COSMOS_DATABASE") ?? DefaultDatabase;
            string container = Environment.GetEnvironmentVariable("COSMOS_CONTAINER") ?? DefaultContainer;
            string hpkContainer = Environment.GetEnvironmentVariable("COSMOS_HPK_CONTAINER") ?? DefaultHpkContainer;

            string runTag = $"qpk-{Guid.NewGuid():N}".Substring(0, 14);

            Console.WriteLine("=== Native Driver — Per-request partition-key query scenarios ===");
            Console.WriteLine($"  endpoint     : {endpoint}");
            Console.WriteLine($"  single-cont  : {container}        (PK path /pk)");
            Console.WriteLine($"  hpk-cont     : {hpkContainer}    (PK paths /tenant, /region, /user)");
            Console.WriteLine($"  run tag      : {runTag}");
            Console.WriteLine("  NOTE: clients are constructed with NO pinned PK; the key is");
            Console.WriteLine("        supplied per request (real-world shape).");
            Console.WriteLine();

            await using var dataset = new QueryDataset(endpoint, masterKey, database, container, hpkContainer, runTag);

            Console.WriteLine("[hydrate] seeding dataset via V3 SDK (one-time setup) ...");
            try
            {
                await dataset.SeedAsync().ConfigureAwait(false);
                Console.WriteLine("[hydrate] done");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[hydrate] FAILED — {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine("Aborting; cannot run tests without seeded data.");
                return 2;
            }

            string scopedSingleKey = dataset.CrossPartitionKeys[0];   // one of the 3 PKs
            var results = new List<TestResult>();
            try
            {
                // Unpinned clients — partition key is passed per request.
                using var single = NativeCosmosClient.CreateCrossPartition(
                    endpoint, masterKey, database, container,
                    userAgentSuffix: "cosmos-querypk-single");
                using var hpk = NativeCosmosClient.CreateCrossPartition(
                    endpoint, masterKey, database, hpkContainer,
                    userAgentSuffix: "cosmos-querypk-hpk");

                Console.WriteLine();
                Console.WriteLine("=== Single (non-hierarchical) partition key ===");
                results.Add(await WithTimeout("S1 single PK scoped (1 partition)",
                    S1_SingleScoped(single, dataset, scopedSingleKey)).ConfigureAwait(false));
                results.Add(await WithTimeout("S2 single PK cross-partition",
                    S2_SingleCrossPartition(single, dataset)).ConfigureAwait(false));
                results.Add(await WithTimeout("W1 single PK via WHERE clause",
                    W1_SingleWhereClause(single, dataset, scopedSingleKey)).ConfigureAwait(false));

                Console.WriteLine();
                Console.WriteLine("=== Hierarchical partition key ===");
                results.Add(await WithTimeout("H1 HPK scoped (1 partition)",
                    H1_HpkScoped(hpk, dataset)).ConfigureAwait(false));
                results.Add(await WithTimeout("H2 HPK cross-partition",
                    H2_HpkCrossPartition(hpk, dataset)).ConfigureAwait(false));
                results.Add(await WithTimeout("W2 HPK via WHERE clause",
                    W2_HpkWhereClause(hpk, dataset)).ConfigureAwait(false));
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("[cleanup] deleting seeded docs (best-effort, via V3 SDK)");
                try { await dataset.CleanupAsync().ConfigureAwait(false); }
                catch (Exception ex) { Console.WriteLine($"[cleanup] {ex.GetType().Name}: {ex.Message}"); }
            }

            Console.WriteLine();
            Console.WriteLine("=== Summary ===");
            int passes = results.Count(r => r.Status == TestStatus.Pass);
            int fails = results.Count(r => r.Status == TestStatus.Fail);
            foreach (TestResult r in results)
            {
                string marker = r.Status == TestStatus.Pass ? "PASS" : (r.Status == TestStatus.Skip ? "SKIP" : "FAIL");
                Console.WriteLine($"  [{marker}] {r.Name,-32} {r.Detail}");
            }
            Console.WriteLine();
            Console.WriteLine($"  total = {results.Count}  pass = {passes}  fail = {fails}");
            Console.WriteLine();
            Console.WriteLine("  Interpretation:");
            Console.WriteLine("   * A SCOPED query (S1/H1, via WithPartitionKey) returning the same");
            Console.WriteLine("     count as its CROSS sibling (S2/H2) means the per-request");
            Console.WriteLine("     partition key was ignored (driver fanned out anyway).");
            Console.WriteLine("   * W1/W2 express the same scoping in the WHERE clause instead.");
            Console.WriteLine("     If W1/W2 PASS while S1/H1 FAIL, the defect is isolated to the");
            Console.WriteLine("     WithPartitionKey request-option routing path, not query");
            Console.WriteLine("     filtering — the engine still applies the predicate correctly.");
            Console.WriteLine("   * OBSERVED (this driver build): W1/W2 do NOT pass — a partition-key");
            Console.WriteLine("     equality predicate in the WHERE clause makes the driver PANIC");
            Console.WriteLine("     (planner.rs:258 'topology provider must return ranges that");
            Console.WriteLine("     overlap the query plan EPK range') and never post a completion,");
            Console.WriteLine("     so the call hangs (surfaced here as a 30s TIMEOUT fail). That is");
            Console.WriteLine("     a distinct, more severe defect than the silent fan-out above.");
            return fails == 0 ? 0 : 1;
        }

        // S1 — single PK, scoped to ONE logical partition. The XpartTag dataset
        // has the SAME tag across 3 PKs (5 docs each); scoping to one PK must
        // return only that partition's 5 docs, NOT all 15.
        private static async Task<TestResult> S1_SingleScoped(
            NativeCosmosClient c, QueryDataset d, string scopedKey)
        {
            const string name = "S1 single PK scoped (1 partition)";
            var parms = new (string, object?)[] { ("@tag", d.XpartTag) };
            try
            {
                int docs = await CountAllPages(
                    c, "SELECT * FROM c WHERE c.tag = @tag",
                    partitionKey: new object[] { scopedKey }, parms).ConfigureAwait(false);

                bool ok = docs == 5;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"pk=[{Short(scopedKey)}] docs={docs}/5 (scoped to one logical partition)");
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        // S2 — single PK, cross-partition fan-out (no PK). Same tag, no key:
        // every physical partition is visited, all 15 docs returned.
        private static async Task<TestResult> S2_SingleCrossPartition(
            NativeCosmosClient c, QueryDataset d)
        {
            const string name = "S2 single PK cross-partition";
            var parms = new (string, object?)[] { ("@tag", d.XpartTag) };
            try
            {
                int docs = await CountAllPages(
                    c, "SELECT * FROM c WHERE c.tag = @tag",
                    partitionKey: null, parms).ConfigureAwait(false);

                bool ok = docs == 15;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"pk=<none> docs={docs}/15 (fan-out across 3 partitions)");
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        // H1 — HPK, scoped to ONE full hierarchical key. 9 docs share the tag
        // across 3 full keys (3 each); scoping to [A,east,user-1] must return 3.
        private static async Task<TestResult> H1_HpkScoped(
            NativeCosmosClient c, QueryDataset d)
        {
            const string name = "H1 HPK scoped (1 partition)";
            var parms = new (string, object?)[] { ("@tag", d.RunTag) };
            try
            {
                int docs = await CountAllPages(
                    c, "SELECT * FROM c WHERE c.tag = @tag",
                    partitionKey: new object[] { "tenant-A", "region-east", "user-1" }, parms).ConfigureAwait(false);

                bool ok = docs == 3;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"pk=[tenant-A/region-east/user-1] docs={docs}/3 (scoped to one logical partition)");
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        // H2 — HPK, cross-partition fan-out (no PK). All 9 docs returned.
        private static async Task<TestResult> H2_HpkCrossPartition(
            NativeCosmosClient c, QueryDataset d)
        {
            const string name = "H2 HPK cross-partition";
            var parms = new (string, object?)[] { ("@tag", d.RunTag) };
            try
            {
                int docs = await CountAllPages(
                    c, "SELECT * FROM c WHERE c.tag = @tag",
                    partitionKey: null, parms).ConfigureAwait(false);

                bool ok = docs == 9;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"pk=<none> docs={docs}/9 (fan-out across 3 keys)");
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        // W1 — single PK expressed IN THE WHERE CLAUSE (c.pk = @pk) instead of
        // via WithPartitionKey. NO per-request partition key is supplied, so the
        // driver fans out — but the server-side predicate filters each physical
        // partition's slice, so the RESULT should still be exactly the 5 docs in
        // that one logical partition. Contrast with S1 (same scoping intent via
        // WithPartitionKey) which returns 15 because the dropped key leaves no
        // predicate to filter on. W1 passing while S1 fails isolates the bug to
        // the WithPartitionKey request-option routing path, not query filtering.
        private static async Task<TestResult> W1_SingleWhereClause(
            NativeCosmosClient c, QueryDataset d, string scopedKey)
        {
            const string name = "W1 single PK via WHERE clause";
            var parms = new (string, object?)[] { ("@pk", scopedKey), ("@tag", d.XpartTag) };
            try
            {
                int docs = await CountAllPages(
                    c, "SELECT * FROM c WHERE c.pk = @pk AND c.tag = @tag",
                    partitionKey: null, parms).ConfigureAwait(false);

                bool ok = docs == 5;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"WHERE c.pk='{Short(scopedKey)}' (no WithPartitionKey) docs={docs}/5 (predicate-scoped)");
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        // W2 — full HPK expressed IN THE WHERE CLAUSE (tenant/region/user equality)
        // instead of via WithPartitionKey. No per-request key; the predicate does
        // the scoping. Expect exactly the 3 docs for [tenant-A,region-east,user-1].
        // Contrast with H1 (WithPartitionKey) which returns 9.
        private static async Task<TestResult> W2_HpkWhereClause(
            NativeCosmosClient c, QueryDataset d)
        {
            const string name = "W2 HPK via WHERE clause";
            var parms = new (string, object?)[]
            {
                ("@tenant", "tenant-A"), ("@region", "region-east"), ("@user", "user-1"), ("@tag", d.RunTag),
            };
            try
            {
                int docs = await CountAllPages(
                    c, "SELECT * FROM c WHERE c.tenant = @tenant AND c.region = @region AND c.user = @user AND c.tag = @tag",
                    partitionKey: null, parms).ConfigureAwait(false);

                bool ok = docs == 3;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"WHERE c.tenant/region/user (no WithPartitionKey) docs={docs}/3 (predicate-scoped)");
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        // ---------------- Helpers ----------------

        // Races a test against a wall-clock timeout so a DRIVER-SIDE HANG OR
        // PANIC (e.g. the planner.rs EPK-range panic that W1/W2 can trigger via
        // a partition-key predicate in the WHERE clause) surfaces as a FAIL and
        // lets the remaining scenarios run, instead of dead-locking the .NET
        // await on a native completion that will never fire. The orphaned task
        // is abandoned; native worker threads do not block CLR shutdown.
        private static async Task<TestResult> WithTimeout(string name, Task<TestResult> test, int seconds = 30)
        {
            Task winner = await Task.WhenAny(test, Task.Delay(TimeSpan.FromSeconds(seconds))).ConfigureAwait(false);
            if (winner != test)
            {
                return new TestResult(name, TestStatus.Fail,
                    $"TIMEOUT after {seconds}s — driver did not return (likely worker panic; check stderr for planner.rs)");
            }
            return await test.ConfigureAwait(false);
        }

        private static async Task<int> CountAllPages(
            NativeCosmosClient c,
            string queryText,
            object[]? partitionKey,
            IReadOnlyList<(string Name, object? Value)> parameters)
        {
            int total = 0;
            await foreach (CosmosNativeResponse page in c.QueryPagesAsync(
                queryText, partitionKey, maxItemCount: 50, parameters: parameters))
            {
                total += CountDocuments(page.Body);
            }
            return total;
        }

        private static int CountDocuments(byte[] body)
        {
            try
            {
                using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("Documents", out System.Text.Json.JsonElement docs) &&
                    docs.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    return docs.GetArrayLength();
                }
            }
            catch { /* non-JSON or unexpected shape */ }
            return 0;
        }

        private static string Short(string s) => s.Length <= 18 ? s : "…" + s.Substring(s.Length - 17);

        private static TestResult Fail(string name, Exception ex) =>
            new TestResult(name, TestStatus.Fail,
                $"EXCEPTION {ex.GetType().Name}: {(ex.Message.Length <= 120 ? ex.Message : ex.Message.Substring(0, 120))}");

        private enum TestStatus { Pass, Fail, Skip }

        private sealed record TestResult(string Name, TestStatus Status, string Detail);
    }
}
