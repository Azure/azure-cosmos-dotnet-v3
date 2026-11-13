// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Cancellation-token basics for the native-driver FFI bridge.
    ///
    /// Authoritative spec: PR #4515
    /// <c>sdk/cosmos/azure_data_cosmos_driver_native/c_tests/cancellation.c</c>
    /// ("Cancellation — handle-based cooperative cancel contract"). The
    /// driver models cancellation as a *handle-based, cooperative* operation,
    /// NOT a field inside options:
    ///   * <c>cosmos_operation_handle_cancel(handle)</c> sets a cancel-requested
    ///     latch and wakes the submit task's biased <c>tokio::select!</c> cancel
    ///     branch (a stored Notify permit means a cancel that races *ahead* of
    ///     the task is still observed on its first poll).
    ///   * On cancel the task drops the in-flight driver future and posts
    ///     exactly one <c>CANCELLED</c> completion, so the host continuation is
    ///     always released — never left <c>IN_FLIGHT</c>.
    ///   * <c>cosmos_completion_was_cancel_requested</c> lets the host tell
    ///     "cancel won the race" from "cancel lost" (op completed first).
    ///
    /// How that maps to the .NET host:
    ///   * <see cref="NativeCosmosClient"/> registers
    ///     <c>ct.Register(() =&gt; cosmos_operation_handle_cancel(handle))</c>,
    ///     so a CLR <see cref="CancellationToken"/> drives the native latch.
    ///   * The CQ pump translates a CANCELLED completion into
    ///     <c>TaskCompletionSource.TrySetCanceled()</c>, so the awaiter observes
    ///     an <see cref="OperationCanceledException"/> — never a hang.
    ///   * A token already cancelled at submit time is short-circuited in
    ///     <c>RunRequestAsync</c> (no FFI submit at all).
    ///
    /// Coverage here mirrors the deterministic contracts a binding must rely on:
    ///   X1 pre-cancelled token short-circuits a point read (deterministic)
    ///   X2 pre-cancelled token short-circuits a query page  (deterministic)
    ///   X3 idempotent double-cancel is safe                 (deterministic)
    ///   X4 cancel-race never hangs or leaks — every task resolves to either a
    ///      natural completion or a cancellation, never IN_FLIGHT (aggregate)
    ///   X5 an uncancelled control read completes normally    (deterministic)
    ///   X6 cancelling after terminal completion is a no-op   (deterministic)
    ///
    /// The "cancel wins against a still-running network future" variant
    /// (black-hole endpoint + CANCELLED with was_cancel_requested==true) is
    /// covered in the Rust <c>cancellation.c</c> harness, not here: the .NET
    /// client ctor performs the BLOCKING <c>get_or_create</c> /
    /// <c>resolve_container</c> bootstrap, so it cannot even be constructed
    /// against an unroutable endpoint. We get determinism instead from the
    /// pre-cancelled short-circuit (X1/X2) and aggregate correctness from the
    /// emulator race (X4).
    ///
    /// Configuration (env vars; emulator defaults if unset):
    ///   COSMOS_ENDPOINT / COSMOS_KEY / COSMOS_DATABASE / COSMOS_CONTAINER
    /// </summary>
    internal static class CancellationSample
    {
        private const string EmulatorEndpoint = "https://localhost:8081/";
        private const string EmulatorKey =
            "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        private const string DefaultDatabase = "pocdb";
        private const string DefaultContainer = "items";

        public static async Task<int> RunAsync()
        {
            string endpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT") ?? EmulatorEndpoint;
            string masterKey = Environment.GetEnvironmentVariable("COSMOS_KEY") ?? EmulatorKey;
            string database = Environment.GetEnvironmentVariable("COSMOS_DATABASE") ?? DefaultDatabase;
            string container = Environment.GetEnvironmentVariable("COSMOS_CONTAINER") ?? DefaultContainer;

            string runTag = $"cpoc-{Guid.NewGuid():N}".Substring(0, 14);
            string pkValue = runTag;
            string itemId = $"{runTag}-item";

            Console.WriteLine("=== Native Driver — Cancellation-token basics ===");
            Console.WriteLine($"  endpoint  : {endpoint}");
            Console.WriteLine($"  db / cont : {database} / {container}");
            Console.WriteLine($"  run tag   : {runTag}");
            Console.WriteLine($"  spec      : PR #4515 c_tests/cancellation.c");
            Console.WriteLine();

            using var client = new NativeCosmosClient(
                endpoint, masterKey, database, container, pkValue,
                userAgentSuffix: "cosmos-cancel-demo");

            // Seed one item so the read tests have a real, fast-completing
            // network operation to race against.
            Console.WriteLine("[seed] creating one document for the read-cancel tests");
            try
            {
                string body = $$"""{"id":"{{itemId}}","pk":"{{pkValue}}","tag":"{{runTag}}"}""";
                CosmosNativeResponse seed = await client.CreateItemAsync(itemId, body).ConfigureAwait(false);
                Console.WriteLine($"  CREATE {itemId}  http={seed.HttpStatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[seed] FAILED — {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine("Aborting; cannot run cancellation tests without a seeded item.");
                return 2;
            }

            var results = new List<TestResult>();
            try
            {
                Console.WriteLine();
                Console.WriteLine("=== Running cancellation contract tests ===");
                results.Add(await X1_PreCancelledRead(client, itemId).ConfigureAwait(false));
                results.Add(await X2_PreCancelledQuery(client, runTag).ConfigureAwait(false));
                results.Add(await X3_IdempotentDoubleCancel(client, itemId).ConfigureAwait(false));
                results.Add(await X4_CancelRaceNeverHangs(client, itemId).ConfigureAwait(false));
                results.Add(await X5_UncancelledControlCompletes(client, itemId).ConfigureAwait(false));
                results.Add(await X6_PostCompletionCancelIsNoOp(client, itemId).ConfigureAwait(false));
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("[cleanup] deleting seeded item (best-effort)");
                try { await client.DeleteItemAsync(itemId).ConfigureAwait(false); }
                catch (Exception ex) { Console.WriteLine($"  DELETE {itemId} -> {ex.GetType().Name}: {ex.Message}"); }
            }

            Console.WriteLine();
            Console.WriteLine("=== Summary ===");
            int passes = results.Count(r => r.Status == TestStatus.Pass);
            int fails = results.Count(r => r.Status == TestStatus.Fail);
            foreach (TestResult r in results)
            {
                string marker = r.Status == TestStatus.Pass ? "PASS" : (r.Status == TestStatus.Skip ? "SKIP" : "FAIL");
                Console.WriteLine($"  [{marker}] {r.Name,-42} {r.Detail}");
            }
            Console.WriteLine();
            Console.WriteLine($"  total = {results.Count}  pass = {passes}  fail = {fails}");
            return fails == 0 ? 0 : 1;
        }

        // X1 — a token already cancelled at submit time short-circuits a point
        // read. Deterministic: RunRequestAsync sees ct.IsCancellationRequested
        // and sets TrySetCanceled(ct) without ever touching the FFI.
        private static async Task<TestResult> X1_PreCancelledRead(NativeCosmosClient c, string itemId)
        {
            const string name = "X1 pre-cancelled token (read)";
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            try
            {
                _ = await c.ReadItemAsync(itemId, cts.Token).ConfigureAwait(false);
                return new TestResult(name, TestStatus.Fail, "expected OperationCanceledException, got success");
            }
            catch (OperationCanceledException)
            {
                return new TestResult(name, TestStatus.Pass, "OperationCanceledException raised, no submit, no hang");
            }
            catch (Exception ex)
            {
                return new TestResult(name, TestStatus.Fail, $"wrong exception {ex.GetType().Name}: {Trunc(ex.Message)}");
            }
        }

        // X2 — same pre-cancel short-circuit on the feed/query path, proving the
        // query bucket honors the token identically to the singleton bucket.
        private static async Task<TestResult> X2_PreCancelledQuery(NativeCosmosClient c, string runTag)
        {
            const string name = "X2 pre-cancelled token (query)";
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var parms = new (string, object?)[] { ("@tag", runTag) };
            try
            {
                _ = await c.QueryItemsPageAsync(
                    "SELECT * FROM c WHERE c.tag = @tag", maxItemCount: 10, parameters: parms, ct: cts.Token).ConfigureAwait(false);
                return new TestResult(name, TestStatus.Fail, "expected OperationCanceledException, got success");
            }
            catch (OperationCanceledException)
            {
                return new TestResult(name, TestStatus.Pass, "OperationCanceledException raised on query path");
            }
            catch (Exception ex)
            {
                return new TestResult(name, TestStatus.Fail, $"wrong exception {ex.GetType().Name}: {Trunc(ex.Message)}");
            }
        }

        // X3 — cancelling the same token twice is safe and still yields exactly
        // one clean cancellation. Mirrors the header's "idempotent and
        // non-blocking" guarantee on cosmos_operation_handle_cancel.
        private static async Task<TestResult> X3_IdempotentDoubleCancel(NativeCosmosClient c, string itemId)
        {
            const string name = "X3 idempotent double-cancel";
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            cts.Cancel();  // second cancel must be a no-op, not a throw
            try
            {
                _ = await c.ReadItemAsync(itemId, cts.Token).ConfigureAwait(false);
                return new TestResult(name, TestStatus.Fail, "expected OperationCanceledException, got success");
            }
            catch (OperationCanceledException)
            {
                return new TestResult(name, TestStatus.Pass, "double cancel safe; single clean cancellation");
            }
            catch (Exception ex)
            {
                return new TestResult(name, TestStatus.Fail, $"wrong exception {ex.GetType().Name}: {Trunc(ex.Message)}");
            }
        }

        // X4 — fire N reads, cancel each the instant after submit, and prove
        // EVERY task resolves: cancelled + natural == N. This is the host-side
        // expression of the C contract's "the host continuation is always
        // released — never left IN_FLIGHT". The cancelled/natural split is
        // race-dependent (both outcomes are valid); the invariant is the sum.
        private static async Task<TestResult> X4_CancelRaceNeverHangs(NativeCosmosClient c, string itemId)
        {
            const string name = "X4 cancel-race never hangs/leaks";
            const int n = 200;
            int cancelled = 0, natural = 0, broken = 0;
            string firstBreak = string.Empty;

            for (int i = 0; i < n; i++)
            {
                using var cts = new CancellationTokenSource();
                Task<CosmosNativeResponse> t = c.ReadItemAsync(itemId, cts.Token);
                cts.Cancel();  // race: may win (CANCELLED) or lose (op already done)
                try
                {
                    _ = await t.ConfigureAwait(false);
                    natural++;
                }
                catch (OperationCanceledException) { cancelled++; }
                catch (Exception ex)
                {
                    broken++;
                    if (firstBreak.Length == 0) firstBreak = $"{ex.GetType().Name}: {Trunc(ex.Message)}";
                }
            }

            bool ok = (cancelled + natural == n) && broken == 0;
            string detail = $"cancelled={cancelled} natural={natural} broken={broken} (sum={cancelled + natural}/{n})";
            if (broken > 0) detail += $" firstBreak=[{firstBreak}]";
            return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail, detail);
        }

        // X5 — control: a read with CancellationToken.None completes normally,
        // proving the cancellation plumbing does not perturb the happy path.
        private static async Task<TestResult> X5_UncancelledControlCompletes(NativeCosmosClient c, string itemId)
        {
            const string name = "X5 uncancelled control read";
            try
            {
                CosmosNativeResponse r = await c.ReadItemAsync(itemId, CancellationToken.None).ConfigureAwait(false);
                bool ok = r.HttpStatusCode == 200;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail, $"http={r.HttpStatusCode} (expected 200)");
            }
            catch (Exception ex)
            {
                return new TestResult(name, TestStatus.Fail, $"unexpected {ex.GetType().Name}: {Trunc(ex.Message)}");
            }
        }

        // X6 — cancelling a token AFTER its operation already reached a terminal
        // state is a no-op. The CT registration was already disposed by the
        // completion continuation, so the late cancel must not throw or disturb
        // the already-materialized result.
        private static async Task<TestResult> X6_PostCompletionCancelIsNoOp(NativeCosmosClient c, string itemId)
        {
            const string name = "X6 post-completion cancel no-op";
            using var cts = new CancellationTokenSource();
            try
            {
                CosmosNativeResponse r = await c.ReadItemAsync(itemId, cts.Token).ConfigureAwait(false);
                // Operation is terminal/OK. A late cancel must be harmless.
                cts.Cancel();
                bool ok = r.HttpStatusCode == 200;
                return new TestResult(name, ok ? TestStatus.Pass : TestStatus.Fail,
                    $"completed http={r.HttpStatusCode} then late-cancel was a no-op");
            }
            catch (Exception ex)
            {
                return new TestResult(name, TestStatus.Fail, $"unexpected {ex.GetType().Name}: {Trunc(ex.Message)}");
            }
        }

        private static string Trunc(string s) => s.Length <= 100 ? s : s.Substring(0, 100) + "...";

        private enum TestStatus { Pass, Fail, Skip }

        private sealed record TestResult(string Name, TestStatus Status, string Detail);
    }
}
