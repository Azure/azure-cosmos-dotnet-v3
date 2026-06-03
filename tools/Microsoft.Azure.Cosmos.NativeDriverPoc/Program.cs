// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// V2 driver harness against the actual PR #4515 ABI. Same F1-F5
    /// envelope as the earlier draft, signatures adjusted to the real
    /// surface (item-CRUD factories, partition-key builder API, etc.).
    /// </summary>
    internal static class Program
    {
        private const string EmulatorEndpoint = "https://localhost:8081/";
        private const string EmulatorKey =
            "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        private const string DatabaseId = "pocdb";
        private const string ContainerId = "items";
        private const string PartitionKey = "p";
        private const string ItemId = "x";

        public static async Task<int> Main()
        {
            Console.WriteLine("=== Async-FFI POC V2 — PR #4515 .NET host ===");
            Console.WriteLine($"  endpoint   = {EmulatorEndpoint}");
            Console.WriteLine($"  db/cont    = {DatabaseId}/{ContainerId}");
            Console.WriteLine($"  pk/id      = {PartitionKey}/{ItemId}");
            Console.WriteLine($"  dll        = {NativeMethods.LibraryName}.dll");
            Console.WriteLine();

            if (!PreflightDll())
            {
                return 2;
            }

            string? versionString = null;
            try
            {
                IntPtr versionPtr = NativeMethods.cosmos_version();
                versionString = NativeMethods.PtrToUtf8(versionPtr);
            }
            catch (DllNotFoundException)
            {
                // Caught by preflight; should never reach here.
            }
            Console.WriteLine($"[bootstrap] cosmos_version() = {versionString ?? "(unavailable)"}");

            using var client = new NativeCosmosClient(
                EmulatorEndpoint, EmulatorKey,
                DatabaseId, ContainerId, PartitionKey);

            int rc = 0;
            rc |= await RunCheckAsync("F1 single read returns success with seeded body", F1SingleRead, client);
            rc |= await RunCheckAsync("F2 submit does not block calling thread (avg of 1000)", F2NoBlock, client);
            rc |= await RunCheckAsync("F3 1000 concurrent reads on a single pump", F3Concurrency, client);
            rc |= await RunCheckAsync("F4 CancellationToken → op_handle_cancel honored", F4Cancel, client);
            rc |= await RunCheckAsync("F5 missing item surfaces rich 404 (IsNotFound=true)", F5NotFound, client);

            Console.WriteLine();
            Console.WriteLine(rc == 0 ? "ALL CHECKS PASSED" : "FAILURES");
            return rc;
        }

        private static bool PreflightDll()
        {
            string baseDir = AppContext.BaseDirectory;
            string dllName = NativeMethods.LibraryName + ".dll";
            string path = System.IO.Path.Combine(baseDir, dllName);
            if (System.IO.File.Exists(path))
            {
                Console.WriteLine($"[preflight] native library found: {path}");
                return true;
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine("==============================================================");
            Console.Error.WriteLine($"  {dllName} is NOT present next to this executable.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  This is expected if PR #4515 has not yet been built locally.");
            Console.Error.WriteLine("  To unblock the F-checks:");
            Console.Error.WriteLine();
            Console.Error.WriteLine("    1. git -C Q:\\src\\azure-sdk-for-rust fetch origin");
            Console.Error.WriteLine("         users/kundadebdatta/4372_cosmos_driver_native_crate_async_impl");
            Console.Error.WriteLine("    2. cargo build --release -p azure_data_cosmos_driver_native");
            Console.Error.WriteLine("    3. copy target\\release\\azurecosmosdriver.dll into");
            Console.Error.WriteLine($"       {baseDir}");
            Console.Error.WriteLine("       or set the MSBuild property DriverNativeArtifactDir.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("==============================================================");
            return false;
        }

        private static async Task<int> RunCheckAsync(
            string title, Func<NativeCosmosClient, Task<bool>> fn, NativeCosmosClient c)
        {
            Console.Write($"[ ?  ] {title} ... ");
            try
            {
                bool ok = await fn(c).ConfigureAwait(false);
                Console.WriteLine(ok ? "PASS" : "FAIL");
                return ok ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL — {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }

        private static async Task<bool> F1SingleRead(NativeCosmosClient c)
        {
            CosmosNativeResponse r = await c.ReadItemAsync(ItemId).ConfigureAwait(false);
            if (r.HttpStatusCode != 200)
            {
                Console.Write($"http={r.HttpStatusCode} ");
                return false;
            }
            string body = r.BodyAsString();
            if (!body.Contains("from native async poc", StringComparison.Ordinal))
            {
                Console.Write($"body={body} ");
                return false;
            }
            Console.Write($"http=200 ru={r.RequestCharge:F2} bodylen={r.Body.Length} ");
            return true;
        }

        private static async Task<bool> F2NoBlock(NativeCosmosClient c)
        {
            await c.ReadItemAsync(ItemId).ConfigureAwait(false);  // warm up

            const int N = 1000;
            Task<CosmosNativeResponse>[] tasks = new Task<CosmosNativeResponse>[N];

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < N; i++)
            {
                tasks[i] = c.ReadItemAsync(ItemId);
            }
            long submitTotalTicks = sw.ElapsedTicks;
            await Task.WhenAll(tasks).ConfigureAwait(false);

            double avgMicros = (submitTotalTicks * 1_000_000.0) / Stopwatch.Frequency / N;
            Console.Write($"avg={avgMicros:F1}us ");
            return avgMicros < 100.0;
        }

        private static async Task<bool> F3Concurrency(NativeCosmosClient c)
        {
            await c.ReadItemAsync(ItemId).ConfigureAwait(false);  // warm up

            const int N = 1000;
            Task<CosmosNativeResponse>[] tasks = new Task<CosmosNativeResponse>[N];
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < N; i++)
            {
                tasks[i] = c.ReadItemAsync(ItemId);
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            long ms = sw.ElapsedMilliseconds;

            int ok = 0;
            for (int i = 0; i < N; i++)
            {
                if (tasks[i].Result.HttpStatusCode == 200) ok++;
            }
            Console.Write($"{ok}/{N} ok in {ms}ms ");
            return ok == N && ms < 5_000;
        }

        private static async Task<bool> F4Cancel(NativeCosmosClient c)
        {
            const int N = 100;
            int cancelled = 0, natural = 0;
            for (int i = 0; i < N; i++)
            {
                using var cts = new CancellationTokenSource();
                Task<CosmosNativeResponse> t = c.ReadItemAsync(ItemId, cts.Token);
                cts.Cancel();
                try
                {
                    _ = await t.ConfigureAwait(false);
                    natural++;
                }
                catch (TaskCanceledException) { cancelled++; }
                catch (OperationCanceledException) { cancelled++; }
            }
            Console.Write($"cancelled={cancelled}/natural={natural} (sum={cancelled + natural}/{N}) ");
            return cancelled + natural == N;
        }

        // F5: 404 surfaces as outcome=ERROR; coarse code = NotFound (2404).
        // Per PR #4515 the dropped spec-draft is_not_found() predicate is
        // replaced by comparing CoarseCode against the enum band.
        private static async Task<bool> F5NotFound(NativeCosmosClient c)
        {
            try
            {
                _ = await c.ReadItemAsync("definitely-does-not-exist-" + Guid.NewGuid().ToString("N"))
                    .ConfigureAwait(false);
                Console.Write("(expected exception, got success) ");
                return false;
            }
            catch (CosmosNativeException ex)
            {
                Console.Write($"code={ex.CoarseCode} http={ex.HttpStatusCode} isNotFound={ex.IsNotFound} ");
                return ex.HttpStatusCode == 404 && ex.IsNotFound;
            }
        }
    }
}
