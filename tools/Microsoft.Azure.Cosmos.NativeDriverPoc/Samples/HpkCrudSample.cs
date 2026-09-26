// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// Read/Write/CRUD coverage for HIERARCHICAL partition keys (HPK).
    ///
    /// Context: the query matrix (Group F) showed that <c>QueryItems</c>
    /// ignores the pinned <c>cosmos_partition_key_t*</c> and fans out across
    /// every logical partition (F1 returned 9 docs instead of 3). Point
    /// operations are a DIFFERENT code path — Create/Read/Replace/Upsert/Delete
    /// pass <c>WithPartitionKey(this.partitionKey)</c> explicitly to the FFI
    /// request builder — so they SHOULD route by the pinned full HPK. This
    /// suite proves that end-to-end for a 3-component key
    /// <c>[tenant, region, user]</c> and, critically, tests pin-based
    /// ISOLATION at the point-op level (H8) — the thing the query path could
    /// not validate.
    ///
    /// Container: <c>items-hpk</c>, PK paths <c>/tenant, /region, /user</c>
    /// (kind = MultiHash, version 2). Provisioned here via the V3 SDK if it
    /// does not already exist, so the suite is self-contained (no dependency
    /// on having run the query matrix first).
    ///
    /// Each native write carries the matching HPK property values in its body
    /// (<c>tenant</c>/<c>region</c>/<c>user</c>); the Cosmos backend validates
    /// the document's PK-path values against the partition key supplied on the
    /// request, so a body that disagrees with the pin would be rejected
    /// (BadRequest), not silently misrouted.
    ///
    /// Test list (all against a fresh GUID-tagged id space, cleaned up after):
    ///   H1 CREATE under [tenant-A, region-east, user-1]            -> 201
    ///   H2 READ it back, body round-trips (version=1)              -> 200
    ///   H3 REPLACE with version=2                                  -> 200
    ///   H4 READ after replace shows version=2                      -> 200
    ///   H5 UPSERT existing (->version=3) and UPSERT brand-new id   -> 200 / 201
    ///   H6 DELETE                                                  -> 204
    ///   H7 READ after delete                                       -> 404 NotFound
    ///   H8 ISOLATION: read user-1's doc through a user-2 pin       -> 404 NotFound
    ///   H9 second distinct key [tenant-B, region-west, user-3] CRUD round-trip
    ///
    /// Configuration (env vars; emulator defaults if unset):
    ///   COSMOS_ENDPOINT / COSMOS_KEY / COSMOS_DATABASE / COSMOS_HPK_CONTAINER
    /// </summary>
    internal static class HpkCrudSample
    {
        private const string EmulatorEndpoint = "https://localhost:8081/";
        private const string EmulatorKey =
            "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        private const string DefaultDatabase = "pocdb";
        private const string DefaultHpkContainer = "items-hpk";

        public static async Task<int> RunAsync()
        {
            string endpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT") ?? EmulatorEndpoint;
            string masterKey = Environment.GetEnvironmentVariable("COSMOS_KEY") ?? EmulatorKey;
            string database = Environment.GetEnvironmentVariable("COSMOS_DATABASE") ?? DefaultDatabase;
            string hpkContainer = Environment.GetEnvironmentVariable("COSMOS_HPK_CONTAINER") ?? DefaultHpkContainer;

            string runTag = $"hpkcrud-{Guid.NewGuid():N}".Substring(0, 18);

            Console.WriteLine("=== Native Driver — HPK CRUD matrix ===");
            Console.WriteLine($"  endpoint  : {endpoint}");
            Console.WriteLine($"  db / cont : {database} / {hpkContainer}   (PK paths /tenant, /region, /user)");
            Console.WriteLine($"  run tag   : {runTag}");
            Console.WriteLine();

            // Provision DB + HPK container via the V3 SDK so the suite is
            // self-contained. Identical container shape to QueryDataset.
            Console.WriteLine("[setup] ensuring database + HPK container exist (via V3 SDK)");
            try
            {
                await EnsureContainerAsync(endpoint, masterKey, database, hpkContainer).ConfigureAwait(false);
                Console.WriteLine("[setup] done");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[setup] FAILED — {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine("Aborting; cannot run HPK CRUD without the container.");
                return 2;
            }

            // Pinned clients. CRUD routes by the pinned full HPK handle.
            using var user1 = new NativeCosmosClient(
                endpoint, masterKey, database, hpkContainer,
                new[] { "tenant-A", "region-east", "user-1" },
                userAgentSuffix: "cosmos-hpkcrud-u1");
            using var user2 = new NativeCosmosClient(
                endpoint, masterKey, database, hpkContainer,
                new[] { "tenant-A", "region-east", "user-2" },
                userAgentSuffix: "cosmos-hpkcrud-u2");
            using var tenantB = new NativeCosmosClient(
                endpoint, masterKey, database, hpkContainer,
                new[] { "tenant-B", "region-west", "user-3" },
                userAgentSuffix: "cosmos-hpkcrud-tb");

            string docId = $"{runTag}-doc";
            string upsertNewId = $"{runTag}-upsert-new";
            string tenantBId = $"{runTag}-tb-doc";

            string BodyU1(int version, string message) =>
                $$"""{"id":"{{docId}}","tenant":"tenant-A","region":"region-east","user":"user-1","tag":"{{runTag}}","message":"{{message}}","version":{{version}}}""";

            var results = new List<TestResult>();
            try
            {
                Console.WriteLine();
                Console.WriteLine("=== Running HPK CRUD tests ===");

                // H1 CREATE
                results.Add(await Expect("H1 CREATE (user-1, v1)", 201,
                    () => user1.CreateItemAsync(docId, BodyU1(1, "created"))).ConfigureAwait(false));

                // H2 READ back, version=1
                results.Add(await ExpectRead("H2 READ back (v1)", user1, docId, 200, expectVersion: 1).ConfigureAwait(false));

                // H3 REPLACE -> v2
                results.Add(await Expect("H3 REPLACE (v2)", 200,
                    () => user1.ReplaceItemAsync(docId, BodyU1(2, "replaced"))).ConfigureAwait(false));

                // H4 READ shows v2
                results.Add(await ExpectRead("H4 READ after replace (v2)", user1, docId, 200, expectVersion: 2).ConfigureAwait(false));

                // H5 UPSERT existing -> v3 (200) AND upsert brand-new id (201)
                results.Add(await H5_Upsert(user1, docId, upsertNewId, runTag).ConfigureAwait(false));

                // H6 DELETE the main doc
                results.Add(await Expect("H6 DELETE (user-1 doc)", 204,
                    () => user1.DeleteItemAsync(docId)).ConfigureAwait(false));

                // H7 READ after delete -> 404
                results.Add(await ExpectNotFound("H7 READ after delete -> 404", user1, docId).ConfigureAwait(false));

                // H8 ISOLATION — recreate under user-1, then read via user-2 pin.
                results.Add(await H8_Isolation(user1, user2, runTag).ConfigureAwait(false));

                // H9 second distinct HPK round-trip
                results.Add(await H9_SecondTenant(tenantB, tenantBId, runTag).ConfigureAwait(false));
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("[cleanup] best-effort delete of seeded docs");
                await BestEffortDelete(user1, docId).ConfigureAwait(false);
                await BestEffortDelete(user1, upsertNewId).ConfigureAwait(false);
                await BestEffortDelete(user1, $"{runTag}-iso").ConfigureAwait(false);
                await BestEffortDelete(tenantB, tenantBId).ConfigureAwait(false);
            }

            Console.WriteLine();
            Console.WriteLine("=== Summary ===");
            int passes = results.Count(r => r.Status == TestStatus.Pass);
            int fails = results.Count(r => r.Status == TestStatus.Fail);
            foreach (TestResult r in results)
            {
                string marker = r.Status == TestStatus.Pass ? "PASS" : (r.Status == TestStatus.Skip ? "SKIP" : "FAIL");
                Console.WriteLine($"  [{marker}] {r.Name,-34} {r.Detail}");
            }
            Console.WriteLine();
            Console.WriteLine($"  total = {results.Count}  pass = {passes}  fail = {fails}");
            return fails == 0 ? 0 : 1;
        }

        // ---------------- Individual multi-step tests ----------------

        private static async Task<TestResult> H5_Upsert(
            NativeCosmosClient c, string existingId, string newId, string runTag)
        {
            const string name = "H5 UPSERT (update + insert)";
            try
            {
                string updateBody =
                    $$"""{"id":"{{existingId}}","tenant":"tenant-A","region":"region-east","user":"user-1","tag":"{{runTag}}","message":"upserted","version":3}""";
                CosmosNativeResponse upd = await c.UpsertItemAsync(existingId, updateBody).ConfigureAwait(false);

                string insertBody =
                    $$"""{"id":"{{newId}}","tenant":"tenant-A","region":"region-east","user":"user-1","tag":"{{runTag}}","message":"upsert-insert","version":1}""";
                CosmosNativeResponse ins = await c.UpsertItemAsync(newId, insertBody).ConfigureAwait(false);

                // Confirm the update actually moved the version forward.
                CosmosNativeResponse readBack = await c.ReadItemAsync(existingId).ConfigureAwait(false);
                int? v = ExtractVersion(readBack.BodyAsString());

                bool ok = upd.HttpStatusCode == 200 && ins.HttpStatusCode == 201 && v == 3;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"update http={upd.HttpStatusCode} (exp 200), insert http={ins.HttpStatusCode} (exp 201), readVersion={v} (exp 3)");
            }
            catch (Exception ex)
            {
                return new TestResult(name, TestStatus.Fail, $"EXCEPTION {ex.GetType().Name}: {Trunc(ex.Message)}");
            }
        }

        // H8 — the isolation test the query path could not give us. Create a doc
        // in user-1's logical partition, then attempt to READ the very same id
        // through a client pinned to user-2. Because point reads route by the
        // pinned HPK handle, user-2 should look in a DIFFERENT logical partition
        // and get a clean 404. A 200 here would mean point reads ALSO ignore the
        // pinned PK (a new finding beyond the known QueryItems bug).
        private static async Task<TestResult> H8_Isolation(
            NativeCosmosClient user1, NativeCosmosClient user2, string runTag)
        {
            const string name = "H8 cross-HPK isolation (read)";
            string isoId = $"{runTag}-iso";
            try
            {
                string body =
                    $$"""{"id":"{{isoId}}","tenant":"tenant-A","region":"region-east","user":"user-1","tag":"{{runTag}}","message":"iso","version":1}""";
                CosmosNativeResponse created = await user1.CreateItemAsync(isoId, body).ConfigureAwait(false);

                // Sanity: user-1 (the owning pin) CAN read it.
                CosmosNativeResponse ownerRead = await user1.ReadItemAsync(isoId).ConfigureAwait(false);

                // The actual isolation assertion: user-2's pin must NOT find it.
                try
                {
                    CosmosNativeResponse leaked = await user2.ReadItemAsync(isoId).ConfigureAwait(false);
                    return new TestResult(name, TestStatus.Fail,
                        $"LEAK: user-2 pin read user-1's doc http={leaked.HttpStatusCode} " +
                        "(point read ignored the pinned HPK — new finding beyond QueryItems)");
                }
                catch (CosmosNativeException ex) when (ex.HttpStatusCode == 404)
                {
                    bool ok = created.HttpStatusCode == 201 && ownerRead.HttpStatusCode == 200;
                    return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                        $"owner read http={ownerRead.HttpStatusCode}, foreign pin -> 404 (isolated)");
                }
            }
            catch (Exception ex)
            {
                return new TestResult(name, TestStatus.Fail, $"EXCEPTION {ex.GetType().Name}: {Trunc(ex.Message)}");
            }
        }

        private static async Task<TestResult> H9_SecondTenant(
            NativeCosmosClient c, string id, string runTag)
        {
            const string name = "H9 second key CRUD (tenant-B)";
            try
            {
                string body =
                    $$"""{"id":"{{id}}","tenant":"tenant-B","region":"region-west","user":"user-3","tag":"{{runTag}}","message":"tb","version":1}""";
                CosmosNativeResponse created = await c.CreateItemAsync(id, body).ConfigureAwait(false);
                CosmosNativeResponse read = await c.ReadItemAsync(id).ConfigureAwait(false);
                CosmosNativeResponse del = await c.DeleteItemAsync(id).ConfigureAwait(false);

                bool ok = created.HttpStatusCode == 201 && read.HttpStatusCode == 200 && del.HttpStatusCode == 204;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"create={created.HttpStatusCode} read={read.HttpStatusCode} delete={del.HttpStatusCode} (exp 201/200/204)");
            }
            catch (Exception ex)
            {
                return new TestResult(name, TestStatus.Fail, $"EXCEPTION {ex.GetType().Name}: {Trunc(ex.Message)}");
            }
        }

        // ---------------- Small assertion helpers ----------------

        private static async Task<TestResult> Expect(
            string name, int expectedHttp, Func<Task<CosmosNativeResponse>> op)
        {
            try
            {
                CosmosNativeResponse r = await op().ConfigureAwait(false);
                bool ok = r.HttpStatusCode == expectedHttp;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"http={r.HttpStatusCode} (exp {expectedHttp}) ru={r.RequestCharge:F2}");
            }
            catch (Exception ex)
            {
                return new TestResult(name, TestStatus.Fail, $"EXCEPTION {ex.GetType().Name}: {Trunc(ex.Message)}");
            }
        }

        private static async Task<TestResult> ExpectRead(
            string name, NativeCosmosClient c, string id, int expectedHttp, int expectVersion)
        {
            try
            {
                CosmosNativeResponse r = await c.ReadItemAsync(id).ConfigureAwait(false);
                int? v = ExtractVersion(r.BodyAsString());
                bool ok = r.HttpStatusCode == expectedHttp && v == expectVersion;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"http={r.HttpStatusCode} (exp {expectedHttp}) version={v} (exp {expectVersion})");
            }
            catch (Exception ex)
            {
                return new TestResult(name, TestStatus.Fail, $"EXCEPTION {ex.GetType().Name}: {Trunc(ex.Message)}");
            }
        }

        private static async Task<TestResult> ExpectNotFound(
            string name, NativeCosmosClient c, string id)
        {
            try
            {
                CosmosNativeResponse r = await c.ReadItemAsync(id).ConfigureAwait(false);
                return new TestResult(name, TestStatus.Fail, $"expected 404, got http={r.HttpStatusCode}");
            }
            catch (CosmosNativeException ex) when (ex.HttpStatusCode == 404 && ex.IsNotFound)
            {
                return new TestResult(name, TestStatus.Pass, "404 NotFound (IsNotFound=true)");
            }
            catch (Exception ex)
            {
                return new TestResult(name, TestStatus.Fail, $"wrong exception {ex.GetType().Name}: {Trunc(ex.Message)}");
            }
        }

        private static async Task BestEffortDelete(NativeCosmosClient c, string id)
        {
            try { await c.DeleteItemAsync(id).ConfigureAwait(false); }
            catch { /* doc may not exist — fine */ }
        }

        private static int? ExtractVersion(string json)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("version", out JsonElement v) &&
                    v.ValueKind == JsonValueKind.Number)
                {
                    return v.GetInt32();
                }
            }
            catch { /* not JSON / no version field */ }
            return null;
        }

        private static async Task EnsureContainerAsync(
            string endpoint, string masterKey, string database, string hpkContainer)
        {
            using var sdk = new CosmosClient(
                endpoint, masterKey,
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

            DatabaseResponse dbResp = await sdk.CreateDatabaseIfNotExistsAsync(database).ConfigureAwait(false);
            await dbResp.Database.CreateContainerIfNotExistsAsync(new ContainerProperties
            {
                Id = hpkContainer,
                PartitionKeyPaths = new Collection<string> { "/tenant", "/region", "/user" },
            }).ConfigureAwait(false);
        }

        private static string Trunc(string s) => s.Length <= 110 ? s : s.Substring(0, 110) + "...";

        private enum TestStatus { Pass, Fail, Skip }

        private sealed record TestResult(string Name, TestStatus Status, string Detail);
    }
}
