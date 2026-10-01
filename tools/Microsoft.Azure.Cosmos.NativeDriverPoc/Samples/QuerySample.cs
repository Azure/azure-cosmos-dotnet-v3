// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// End-to-end query test matrix against the native driver's feed-bucket
    /// submit path (<c>cosmos_submit_operation</c> with
    /// <c>CosmosOperationKind.QueryItems</c>).
    ///
    /// Runs every query shape worth testing against a freshly hydrated
    /// dataset and reports PASS / FAIL / SKIP per shape. Failures are
    /// expected (and valuable) for shapes the driver header documents
    /// as not-yet-implemented — e.g. multi-part feed bodies, x-partition
    /// ORDER BY (header §1700: "multi-part feed bodies return first part
    /// only; full iteration lands in Phase 8").
    ///
    /// Test groups:
    ///   A. Sanity — single-PK SinglePage + Paginated (legacy coverage)
    ///   B. Single-partition SQL shapes — TOP / OFFSET LIMIT / projection /
    ///      JOIN / SELECT VALUE / empty / unparameterized
    ///   C. Multi-part non-aggregate — ORDER BY only. Client-side
    ///      aggregations (COUNT / SUM / AVG / MIN / MAX / GROUP BY /
    ///      DISTINCT) are intentionally NOT attempted: the Rust driver
    ///      does not yet implement the client-side aggregate pipeline,
    ///      so issuing them is a known no-op rather than a useful probe.
    ///   D. Cross-partition queries — uses NativeCosmosClient.CreateCrossPartition
    ///   E. Large bodies — &gt;500 KB single-doc + &gt;15-doc batch
    ///   F. Hierarchical partition keys — pinned full HPK + isolation +
    ///      cross-HPK fan-out
    ///
    /// Configuration (env vars; emulator defaults if unset):
    ///   COSMOS_ENDPOINT   — account URI            (default: emulator)
    ///   COSMOS_KEY        — master key             (default: emulator well-known)
    ///   COSMOS_DATABASE   — database id            (default: pocdb)
    ///   COSMOS_CONTAINER  — single-PK container id (default: items)
    ///   COSMOS_HPK_CONTAINER — HPK container id    (default: items-hpk)
    /// </summary>
    internal static class QuerySample
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

            string runTag = $"qpoc-{Guid.NewGuid():N}".Substring(0, 14);  // shorter to keep ids readable

            Console.WriteLine("=== Native Driver — Query test matrix ===");
            Console.WriteLine($"  endpoint     : {endpoint}");
            Console.WriteLine($"  database     : {database}");
            Console.WriteLine($"  single-cont  : {container}        (PK path /pk)");
            Console.WriteLine($"  hpk-cont     : {hpkContainer}    (PK paths /tenant, /region, /user)");
            Console.WriteLine($"  run tag      : {runTag}");
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

            var results = new List<TestResult>();
            try
            {
                using var spClient = new NativeCosmosClient(
                    endpoint, masterKey, database, container, dataset.SinglePartitionKey,
                    userAgentSuffix: "cosmos-query-matrix-sp");
                using var xpClient = NativeCosmosClient.CreateCrossPartition(
                    endpoint, masterKey, database, container,
                    userAgentSuffix: "cosmos-query-matrix-xp");
                using var hpkPinned = new NativeCosmosClient(
                    endpoint, masterKey, database, hpkContainer,
                    new[] { "tenant-A", "region-east", "user-1" },
                    userAgentSuffix: "cosmos-query-matrix-hpk");
                using var hpkOtherUser = new NativeCosmosClient(
                    endpoint, masterKey, database, hpkContainer,
                    new[] { "tenant-A", "region-east", "user-2" },
                    userAgentSuffix: "cosmos-query-matrix-hpk2");
                using var hpkCross = NativeCosmosClient.CreateCrossPartition(
                    endpoint, masterKey, database, hpkContainer,
                    userAgentSuffix: "cosmos-query-matrix-hpkx");

                Console.WriteLine();
                Console.WriteLine("=== Group A — Sanity (single-PK) ===");
                results.Add(await GroupA_SinglePage(spClient, dataset).ConfigureAwait(false));
                results.Add(await GroupA_Paginated(spClient, dataset).ConfigureAwait(false));

                Console.WriteLine();
                Console.WriteLine("=== Group B — Single-partition SQL shapes ===");
                results.Add(await GroupB_Unparameterized(spClient, dataset).ConfigureAwait(false));
                results.Add(await GroupB_EmptyResult(spClient, dataset).ConfigureAwait(false));
                results.Add(await GroupB_SelectValueScalar(spClient, dataset).ConfigureAwait(false));
                results.Add(await GroupB_Projection(spClient, dataset).ConfigureAwait(false));
                results.Add(await GroupB_Join(spClient, dataset).ConfigureAwait(false));
                results.Add(await GroupB_Top(spClient, dataset).ConfigureAwait(false));
                results.Add(await GroupB_OffsetLimit(spClient, dataset).ConfigureAwait(false));

                Console.WriteLine();
                Console.WriteLine("=== Group C — Multi-part non-aggregate (ORDER BY) ===");
                Console.WriteLine("    NOTE: client-side aggregations (COUNT/SUM/AVG/MIN/MAX/GROUP BY/DISTINCT)");
                Console.WriteLine("          are NOT attempted — the Rust driver does not yet support the");
                Console.WriteLine("          client-side aggregate pipeline (gather+merge+accumulate).");
                results.Add(await GroupC_OrderBy(spClient, dataset).ConfigureAwait(false));

                Console.WriteLine();
                Console.WriteLine("=== Group D — Cross-partition queries ===");
                results.Add(await GroupD_XpartSinglePage(xpClient, dataset).ConfigureAwait(false));
                results.Add(await GroupD_XpartPaginated(xpClient, dataset).ConfigureAwait(false));
                results.Add(await GroupD_XpartOrderBy(xpClient, dataset).ConfigureAwait(false));

                Console.WriteLine();
                Console.WriteLine("=== Group E — Large bodies ===");
                results.Add(await GroupE_LargeSingleDoc(spClient, dataset).ConfigureAwait(false));
                results.Add(await GroupE_LargeFeed(xpClient, dataset).ConfigureAwait(false));

                Console.WriteLine();
                Console.WriteLine("=== Group F — Hierarchical partition keys ===");
                results.Add(await GroupF_HpkPinnedFull(hpkPinned, dataset).ConfigureAwait(false));
                results.Add(await GroupF_HpkIsolation(hpkOtherUser, dataset).ConfigureAwait(false));
                results.Add(await GroupF_HpkCrossPartition(hpkCross, dataset).ConfigureAwait(false));
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
            int skips = results.Count(r => r.Status == TestStatus.Skip);
            foreach (TestResult r in results)
            {
                string marker = r.Status switch
                {
                    TestStatus.Pass => "PASS",
                    TestStatus.Fail => "FAIL",
                    TestStatus.Skip => "SKIP",
                    _ => "????",
                };
                Console.WriteLine($"  [{marker}] {r.Name,-44} {r.Detail}");
            }
            Console.WriteLine();
            Console.WriteLine($"  total = {results.Count}  pass = {passes}  fail = {fails}  skip = {skips}");
            Console.WriteLine();
            Console.WriteLine("  Exit code 0 means all FAIL=0. SKIPs are expected for known driver gaps.");
            return fails == 0 ? 0 : 1;
        }

        // ---------------- Group A: Sanity ----------------

        private static async Task<TestResult> GroupA_SinglePage(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "A1 SinglePage (SELECT *, MaxItem=50)";
            var parms = new (string, object?)[] { ("@tag", d.SingleTag) };
            try
            {
                CosmosNativeResponse r = await c.QueryItemsPageAsync(
                    "SELECT * FROM c WHERE c.tag = @tag",
                    maxItemCount: 50,
                    parameters: parms).ConfigureAwait(false);
                int count = CountDocuments(r.Body);
                return Report(name, r, expectedHttp: 200, expectedCount: 5, actualCount: count);
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        private static async Task<TestResult> GroupA_Paginated(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "A2 Paginated (SELECT *, MaxItem=2)";
            var parms = new (string, object?)[] { ("@tag", d.SingleTag) };
            try
            {
                int total = 0, pages = 0;
                double ru = 0;
                int lastHttp = 0;
                await foreach (CosmosNativeResponse page in c.QueryItemsAsync(
                    "SELECT * FROM c WHERE c.tag = @tag", maxItemCount: 2, parameters: parms))
                {
                    pages++;
                    total += CountDocuments(page.Body);
                    ru += page.RequestCharge;
                    lastHttp = page.HttpStatusCode;
                }
                bool ok = total == 5 && lastHttp == 200;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"http={lastHttp} pages={pages} docs={total}/5 ru={ru:F2}");
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        // ---------------- Group B: Single-partition SQL shapes ----------------

        private static async Task<TestResult> GroupB_Unparameterized(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "B1 Unparameterized (string-literal tag)";
            try
            {
                CosmosNativeResponse r = await c.QueryItemsPageAsync(
                    $"SELECT * FROM c WHERE c.tag = '{d.SingleTag}'",
                    maxItemCount: 50).ConfigureAwait(false);
                int count = CountDocuments(r.Body);
                return Report(name, r, 200, expectedCount: 5, actualCount: count);
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        private static async Task<TestResult> GroupB_EmptyResult(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "B2 Empty result set";
            var parms = new (string, object?)[] { ("@tag", "no-such-tag-" + Guid.NewGuid()) };
            try
            {
                CosmosNativeResponse r = await c.QueryItemsPageAsync(
                    "SELECT * FROM c WHERE c.tag = @tag", maxItemCount: 50, parameters: parms).ConfigureAwait(false);
                int count = CountDocuments(r.Body);
                return Report(name, r, 200, expectedCount: 0, actualCount: count);
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        private static async Task<TestResult> GroupB_SelectValueScalar(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "B3 SELECT VALUE c.score";
            var parms = new (string, object?)[] { ("@tag", d.SingleTag) };
            try
            {
                CosmosNativeResponse r = await c.QueryItemsPageAsync(
                    "SELECT VALUE c.score FROM c WHERE c.tag = @tag", maxItemCount: 50, parameters: parms).ConfigureAwait(false);
                int count = CountDocuments(r.Body);
                return Report(name, r, 200, expectedCount: 5, actualCount: count);
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        private static async Task<TestResult> GroupB_Projection(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "B4 Projection (SELECT c.id, c.tag)";
            var parms = new (string, object?)[] { ("@tag", d.SingleTag) };
            try
            {
                CosmosNativeResponse r = await c.QueryItemsPageAsync(
                    "SELECT c.id, c.tag FROM c WHERE c.tag = @tag", maxItemCount: 50, parameters: parms).ConfigureAwait(false);
                int count = CountDocuments(r.Body);
                bool shaped = AllDocsHaveOnlyProperties(r.Body, "id", "tag");
                return new TestResult(name,
                    (r.HttpStatusCode == 200 && count == 5 && shaped) ? TestStatus.Pass : TestStatus.Fail,
                    $"http={r.HttpStatusCode} docs={count}/5 projection-shape={(shaped ? "ok" : "bad")} ru={r.RequestCharge:F2}");
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        private static async Task<TestResult> GroupB_Join(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "B5 JOIN intra-doc (UNNEST c.tags)";
            var parms = new (string, object?)[] { ("@tag", d.SingleTag) };
            try
            {
                CosmosNativeResponse r = await c.QueryItemsPageAsync(
                    "SELECT c.id, t FROM c JOIN t IN c.tags WHERE c.tag = @tag", maxItemCount: 50, parameters: parms).ConfigureAwait(false);
                int count = CountDocuments(r.Body);
                return Report(name, r, 200, expectedCount: 10, actualCount: count);  // 5 docs × 2 tags
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        private static async Task<TestResult> GroupB_Top(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "B6 TOP 3";
            var parms = new (string, object?)[] { ("@tag", d.SingleTag) };
            try
            {
                CosmosNativeResponse r = await c.QueryItemsPageAsync(
                    "SELECT TOP 3 * FROM c WHERE c.tag = @tag", maxItemCount: 50, parameters: parms).ConfigureAwait(false);
                int count = CountDocuments(r.Body);
                return Report(name, r, 200, expectedCount: 3, actualCount: count);
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        private static async Task<TestResult> GroupB_OffsetLimit(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "B7 OFFSET 2 LIMIT 2";
            var parms = new (string, object?)[] { ("@tag", d.SingleTag) };
            try
            {
                CosmosNativeResponse r = await c.QueryItemsPageAsync(
                    "SELECT * FROM c WHERE c.tag = @tag ORDER BY c.ordinal OFFSET 2 LIMIT 2",
                    maxItemCount: 50, parameters: parms).ConfigureAwait(false);
                int count = CountDocuments(r.Body);
                return Report(name, r, 200, expectedCount: 2, actualCount: count);
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        // ---------------- Group C: Multi-part feed bodies ----------------

        private static async Task<TestResult> GroupC_OrderBy(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "C1 ORDER BY c.ordinal DESC";
            var parms = new (string, object?)[] { ("@tag", d.SingleTag) };
            try
            {
                int total = 0, pages = 0;
                var ordinals = new List<int>();
                await foreach (CosmosNativeResponse page in c.QueryItemsAsync(
                    "SELECT * FROM c WHERE c.tag = @tag ORDER BY c.ordinal DESC",
                    maxItemCount: 50, parameters: parms))
                {
                    pages++;
                    total += CountDocuments(page.Body);
                    ordinals.AddRange(ExtractIntField(page.Body, "ordinal"));
                }
                bool descending = IsStrictlyDescending(ordinals);
                bool ok = total == 5 && descending;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"pages={pages} docs={total}/5 ordinals=[{string.Join(",", ordinals)}] desc={descending}");
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        // ---------------- Group D: Cross-partition queries ----------------

        private static async Task<TestResult> GroupD_XpartSinglePage(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "D1 X-partition SinglePage (15 docs)";
            var parms = new (string, object?)[] { ("@tag", d.XpartTag) };
            try
            {
                CosmosNativeResponse r = await c.QueryItemsPageAsync(
                    "SELECT * FROM c WHERE c.tag = @tag", maxItemCount: 50, parameters: parms).ConfigureAwait(false);
                int count = CountDocuments(r.Body);
                return Report(name, r, 200, expectedCount: 15, actualCount: count);
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        private static async Task<TestResult> GroupD_XpartPaginated(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "D2 X-partition Paginated (MaxItem=4)";
            var parms = new (string, object?)[] { ("@tag", d.XpartTag) };
            try
            {
                int total = 0, pages = 0;
                int lastHttp = 0;
                await foreach (CosmosNativeResponse page in c.QueryItemsAsync(
                    "SELECT * FROM c WHERE c.tag = @tag", maxItemCount: 4, parameters: parms))
                {
                    pages++;
                    total += CountDocuments(page.Body);
                    lastHttp = page.HttpStatusCode;
                }
                bool ok = total == 15 && lastHttp == 200;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"http={lastHttp} pages={pages} docs={total}/15");
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        private static async Task<TestResult> GroupD_XpartOrderBy(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "D3 X-partition ORDER BY ordinal";
            var parms = new (string, object?)[] { ("@tag", d.XpartTag) };
            try
            {
                int total = 0, pages = 0;
                await foreach (CosmosNativeResponse page in c.QueryItemsAsync(
                    "SELECT * FROM c WHERE c.tag = @tag ORDER BY c.ordinal",
                    maxItemCount: 50, parameters: parms))
                {
                    pages++;
                    total += CountDocuments(page.Body);
                }
                return new TestResult(name,
                    total == 15 ? TestStatus.Pass : TestStatus.Fail,
                    $"pages={pages} docs={total}/15 (multi-part orchestration probe)");
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        // ---------------- Group E: Large bodies ----------------

        private static async Task<TestResult> GroupE_LargeSingleDoc(NativeCosmosClient _, QueryDataset d)
        {
            const string name = "E1 Single ~600KB doc by id";
            // Need a client pinned to LargeDocPartitionKey.
            string endpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT") ?? EmulatorEndpoint;
            string masterKey = Environment.GetEnvironmentVariable("COSMOS_KEY") ?? EmulatorKey;
            try
            {
                using var c2 = new NativeCosmosClient(endpoint, masterKey, d.Database, d.SingleContainer,
                    d.LargeDocPartitionKey, userAgentSuffix: "cosmos-query-matrix-large");
                var parms = new (string, object?)[] { ("@id", d.LargeDocId) };
                CosmosNativeResponse r = await c2.QueryItemsPageAsync(
                    "SELECT * FROM c WHERE c.id = @id", maxItemCount: 5, parameters: parms).ConfigureAwait(false);
                int count = CountDocuments(r.Body);
                bool sizeOk = r.Body.Length > 500_000;
                return new TestResult(name,
                    (r.HttpStatusCode == 200 && count == 1 && sizeOk) ? TestStatus.Pass : TestStatus.Fail,
                    $"http={r.HttpStatusCode} docs={count}/1 bytes={r.Body.Length:N0} (>500K? {sizeOk})");
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        private static async Task<TestResult> GroupE_LargeFeed(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "E2 X-partition feed of 15 docs in one page";
            var parms = new (string, object?)[] { ("@tag", d.XpartTag) };
            try
            {
                CosmosNativeResponse r = await c.QueryItemsPageAsync(
                    "SELECT * FROM c WHERE c.tag = @tag", maxItemCount: 100, parameters: parms).ConfigureAwait(false);
                int count = CountDocuments(r.Body);
                return new TestResult(name,
                    (r.HttpStatusCode == 200 && count == 15) ? TestStatus.Pass : TestStatus.Fail,
                    $"http={r.HttpStatusCode} docs={count}/15 bytes={r.Body.Length:N0}");
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        // ---------------- Group F: HPK ----------------

        private static async Task<TestResult> GroupF_HpkPinnedFull(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "F1 HPK pinned (tenant-A/region-east/user-1)";
            var parms = new (string, object?)[] { ("@tag", d.RunTag) };
            try
            {
                CosmosNativeResponse r = await c.QueryItemsPageAsync(
                    "SELECT * FROM c WHERE c.tag = @tag", maxItemCount: 50, parameters: parms).ConfigureAwait(false);
                int count = CountDocuments(r.Body);
                return Report(name, r, 200, expectedCount: 3, actualCount: count);
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        private static async Task<TestResult> GroupF_HpkIsolation(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "F2 HPK isolation (different user same tenant)";
            var parms = new (string, object?)[] { ("@tag", d.RunTag) };
            try
            {
                CosmosNativeResponse r = await c.QueryItemsPageAsync(
                    "SELECT * FROM c WHERE c.tag = @tag AND c.user = 'user-2'",
                    maxItemCount: 50, parameters: parms).ConfigureAwait(false);
                int count = CountDocuments(r.Body);
                return Report(name, r, 200, expectedCount: 3, actualCount: count);
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        private static async Task<TestResult> GroupF_HpkCrossPartition(NativeCosmosClient c, QueryDataset d)
        {
            const string name = "F3 HPK cross-partition (all 9 docs)";
            var parms = new (string, object?)[] { ("@tag", d.RunTag) };
            try
            {
                int total = 0, pages = 0;
                int lastHttp = 0;
                await foreach (CosmosNativeResponse page in c.QueryItemsAsync(
                    "SELECT * FROM c WHERE c.tag = @tag", maxItemCount: 50, parameters: parms))
                {
                    pages++;
                    total += CountDocuments(page.Body);
                    lastHttp = page.HttpStatusCode;
                }
                bool ok = total == 9 && lastHttp == 200;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"http={lastHttp} pages={pages} docs={total}/9");
            }
            catch (Exception ex) { return Fail(name, ex); }
        }

        // ---------------- Helpers ----------------

        private static TestResult Report(string name, CosmosNativeResponse r, int expectedHttp, int expectedCount, int actualCount)
        {
            bool ok = r.HttpStatusCode == expectedHttp && actualCount == expectedCount;
            return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                $"http={r.HttpStatusCode} docs={actualCount}/{expectedCount} bytes={r.Body.Length:N0} ru={r.RequestCharge:F2}");
        }

        private static TestResult Fail(string name, Exception ex)
        {
            return new TestResult(name, TestStatus.Fail,
                $"EXCEPTION {ex.GetType().Name}: {Truncate(ex.Message, 120)}");
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";

        private static int CountDocuments(byte[] body)
        {
            if (body is null || body.Length == 0) return 0;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("Documents", out JsonElement docs)
                    && docs.ValueKind == JsonValueKind.Array)
                {
                    return docs.GetArrayLength();
                }
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return doc.RootElement.GetArrayLength();
                }
                return 0;
            }
            catch (JsonException) { return 0; }
        }

        private static bool AllDocsHaveOnlyProperties(byte[] body, params string[] expectedProps)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("Documents", out JsonElement docs)
                    || docs.ValueKind != JsonValueKind.Array) return false;
                foreach (JsonElement el in docs.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) return false;
                    var keys = new HashSet<string>();
                    foreach (JsonProperty p in el.EnumerateObject()) keys.Add(p.Name);
                    foreach (string e in expectedProps) if (!keys.Contains(e)) return false;
                    // Each doc must have exactly the expected projection keys.
                    if (keys.Count != expectedProps.Length) return false;
                }
                return true;
            }
            catch (JsonException) { return false; }
        }

        private static IEnumerable<int> ExtractIntField(byte[] body, string field)
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("Documents", out JsonElement docs)) yield break;
            foreach (JsonElement el in docs.EnumerateArray())
            {
                if (el.TryGetProperty(field, out JsonElement v) && v.TryGetInt32(out int n)) yield return n;
            }
        }

        private static bool IsStrictlyDescending(List<int> xs)
        {
            for (int i = 1; i < xs.Count; i++) if (xs[i] > xs[i - 1]) return false;
            return xs.Count > 0;
        }

        private enum TestStatus { Pass, Fail, Skip }

        private sealed record TestResult(string Name, TestStatus Status, string Detail);
    }
}
