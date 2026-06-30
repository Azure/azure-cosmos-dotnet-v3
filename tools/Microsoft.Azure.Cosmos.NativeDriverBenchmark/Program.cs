// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverBenchmark
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Running;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.NativeDriverPoc;

    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // `dotnet run -- validate`          — reads-only sanity check.
            // `dotnet run -- validate --crud`   — full CRUD sanity per mode.
            // `dotnet run -- validate --query`  — query-shape sanity per mode.
            // All three are cheap pre-flights; run before kicking off a full
            // benchmark suite to catch env-var / DLL / firewall issues.
            if (args.Length > 0 && string.Equals(args[0], "validate", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length > 1 && string.Equals(args[1], "--crud", StringComparison.OrdinalIgnoreCase))
                {
                    return await ValidateCrudAsync().ConfigureAwait(false);
                }
                if (args.Length > 1 && string.Equals(args[1], "--query", StringComparison.OrdinalIgnoreCase))
                {
                    return await ValidateQueryAsync().ConfigureAwait(false);
                }
                return await ValidateAsync().ConfigureAwait(false);
            }

            // Everything else goes to BenchmarkSwitcher, which discovers
            // every public [Benchmark]-bearing class in this assembly
            // (ReadItem / CreateItem / ReplaceItem / DeleteItem).
            //
            //   dotnet run -c Release                              full matrix (4 classes x 3 methods)
            //   dotnet run -c Release -- --filter "*ReadItem*"     reads only
            //   dotnet run -c Release -- --filter "*Create*"       creates only
            //   dotnet run -c Release -- --filter "*Native*"       all native rows
            //   dotnet run -c Release -- --list flat               enumerate without running
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
                .Run(args, ManualConfig.CreateMinimumViable()
                    .WithOption(ConfigOptions.JoinSummary, true));
            return 0;
        }

        /// <summary>
        /// Read-only sanity. Runs one ReadItem per mode, prints status +
        /// bytes + RU, and confirms all three paths see the same document.
        /// Cheap and write-free — safe to run anywhere.
        /// </summary>
        private static async Task<int> ValidateAsync()
        {
            BenchmarkSettings settings;
            try
            {
                settings = BenchmarkSettings.FromEnvironment();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — configuration: {ex.Message}");
                return 2;
            }

            Console.WriteLine($"validate: {settings.Describe()}");
            Console.WriteLine();

            // --- V3 SDK Gateway -------------------------------------------
            long sdkGwBytes;
            int sdkGwHttp;
            try
            {
                using var sdk = new CosmosClient(
                    settings.Endpoint, settings.Key,
                    new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        ApplicationName = "cosmos-bench-validate-gw",
                    });
                Container container = sdk.GetDatabase(settings.Database).GetContainer(settings.Container);
                using ResponseMessage rm = await container
                    .ReadItemStreamAsync(settings.ItemId, new PartitionKey(settings.PartitionKey))
                    .ConfigureAwait(false);
                sdkGwHttp = (int)rm.StatusCode;
                using var ms = new MemoryStream();
                if (rm.Content != null) await rm.Content.CopyToAsync(ms).ConfigureAwait(false);
                sdkGwBytes = ms.Length;
                Console.WriteLine($"[V3 SDK Gateway] http={sdkGwHttp} bytes={sdkGwBytes} ru={rm.Headers?.RequestCharge:F2} activityId={rm.Headers?.ActivityId}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — V3 SDK Gateway: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }

            // --- V3 SDK Direct --------------------------------------------
            long sdkDirBytes;
            int sdkDirHttp;
            try
            {
                using var sdk = new CosmosClient(
                    settings.Endpoint, settings.Key,
                    new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Direct,
                        ApplicationName = "cosmos-bench-validate-dir",
                    });
                Container container = sdk.GetDatabase(settings.Database).GetContainer(settings.Container);
                using ResponseMessage rm = await container
                    .ReadItemStreamAsync(settings.ItemId, new PartitionKey(settings.PartitionKey))
                    .ConfigureAwait(false);
                sdkDirHttp = (int)rm.StatusCode;
                using var ms = new MemoryStream();
                if (rm.Content != null) await rm.Content.CopyToAsync(ms).ConfigureAwait(false);
                sdkDirBytes = ms.Length;
                Console.WriteLine($"[V3 SDK Direct]  http={sdkDirHttp} bytes={sdkDirBytes} ru={rm.Headers?.RequestCharge:F2} activityId={rm.Headers?.ActivityId}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — V3 SDK Direct: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine("  Hint: Direct mode requires outbound TCP to backend replicas (ephemeral port range).");
                Console.Error.WriteLine("        If you're behind a restrictive firewall, fall back to Gateway-only.");
                return 1;
            }

            // --- Native driver --------------------------------------------
            long nativeBytes;
            int nativeHttp;
            try
            {
                using var native = new NativeCosmosClient(
                    settings.Endpoint, settings.Key,
                    settings.Database, settings.Container, settings.PartitionKey,
                    userAgentSuffix: "cosmos-bench-validate");
                CosmosNativeResponse r = await native.ReadItemAsync(settings.ItemId).ConfigureAwait(false);
                nativeHttp = r.HttpStatusCode;
                nativeBytes = r.Body.LongLength;
                Console.WriteLine($"[Native driver]  http={nativeHttp} bytes={nativeBytes} ru={r.RequestCharge:F2} activityId={r.ActivityId}");
            }
            catch (DllNotFoundException ex)
            {
                Console.Error.WriteLine($"FAIL — Native driver: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine("  Cause: azurecosmosdriver.dll is not on the probing path.");
                Console.Error.WriteLine("  Build it: pwsh ..\\Microsoft.Azure.Cosmos.NativeDriverPoc\\scripts\\build-native-dll.ps1");
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — Native driver: {ex.GetType().Name}: {ex.Message}");
                if (ex.Message.Contains("InvalidOptionValue", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine("  Hint: the value passed to that FFI option violates the driver's contract");
                    Console.Error.WriteLine("        (e.g. user-agent suffix must be <=25 chars / [A-Za-z0-9._~-]).");
                }
                return 1;
            }

            Console.WriteLine();
            if (sdkGwHttp == 200 && sdkDirHttp == 200 && nativeHttp == 200 &&
                sdkGwBytes == nativeBytes && sdkDirBytes == nativeBytes)
            {
                Console.WriteLine($"PASS — all three paths returned HTTP 200 with {nativeBytes} bytes.");
                return 0;
            }

            Console.Error.WriteLine(
                $"MISMATCH — SDK Gateway: http={sdkGwHttp} bytes={sdkGwBytes} / " +
                $"SDK Direct: http={sdkDirHttp} bytes={sdkDirBytes} / " +
                $"Native: http={nativeHttp} bytes={nativeBytes}");
            return 1;
        }

        /// <summary>
        /// Full-CRUD sanity. For each of the three modes, runs a complete
        /// CREATE → READ → REPLACE → READ → DELETE cycle against a freshly
        /// generated id, then cleans up. Verifies the entire write surface
        /// the CRUD benchmarks need, BEFORE incurring their RU cost.
        ///
        /// Per cycle:
        ///   CREATE expects 201, READ expects 200 with version=1,
        ///   REPLACE expects 200, READ expects 200 with version=2,
        ///   DELETE expects 204.
        ///
        /// Failures inside a cycle attempt a best-effort delete; a leaked
        /// doc will have id <c>validate-{mode}-{guid}</c>.
        /// </summary>
        private static async Task<int> ValidateCrudAsync()
        {
            BenchmarkSettings settings;
            try
            {
                settings = BenchmarkSettings.FromEnvironment();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — configuration: {ex.Message}");
                return 2;
            }

            Console.WriteLine($"validate --crud: {settings.Describe()}");
            Console.WriteLine();

            string runGuid = Guid.NewGuid().ToString("N").Substring(0, 8);
            int failures = 0;

            // --- V3 SDK Gateway -------------------------------------------
            try
            {
                using var sdk = new CosmosClient(
                    settings.Endpoint, settings.Key,
                    new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        ApplicationName = "cosmos-bench-validate-gw",
                        EnableContentResponseOnWrite = false,
                    });
                Container c = sdk.GetDatabase(settings.Database).GetContainer(settings.Container);
                await RunSdkCycleAsync(c, $"validate-gw-{runGuid}", "V3 SDK Gateway").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — V3 SDK Gateway CRUD: {ex.GetType().Name}: {ex.Message}");
                failures++;
            }

            // --- V3 SDK Direct --------------------------------------------
            try
            {
                using var sdk = new CosmosClient(
                    settings.Endpoint, settings.Key,
                    new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Direct,
                        ApplicationName = "cosmos-bench-validate-dir",
                        EnableContentResponseOnWrite = false,
                    });
                Container c = sdk.GetDatabase(settings.Database).GetContainer(settings.Container);
                await RunSdkCycleAsync(c, $"validate-dir-{runGuid}", "V3 SDK Direct").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — V3 SDK Direct CRUD: {ex.GetType().Name}: {ex.Message}");
                failures++;
            }

            // --- Native driver --------------------------------------------
            try
            {
                string id = $"validate-nat-{runGuid}";
                // Partition-of-one for the sample. Native ctor fixes pk.
                using var native = new NativeCosmosClient(
                    settings.Endpoint, settings.Key,
                    settings.Database, settings.Container, id,
                    userAgentSuffix: "cosmos-bench-validate");
                await RunNativeCycleAsync(native, id, "Native driver").ConfigureAwait(false);
            }
            catch (DllNotFoundException ex)
            {
                Console.Error.WriteLine($"FAIL — Native driver CRUD: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine("  Cause: azurecosmosdriver.dll is not on the probing path.");
                Console.Error.WriteLine("  Build it: pwsh ..\\Microsoft.Azure.Cosmos.NativeDriverPoc\\scripts\\build-native-dll.ps1");
                failures++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — Native driver CRUD: {ex.GetType().Name}: {ex.Message}");
                failures++;
            }

            Console.WriteLine();
            if (failures == 0)
            {
                Console.WriteLine("PASS — all three modes completed CREATE/READ/REPLACE/READ/DELETE.");
                return 0;
            }
            Console.Error.WriteLine($"FAIL — {failures}/3 modes had errors.");
            return 1;
        }

        private static async Task RunSdkCycleAsync(Container c, string id, string label)
        {
            // Partition-of-one — pk equals id. Matches the CrudSample style.
            var pk = new PartitionKey(id);
            string body1 = "{\"id\":\"" + id + "\",\"pk\":\"" + id + "\",\"message\":\"validate\",\"version\":1}";
            string body2 = "{\"id\":\"" + id + "\",\"pk\":\"" + id + "\",\"message\":\"validate (updated)\",\"version\":2}";
            byte[] bytes1 = System.Text.Encoding.UTF8.GetBytes(body1);
            byte[] bytes2 = System.Text.Encoding.UTF8.GetBytes(body2);

            try
            {
                using (var ms = new MemoryStream(bytes1))
                using (ResponseMessage rm = await c.CreateItemStreamAsync(ms, pk).ConfigureAwait(false))
                {
                    if ((int)rm.StatusCode != 201) throw new InvalidOperationException($"[{label}] CREATE expected 201 got {(int)rm.StatusCode}");
                    Console.WriteLine($"[{label}] CREATE  http={(int)rm.StatusCode} ru={rm.Headers?.RequestCharge:F2}");
                }

                using (ResponseMessage rm = await c.ReadItemStreamAsync(id, pk).ConfigureAwait(false))
                {
                    if ((int)rm.StatusCode != 200) throw new InvalidOperationException($"[{label}] READ#1 expected 200 got {(int)rm.StatusCode}");
                    Console.WriteLine($"[{label}] READ#1  http={(int)rm.StatusCode} ru={rm.Headers?.RequestCharge:F2}");
                }

                using (var ms = new MemoryStream(bytes2))
                using (ResponseMessage rm = await c.ReplaceItemStreamAsync(ms, id, pk).ConfigureAwait(false))
                {
                    if ((int)rm.StatusCode != 200) throw new InvalidOperationException($"[{label}] REPLACE expected 200 got {(int)rm.StatusCode}");
                    Console.WriteLine($"[{label}] REPLACE http={(int)rm.StatusCode} ru={rm.Headers?.RequestCharge:F2}");
                }

                using (ResponseMessage rm = await c.ReadItemStreamAsync(id, pk).ConfigureAwait(false))
                {
                    if ((int)rm.StatusCode != 200) throw new InvalidOperationException($"[{label}] READ#2 expected 200 got {(int)rm.StatusCode}");
                    Console.WriteLine($"[{label}] READ#2  http={(int)rm.StatusCode} ru={rm.Headers?.RequestCharge:F2}");
                }

                using (ResponseMessage rm = await c.DeleteItemStreamAsync(id, pk).ConfigureAwait(false))
                {
                    if ((int)rm.StatusCode != 204) throw new InvalidOperationException($"[{label}] DELETE expected 204 got {(int)rm.StatusCode}");
                    Console.WriteLine($"[{label}] DELETE  http={(int)rm.StatusCode} ru={rm.Headers?.RequestCharge:F2}");
                }
            }
            catch
            {
                // Best-effort cleanup.
                try { (await c.DeleteItemStreamAsync(id, pk).ConfigureAwait(false)).Dispose(); } catch { }
                throw;
            }
        }

        private static async Task RunNativeCycleAsync(NativeCosmosClient native, string id, string label)
        {
            string body1 = "{\"id\":\"" + id + "\",\"pk\":\"" + id + "\",\"message\":\"validate\",\"version\":1}";
            string body2 = "{\"id\":\"" + id + "\",\"pk\":\"" + id + "\",\"message\":\"validate (updated)\",\"version\":2}";

            try
            {
                CosmosNativeResponse r;

                r = await native.CreateItemAsync(id, body1).ConfigureAwait(false);
                if (r.HttpStatusCode != 201) throw new InvalidOperationException($"[{label}] CREATE expected 201 got {r.HttpStatusCode}");
                Console.WriteLine($"[{label}] CREATE  http={r.HttpStatusCode} ru={r.RequestCharge:F2}");

                r = await native.ReadItemAsync(id).ConfigureAwait(false);
                if (r.HttpStatusCode != 200) throw new InvalidOperationException($"[{label}] READ#1 expected 200 got {r.HttpStatusCode}");
                Console.WriteLine($"[{label}] READ#1  http={r.HttpStatusCode} ru={r.RequestCharge:F2}");

                r = await native.ReplaceItemAsync(id, body2).ConfigureAwait(false);
                if (r.HttpStatusCode != 200) throw new InvalidOperationException($"[{label}] REPLACE expected 200 got {r.HttpStatusCode}");
                Console.WriteLine($"[{label}] REPLACE http={r.HttpStatusCode} ru={r.RequestCharge:F2}");

                r = await native.ReadItemAsync(id).ConfigureAwait(false);
                if (r.HttpStatusCode != 200) throw new InvalidOperationException($"[{label}] READ#2 expected 200 got {r.HttpStatusCode}");
                Console.WriteLine($"[{label}] READ#2  http={r.HttpStatusCode} ru={r.RequestCharge:F2}");

                r = await native.DeleteItemAsync(id).ConfigureAwait(false);
                if (r.HttpStatusCode != 204) throw new InvalidOperationException($"[{label}] DELETE expected 204 got {r.HttpStatusCode}");
                Console.WriteLine($"[{label}] DELETE  http={r.HttpStatusCode} ru={r.RequestCharge:F2}");
            }
            catch
            {
                try { await native.DeleteItemAsync(id).ConfigureAwait(false); } catch { }
                throw;
            }
        }

        /// <summary>
        /// Query-shape sanity. Seeds N small docs into a fresh PK, runs
        /// the same parameterized single-partition query through each
        /// of the three drivers in both shapes (single-page MaxItemCount
        /// large enough for the full set, and paginated MaxItemCount=2),
        /// asserts the doc counts match, then deletes the seed. Mirrors
        /// the query-benchmark's GlobalSetup minus the warm-up loop.
        /// </summary>
        private static async Task<int> ValidateQueryAsync()
        {
            const int SeedCount = 5;
            const int SinglePageMaxItems = 20;
            const int PaginatedMaxItems = 2;
            const string QueryText = "SELECT * FROM c WHERE c.tag = @tag";

            BenchmarkSettings settings;
            try
            {
                settings = BenchmarkSettings.FromEnvironment();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — configuration: {ex.Message}");
                return 2;
            }

            Console.WriteLine($"validate --query: {settings.Describe()}");
            Console.WriteLine();

            string runGuid = Guid.NewGuid().ToString("N").Substring(0, 8);
            string pk = $"validate-q-pk-{runGuid}";
            string tag = $"validate-q-{runGuid}";
            var sdkPk = new PartitionKey(pk);

            // Seed using SDK Gateway up front — independent of the
            // path-under-test so per-mode failures are isolated.
            CosmosClient seedClient = new CosmosClient(
                settings.Endpoint, settings.Key,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ApplicationName = "cosmos-bench-validate-q-seed",
                    EnableContentResponseOnWrite = false,
                });
            Container seedContainer = seedClient.GetDatabase(settings.Database).GetContainer(settings.Container);
            var seededIds = new string[SeedCount];
            try
            {
                for (int i = 0; i < SeedCount; i++)
                {
                    string id = $"{tag}-doc-{i:D2}";
                    seededIds[i] = id;
                    string body = "{\"id\":\"" + id + "\",\"pk\":\"" + pk + "\",\"tag\":\"" + tag + "\",\"ordinal\":" + i + "}";
                    using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
                    using ResponseMessage rm = await seedContainer
                        .CreateItemStreamAsync(ms, sdkPk).ConfigureAwait(false);
                    if ((int)rm.StatusCode != 201)
                    {
                        Console.Error.WriteLine($"FAIL — seed: HTTP {(int)rm.StatusCode} for id={id}");
                        return 1;
                    }
                }
                Console.WriteLine($"[seed] {SeedCount} docs into pk='{pk}' tag='{tag}'");
                Console.WriteLine();

                int failures = 0;

                // --- V3 SDK Gateway -----------------------------------
                try
                {
                    await RunSdkQueryShapesAsync(
                        seedContainer, sdkPk, tag,
                        SinglePageMaxItems, PaginatedMaxItems, SeedCount,
                        "V3 SDK Gateway").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"FAIL — V3 SDK Gateway query: {ex.GetType().Name}: {ex.Message}");
                    failures++;
                }

                // --- V3 SDK Direct ------------------------------------
                try
                {
                    using var sdk = new CosmosClient(
                        settings.Endpoint, settings.Key,
                        new CosmosClientOptions
                        {
                            ConnectionMode = ConnectionMode.Direct,
                            ApplicationName = "cosmos-bench-validate-q-dir",
                            EnableContentResponseOnWrite = false,
                        });
                    Container c = sdk.GetDatabase(settings.Database).GetContainer(settings.Container);
                    await RunSdkQueryShapesAsync(
                        c, sdkPk, tag,
                        SinglePageMaxItems, PaginatedMaxItems, SeedCount,
                        "V3 SDK Direct").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"FAIL — V3 SDK Direct query: {ex.GetType().Name}: {ex.Message}");
                    failures++;
                }

                // --- Native driver ------------------------------------
                try
                {
                    using var native = new NativeCosmosClient(
                        settings.Endpoint, settings.Key,
                        settings.Database, settings.Container, pk,
                        userAgentSuffix: "cosmos-bench-vq");
                    await RunNativeQueryShapesAsync(
                        native, QueryText, tag,
                        SinglePageMaxItems, PaginatedMaxItems, SeedCount,
                        "Native driver").ConfigureAwait(false);
                }
                catch (DllNotFoundException ex)
                {
                    Console.Error.WriteLine($"FAIL — Native driver query: {ex.GetType().Name}: {ex.Message}");
                    Console.Error.WriteLine("  Cause: azurecosmosdriver.dll is not on the probing path.");
                    failures++;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"FAIL — Native driver query: {ex.GetType().Name}: {ex.Message}");
                    failures++;
                }

                Console.WriteLine();
                if (failures == 0)
                {
                    Console.WriteLine("PASS — all three modes returned the expected doc counts in both shapes.");
                    return 0;
                }
                Console.Error.WriteLine($"FAIL — {failures}/3 modes had query errors.");
                return 1;
            }
            finally
            {
                // Best-effort cleanup.
                for (int i = 0; i < seededIds.Length; i++)
                {
                    if (seededIds[i] == null) continue;
                    try
                    {
                        (await seedContainer.DeleteItemStreamAsync(seededIds[i]!, sdkPk).ConfigureAwait(false)).Dispose();
                    }
                    catch
                    {
                        // leaked docs share the validate-q-* prefix
                    }
                }
                seedClient.Dispose();
            }
        }

        private static async Task RunSdkQueryShapesAsync(
            Container container, PartitionKey pk, string tag,
            int singlePageMaxItems, int paginatedMaxItems, int expectedCount,
            string label)
        {
            QueryDefinition Def() =>
                new QueryDefinition("SELECT * FROM c WHERE c.tag = @tag").WithParameter("@tag", tag);
            QueryRequestOptions Opts(int n) =>
                new QueryRequestOptions { PartitionKey = pk, MaxItemCount = n };

            int singleDocs;
            using (FeedIterator iter = container.GetItemQueryStreamIterator(Def(), null, Opts(singlePageMaxItems)))
            {
                if (!iter.HasMoreResults)
                {
                    throw new InvalidOperationException($"[{label}] SinglePage: iterator empty");
                }
                using ResponseMessage rm = await iter.ReadNextAsync().ConfigureAwait(false);
                if ((int)rm.StatusCode != 200)
                {
                    throw new InvalidOperationException($"[{label}] SinglePage HTTP {(int)rm.StatusCode}");
                }
                singleDocs = CountDocumentsFromStream(rm.Content);
                Console.WriteLine($"[{label}] SinglePage  http={(int)rm.StatusCode} docs={singleDocs} ru={rm.Headers?.RequestCharge:F2}");
            }
            if (singleDocs != expectedCount)
            {
                throw new InvalidOperationException($"[{label}] SinglePage expected {expectedCount} docs, got {singleDocs}");
            }

            int paginatedDocs = 0;
            int pages = 0;
            double pageRu = 0;
            using (FeedIterator iter = container.GetItemQueryStreamIterator(Def(), null, Opts(paginatedMaxItems)))
            {
                while (iter.HasMoreResults)
                {
                    using ResponseMessage rm = await iter.ReadNextAsync().ConfigureAwait(false);
                    if ((int)rm.StatusCode != 200)
                    {
                        throw new InvalidOperationException($"[{label}] Paginated page {pages + 1} HTTP {(int)rm.StatusCode}");
                    }
                    pages++;
                    paginatedDocs += CountDocumentsFromStream(rm.Content);
                    pageRu += rm.Headers?.RequestCharge ?? 0;
                }
                Console.WriteLine($"[{label}] Paginated   pages={pages} totalDocs={paginatedDocs} ru={pageRu:F2}");
            }
            if (paginatedDocs != expectedCount)
            {
                throw new InvalidOperationException($"[{label}] Paginated expected {expectedCount} docs, got {paginatedDocs} across {pages} pages");
            }
        }

        private static async Task RunNativeQueryShapesAsync(
            NativeCosmosClient native, string queryText, string tag,
            int singlePageMaxItems, int paginatedMaxItems, int expectedCount,
            string label)
        {
            var parms = new (string Name, object? Value)[] { ("@tag", tag) };

            CosmosNativeResponse single = await native.QueryItemsPageAsync(
                queryText, null, singlePageMaxItems, parms).ConfigureAwait(false);
            if (single.HttpStatusCode != 200)
            {
                throw new InvalidOperationException($"[{label}] SinglePage HTTP {single.HttpStatusCode}");
            }
            int singleDocs = CountDocumentsFromBytes(single.Body);
            Console.WriteLine($"[{label}] SinglePage  http={single.HttpStatusCode} docs={singleDocs} ru={single.RequestCharge:F2}");
            if (singleDocs != expectedCount)
            {
                throw new InvalidOperationException($"[{label}] SinglePage expected {expectedCount} docs, got {singleDocs}");
            }

            int paginatedDocs = 0;
            int pages = 0;
            double pageRu = 0;
            await foreach (CosmosNativeResponse page in native.QueryItemsAsync(
                queryText, paginatedMaxItems, parms).ConfigureAwait(false))
            {
                pages++;
                paginatedDocs += CountDocumentsFromBytes(page.Body);
                pageRu += page.RequestCharge;
            }
            Console.WriteLine($"[{label}] Paginated   pages={pages} totalDocs={paginatedDocs} ru={pageRu:F2}");
            if (paginatedDocs != expectedCount)
            {
                throw new InvalidOperationException($"[{label}] Paginated expected {expectedCount} docs, got {paginatedDocs} across {pages} pages");
            }
        }

        private static int CountDocumentsFromStream(Stream? content)
        {
            if (content == null) return 0;
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            return CountDocumentsFromBytes(ms.ToArray());
        }

        private static int CountDocumentsFromBytes(byte[] body)
        {
            if (body == null || body.Length == 0) return 0;
            try
            {
                using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("Documents", out System.Text.Json.JsonElement docs)
                    && docs.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    return docs.GetArrayLength();
                }
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    return doc.RootElement.GetArrayLength();
                }
                return 0;
            }
            catch (System.Text.Json.JsonException)
            {
                return 0;
            }
        }
    }
}
