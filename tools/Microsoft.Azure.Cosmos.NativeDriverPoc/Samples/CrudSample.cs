// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc.Samples
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// End-to-end CRUD walk-through against the native driver.
    ///
    /// Linear story:
    ///   1. CREATE  a freshly-id'd document
    ///   2. READ    to confirm it round-tripped
    ///   3. REPLACE the document with v2 payload
    ///   4. READ    to confirm the replacement is visible
    ///   5. DELETE  to leave the container as we found it
    ///
    /// Modelled on the V3 SDK ItemManagement samples and the
    /// <c>azure_data_cosmos</c> crate examples: one document, one
    /// partition, linear flow, narrated to stdout, idempotent across
    /// runs (each invocation uses a fresh GUID-suffixed id and deletes
    /// at the end).
    ///
    /// <para>
    /// What this sample intentionally does NOT do (those scenarios are
    /// already proven for ReadItem in the F-check harness and add no
    /// new coverage for the write/replace/delete code paths):
    ///   * F2NoBlock     — submit-doesn't-block timing
    ///   * F3Concurrency — 1000 concurrent operations on one pump
    ///   * F4Cancel      — CancellationToken → op_handle_cancel
    ///   * F5NotFound    — rich 404 surface
    /// </para>
    ///
    /// Configuration (env vars; emulator defaults if unset):
    ///   COSMOS_ENDPOINT   — account URI            (default: emulator)
    ///   COSMOS_KEY        — master key             (default: emulator well-known)
    ///   COSMOS_DATABASE   — database id            (default: pocdb)
    ///   COSMOS_CONTAINER  — container id           (default: items)
    ///
    /// The container's partition-key path is assumed to be <c>/pk</c>.
    /// Adjust the JSON literals below if your container uses a different path.
    /// </summary>
    internal static class CrudSample
    {
        private const string EmulatorEndpoint = "https://localhost:8081/";
        private const string EmulatorKey =
            "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        private const string DefaultDatabase = "pocdb";
        private const string DefaultContainer = "items";

        public static async Task<int> RunAsync()
        {
            string endpoint  = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT")  ?? EmulatorEndpoint;
            string masterKey = Environment.GetEnvironmentVariable("COSMOS_KEY")       ?? EmulatorKey;
            string database  = Environment.GetEnvironmentVariable("COSMOS_DATABASE")  ?? DefaultDatabase;
            string container = Environment.GetEnvironmentVariable("COSMOS_CONTAINER") ?? DefaultContainer;

            string itemId  = $"crud-sample-{Guid.NewGuid():N}";
            string pkValue = itemId;  // partition-of-one, easiest pattern for a sample

            Console.WriteLine("=== Native Driver — CRUD sample ===");
            Console.WriteLine($"  endpoint  : {endpoint}");
            Console.WriteLine($"  db / cont : {database} / {container}");
            Console.WriteLine($"  doc id    : {itemId}");
            Console.WriteLine($"  pk value  : {pkValue}  (assumes container PK path = /pk)");
            Console.WriteLine();

            using var client = new NativeCosmosClient(
                endpoint, masterKey, database, container, pkValue,
                userAgentSuffix: "cosmos-crud-demo");

            string bodyV1 =
                $$"""{"id":"{{itemId}}","pk":"{{pkValue}}","message":"hello, native driver","version":1}""";
            string bodyV2 =
                $$"""{"id":"{{itemId}}","pk":"{{pkValue}}","message":"hello, native driver (updated)","version":2}""";

            try
            {
                await Step("CREATE ", () => client.CreateItemAsync(itemId, bodyV1)).ConfigureAwait(false);
                await Step("READ   ", () => client.ReadItemAsync(itemId), printBody: true).ConfigureAwait(false);
                await Step("REPLACE", () => client.ReplaceItemAsync(itemId, bodyV2)).ConfigureAwait(false);
                await Step("READ   ", () => client.ReadItemAsync(itemId), printBody: true).ConfigureAwait(false);
                await Step("DELETE ", () => client.DeleteItemAsync(itemId)).ConfigureAwait(false);

                Console.WriteLine();
                Console.WriteLine("[done] CRUD cycle complete.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"[error] {ex.GetType().Name}: {ex.Message}");

                // Best-effort cleanup so a partial-run failure doesn't leak documents
                // into the container. Swallow secondary failures — the primary error
                // above is what the user needs to act on.
                try
                {
                    await client.DeleteItemAsync(itemId).ConfigureAwait(false);
                    Console.Error.WriteLine($"[cleanup] deleted '{itemId}' after failure.");
                }
                catch
                {
                    // ignored — doc may not have been created, or container is unreachable
                }

                return 1;
            }
        }

        private static async Task Step(
            string label,
            Func<Task<CosmosNativeResponse>> op,
            bool printBody = false)
        {
            CosmosNativeResponse r = await op().ConfigureAwait(false);
            Console.WriteLine(
                $"[{label}] http={r.HttpStatusCode}  ru={r.RequestCharge,5:F2}  bytes={r.Body.Length,5}  activityId={r.ActivityId}");
            if (printBody)
            {
                Console.WriteLine($"           body: {r.BodyAsString()}");
            }
        }
    }
}
