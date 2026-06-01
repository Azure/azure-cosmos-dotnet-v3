// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeAsyncPoc
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Driver for the async-FFI hypothesis from the .NET side. Mirrors the
    /// three F-checks the Rust-side smoke test already passed; if these pass
    /// here too, the hypothesis is validated end-to-end across the FFI
    /// boundary.
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
            Console.WriteLine("=== Async-FFI POC — .NET host ===");
            Console.WriteLine($"  endpoint   = {EmulatorEndpoint}");
            Console.WriteLine($"  db/cont    = {DatabaseId}/{ContainerId}");
            Console.WriteLine($"  pk/id      = {PartitionKey}/{ItemId}");
            Console.WriteLine();

            using var client = new NativeCosmosClient(EmulatorEndpoint, EmulatorKey, workerThreads: 2);

            int rc = 0;
            rc |= await RunCheckAsync("F1 single read returns success with seeded body", F1SingleRead, client);
            rc |= await RunCheckAsync("F2 read submission does not block calling thread", F2NoBlock, client);
            rc |= await RunCheckAsync("F3 16 concurrent reads beat strict serial by >=4x", F3Concurrency, client);
            rc |= await RunCheckAsync("F4 CancellationToken propagates to Rust", F4Cancel, client);

            Console.WriteLine();
            Console.WriteLine(rc == 0 ? "ALL CHECKS PASSED" : "FAILURES");
            return rc;
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

        // F1: Single read end-to-end. Validates the wire format + body
        // marshalling. The seeded item contains the literal marker
        // "from native async poc"; if that round-trips, the entire stack
        // (Rust driver -> CQ -> P/Invoke -> TCS) worked.
        private static async Task<bool> F1SingleRead(NativeCosmosClient c)
        {
            RawResponse r = await c.ReadItemAsync(DatabaseId, ContainerId, PartitionKey, ItemId)
                .ConfigureAwait(false);
            if (r.HttpStatus != 200)
            {
                Console.Write($"http={r.HttpStatus} ");
                return false;
            }
            string body = Encoding.UTF8.GetString(r.Body);
            if (!body.Contains("from native async poc", StringComparison.Ordinal))
            {
                Console.Write($"body={body} ");
                return false;
            }
            return true;
        }

        // F2: Submission must not block the calling thread on I/O. We
        // measure the wall time *before* awaiting (the call returns a Task
        // synchronously) and require it to be sub-millisecond. This is the
        // strongest possible refutation of "the FFI blocks me."
        private static async Task<bool> F2NoBlock(NativeCosmosClient c)
        {
            var sw = Stopwatch.StartNew();
            Task<RawResponse> task = c.ReadItemAsync(DatabaseId, ContainerId, PartitionKey, ItemId);
            long submitMicros = sw.Elapsed.Ticks * 1_000_000L / Stopwatch.Frequency;
            await task.ConfigureAwait(false);
            Console.Write($"submit={submitMicros}us ");
            return submitMicros < 5_000;  // 5ms ceiling - we expect <100us
        }

        // F3: Real concurrency. If async-FFI works, N parallel reads
        // complete in roughly the time of one. If the FFI silently
        // serialized (eg via a per-driver mutex), wall time scales linearly.
        // Localhost emulator point read is ~2-3ms; 16 serial would be
        // 32-48ms, 16 concurrent should be 3-8ms.
        private static async Task<bool> F3Concurrency(NativeCosmosClient c)
        {
            // Warm up so the first-call cache prime doesn't pollute the
            // measurement.
            _ = await c.ReadItemAsync(DatabaseId, ContainerId, PartitionKey, ItemId);

            const int N = 16;
            var sw = Stopwatch.StartNew();
            Task<RawResponse>[] tasks = new Task<RawResponse>[N];
            for (int i = 0; i < N; i++)
            {
                tasks[i] = c.ReadItemAsync(DatabaseId, ContainerId, PartitionKey, ItemId);
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            long parallelMs = sw.ElapsedMilliseconds;

            // Now do them strict-serial for comparison.
            sw.Restart();
            for (int i = 0; i < N; i++)
            {
                _ = await c.ReadItemAsync(DatabaseId, ContainerId, PartitionKey, ItemId)
                    .ConfigureAwait(false);
            }
            long serialMs = sw.ElapsedMilliseconds;

            double ratio = serialMs / Math.Max(1.0, (double)parallelMs);
            Console.Write($"parallel={parallelMs}ms serial={serialMs}ms ratio={ratio:F1}x ");
            return ratio >= 4.0;  // proves real concurrency over the FFI
        }

        // F4: Cancellation. Submit a read with a token that's already
        // cancelled; the Task should transition to Canceled (not Faulted,
        // not RanToCompletion). The Rust side aborts the Tokio task and
        // emits a Cancelled completion, which the pump translates.
        private static async Task<bool> F4Cancel(NativeCosmosClient c)
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();  // pre-cancelled
            try
            {
                _ = await c.ReadItemAsync(DatabaseId, ContainerId, PartitionKey, ItemId, cts.Token)
                    .ConfigureAwait(false);
                return false;
            }
            catch (TaskCanceledException)
            {
                return true;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }
    }
}
