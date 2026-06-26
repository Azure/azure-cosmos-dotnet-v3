// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Deterministic read-your-own-writes (RYOW) repro for the missing
    /// session-token defect.
    ///
    /// Background: against a multi-replica account at the default Session
    /// consistency, every write response carries a session token. The SDK is
    /// expected to capture that token and echo it on the IMMEDIATELY FOLLOWING
    /// read so the read is routed to (or waits for) a replica that has applied
    /// the write. The native driver does not appear to capture/propagate that
    /// token, so a read issued right after a write can land on a replica that
    /// has not yet caught up and return 404 (after create) or a stale body
    /// (after replace). This is INVISIBLE on the single-replica emulator
    /// (no lagging replica exists) and FLAKY in one-shot samples (depends on
    /// which replica the read happens to hit).
    ///
    /// This sample makes it DETERMINISTIC by repeating a tight
    /// CREATE -> READ(same id, same client) loop N times. Across N iterations
    /// the probability that every read hits a caught-up replica is low, so the
    /// anomaly count is reliably &gt; 0 on a real multi-replica account. A
    /// correct session-token implementation yields 0 anomalies regardless of N.
    ///
    /// Two loops, one per key shape, both with NO artificial delay between the
    /// write and the read (the whole point is to read before convergence):
    ///   * single (non-hierarchical) PK  -> container `items`     (PK /pk)
    ///   * hierarchical PK [t,r,u]        -> container `items-hpk` (PK /tenant,/region,/user)
    ///
    /// Anomaly classes counted per iteration:
    ///   CREATE-404   : read right after a 201 create returned 404 NotFound
    ///   STALE-READ   : read right after a 200 replace returned the OLD version
    ///   (Either is a session-consistency violation: a client must always be
    ///    able to read its own most-recent committed write.)
    ///
    /// Exit code is non-zero if ANY anomaly is observed (i.e. the bug
    /// reproduced). Iterations default to 50 per shape; override with env
    /// COSMOS_RYOW_ITERS.
    ///
    /// Configuration (env vars; emulator defaults if unset):
    ///   COSMOS_ENDPOINT / COSMOS_KEY / COSMOS_DATABASE
    ///   COSMOS_CONTAINER / COSMOS_HPK_CONTAINER / COSMOS_RYOW_ITERS
    /// </summary>
    internal static class ReadYourWritesSample
    {
        private const string EmulatorEndpoint = "https://localhost:8081/";
        private const string EmulatorKey =
            "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        private const string DefaultDatabase = "pocdb";
        private const string DefaultContainer = "items";
        private const string DefaultHpkContainer = "items-hpk";
        private const int DefaultIterations = 50;

        public static async Task<int> RunAsync()
        {
            string endpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT") ?? EmulatorEndpoint;
            string masterKey = Environment.GetEnvironmentVariable("COSMOS_KEY") ?? EmulatorKey;
            string database = Environment.GetEnvironmentVariable("COSMOS_DATABASE") ?? DefaultDatabase;
            string container = Environment.GetEnvironmentVariable("COSMOS_CONTAINER") ?? DefaultContainer;
            string hpkContainer = Environment.GetEnvironmentVariable("COSMOS_HPK_CONTAINER") ?? DefaultHpkContainer;
            int iterations = ParseIters(Environment.GetEnvironmentVariable("COSMOS_RYOW_ITERS"), DefaultIterations);

            string runTag = $"ryow-{Guid.NewGuid():N}".Substring(0, 14);

            Console.WriteLine("=== Native Driver — Read-your-own-writes (session-token) repro ===");
            Console.WriteLine($"  endpoint     : {endpoint}");
            Console.WriteLine($"  single-cont  : {container}        (PK path /pk)");
            Console.WriteLine($"  hpk-cont     : {hpkContainer}    (PK paths /tenant, /region, /user)");
            Console.WriteLine($"  iterations   : {iterations} per key shape");
            Console.WriteLine($"  run tag      : {runTag}");
            Console.WriteLine("  Each iteration: CREATE id -> immediately READ same id on the SAME");
            Console.WriteLine("  client. Any 404/stale read is a Session-consistency (RYOW) violation.");
            Console.WriteLine();

            var results = new List<LoopResult>();

            // --- Single (non-hierarchical) PK ------------------------------
            // Each iteration uses a fresh id; pk == id (partition-of-one), so a
            // new pinned client per id. The create + read are back-to-back with
            // no delay, on the same client (same session scope).
            int singleAnoms = 0, singleCreate404 = 0, singleStale = 0;
            Console.WriteLine($"[single] running {iterations} CREATE->READ iterations ...");
            for (int i = 0; i < iterations; i++)
            {
                string id = $"{runTag}-sp-{i:D3}";
                using var client = new NativeCosmosClient(
                    endpoint, masterKey, database, container, id,
                    userAgentSuffix: "cosmos-ryow-sp");

                string bodyV1 = $$"""{"id":"{{id}}","pk":"{{id}}","tag":"{{runTag}}","version":1}""";
                string bodyV2 = $$"""{"id":"{{id}}","pk":"{{id}}","tag":"{{runTag}}","version":2}""";

                var a = await OneIteration(client, id, bodyV1, bodyV2).ConfigureAwait(false);
                singleCreate404 += a.Create404 ? 1 : 0;
                singleStale += a.StaleRead ? 1 : 0;
                if (a.Create404 || a.StaleRead) singleAnoms++;

                try { await client.DeleteItemAsync(id).ConfigureAwait(false); } catch { /* best-effort */ }
            }
            results.Add(new LoopResult("single PK", iterations, singleAnoms, singleCreate404, singleStale));

            // --- Hierarchical PK -------------------------------------------
            // One pinned HPK client, reused; each iteration a fresh id under the
            // SAME logical partition [tenant-A,region-east,user-1].
            int hpkAnoms = 0, hpkCreate404 = 0, hpkStale = 0;
            Console.WriteLine($"[hpk] running {iterations} CREATE->READ iterations ...");
            using (var hpkClient = new NativeCosmosClient(
                endpoint, masterKey, database, hpkContainer,
                new[] { "tenant-A", "region-east", "user-1" },
                userAgentSuffix: "cosmos-ryow-hpk"))
            {
                for (int i = 0; i < iterations; i++)
                {
                    string id = $"{runTag}-hpk-{i:D3}";
                    string bodyV1 = $$"""{"id":"{{id}}","tenant":"tenant-A","region":"region-east","user":"user-1","tag":"{{runTag}}","version":1}""";
                    string bodyV2 = $$"""{"id":"{{id}}","tenant":"tenant-A","region":"region-east","user":"user-1","tag":"{{runTag}}","version":2}""";

                    var a = await OneIteration(hpkClient, id, bodyV1, bodyV2).ConfigureAwait(false);
                    hpkCreate404 += a.Create404 ? 1 : 0;
                    hpkStale += a.StaleRead ? 1 : 0;
                    if (a.Create404 || a.StaleRead) hpkAnoms++;

                    try { await hpkClient.DeleteItemAsync(id).ConfigureAwait(false); } catch { /* best-effort */ }
                }
            }
            results.Add(new LoopResult("HPK [A/east/user-1]", iterations, hpkAnoms, hpkCreate404, hpkStale));

            // --- Summary ---------------------------------------------------
            Console.WriteLine();
            Console.WriteLine("=== Summary ===");
            int totalAnoms = results.Sum(r => r.Anomalies);
            foreach (LoopResult r in results)
            {
                string marker = r.Anomalies == 0 ? "PASS" : "FAIL";
                Console.WriteLine($"  [{marker}] {r.Shape,-22} anomalies={r.Anomalies}/{r.Iterations}  (create-404={r.Create404}, stale-read={r.StaleRead})");
            }
            Console.WriteLine();
            Console.WriteLine($"  total anomalies = {totalAnoms} across {results.Sum(r => r.Iterations)} write→read pairs");
            Console.WriteLine();
            if (totalAnoms == 0)
            {
                Console.WriteLine("  0 anomalies. EITHER the session token is propagated correctly,");
                Console.WriteLine("  OR this ran against a single-replica account/emulator where the");
                Console.WriteLine("  bug cannot manifest. Re-run against a multi-replica account.");
            }
            else
            {
                Console.WriteLine("  >0 anomalies = read-your-own-writes VIOLATED. A read immediately");
                Console.WriteLine("  after this client's own committed write returned 404/stale —");
                Console.WriteLine("  consistent with the session token not being captured from the");
                Console.WriteLine("  write response and echoed on the subsequent read.");
            }

            // Non-zero exit when the bug reproduced.
            return totalAnoms == 0 ? 0 : 1;
        }

        // One CREATE -> READ(-after-create) -> REPLACE -> READ(-after-replace)
        // cycle. Reads happen with NO delay after the write so they probe the
        // pre-convergence window.
        private static async Task<Anomaly> OneIteration(
            NativeCosmosClient client, string id, string bodyV1, string bodyV2)
        {
            bool create404 = false;
            bool staleRead = false;

            // CREATE then immediately READ the same id.
            await client.CreateItemAsync(id, bodyV1).ConfigureAwait(false);
            try
            {
                CosmosNativeResponse read = await client.ReadItemAsync(id).ConfigureAwait(false);
                if (read.HttpStatusCode == 404)
                {
                    create404 = true;
                }
                else if (read.HttpStatusCode == 200 && VersionOf(read.Body) != 1)
                {
                    staleRead = true; // read succeeded but not the version we just wrote
                }
            }
            catch (CosmosNativeException ex) when (ex.IsNotFound)
            {
                create404 = true; // 404 surfaced as an exception
            }

            // REPLACE to v2 then immediately READ; expect to see version 2.
            try
            {
                await client.ReplaceItemAsync(id, bodyV2).ConfigureAwait(false);
                CosmosNativeResponse read2 = await client.ReadItemAsync(id).ConfigureAwait(false);
                if (read2.HttpStatusCode == 200 && VersionOf(read2.Body) == 1)
                {
                    staleRead = true; // saw the pre-replace version
                }
            }
            catch (CosmosNativeException) { /* replace/read may 404 on a lagging replica; counted via create path */ }

            return new Anomaly(create404, staleRead);
        }

        private static int VersionOf(byte[] body)
        {
            try
            {
                using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("version", out System.Text.Json.JsonElement v) &&
                    v.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    return v.GetInt32();
                }
            }
            catch { /* unexpected shape */ }
            return -1;
        }

        private static int ParseIters(string? raw, int fallback)
        {
            if (int.TryParse(raw, out int n) && n > 0) return n;
            return fallback;
        }

        private readonly record struct Anomaly(bool Create404, bool StaleRead);

        private sealed record LoopResult(string Shape, int Iterations, int Anomalies, int Create404, int StaleRead);
    }
}
