//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Repro harness for the reported hang when a request-level
    /// <see cref="AvailabilityStrategy.DisabledStrategy"/> is combined with a request-level
    /// <see cref="ReadConsistencyStrategy.LastCommittedSingleWriteRegion"/> on a two-region
    /// single-master account whose application region is the READ replica (not the hub).
    ///
    /// Requires a real account. Set the connection string in an environment variable:
    ///     setx COSMOSDB_MULTIREGION "AccountEndpoint=...;AccountKey=...;"
    /// Never commit or paste the connection string.
    ///
    /// Topology this is written against:
    ///   - Single write region (hub)  : West US 2
    ///   - Read replica               : West Central US
    ///   - ApplicationRegion          : West Central US   (the replica, NOT the hub)
    ///
    /// NOTE on ApplicationRegion vs ApplicationPreferredRegions: these are NOT equivalent.
    /// ApplicationRegion expands, via ConnectionPolicy.SetCurrentLocation, into a proximity-ordered
    /// preferred list containing ALL account regions. So ReadEndpoints.Count == 2 here and the hub
    /// IS a reachable candidate — which is exactly the configuration the customer reported. Using
    /// ApplicationPreferredRegions = [replica] instead would collapse the list to one entry and
    /// exercise a different code path, so this harness deliberately uses ApplicationRegion.
    /// </summary>
    [TestClass]
    [TestCategory("MultiRegion")]
    public class DisabledStrategyLastCommittedHangTests
    {
        private const string ConnectionStringEnvVar = "COSMOSDB_MULTIREGION";

        private const string PartitionLevelFailoverEnvVar = "AZURE_COSMOS_PARTITION_LEVEL_FAILOVER_ENABLED";

        /// <summary>
        /// The read replica region — the client's application region. Deliberately NOT the hub.
        /// Override via COSMOSDB_MULTIREGION_READ_REGION if your account uses different regions.
        /// </summary>
        private static string ApplicationRegionUnderTest =>
            Environment.GetEnvironmentVariable("COSMOSDB_MULTIREGION_READ_REGION") ?? Regions.WestCentralUS;

        private static readonly TimeSpan HangThreshold = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Stable ids, deliberately NOT randomized per run. A freshly created collection is not
        /// immediately readable from the read replica ("Collection is not yet available for read"),
        /// and this client's application region IS the replica — so a create-per-run harness spends
        /// its time fighting propagation instead of exercising the bug. The database is created if
        /// absent and then left in place for subsequent runs.
        /// </summary>
        private const string DatabaseId = "DisabledStrategyHangRepro";
        private const string ContainerId = "repro";

        private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromMinutes(3);

        private string connectionString;

        [TestInitialize]
        public void TestInit()
        {
            this.connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Inconclusive(
                    $"Set environment variable {ConnectionStringEnvVar} to a two-region single-master " +
                    "account connection string to run this test.");
            }
        }

        /// <summary>
        /// Control case. Without DisabledStrategy the read is reported to complete
        /// (with a 403, which is expected for LastCommitted against a non-hub application region).
        /// </summary>
        [TestMethod]
        public Task LastCommitted_WithoutDisabledStrategy_Completes()
        {
            return this.RunScenarioAsync(
                scenario: "LastCommitted only (no DisabledStrategy)",
                enablePartitionLevelFailover: false,
                buildOptions: () => new ItemRequestOptions
                {
                    ReadConsistencyStrategy = ReadConsistencyStrategy.LastCommittedSingleWriteRegion,
                });
        }

        /// <summary>
        /// The reported failure. Both knobs are set at the REQUEST level.
        /// If the SDK hangs, this fails after <see cref="HangThreshold"/> instead of stalling forever.
        /// </summary>
        [TestMethod]
        public Task LastCommitted_WithRequestLevelDisabledStrategy_DoesNotHang()
        {
            return this.RunScenarioAsync(
                scenario: "LastCommitted + request-level DisabledStrategy",
                enablePartitionLevelFailover: false,
                buildOptions: () => new ItemRequestOptions
                {
                    AvailabilityStrategy = AvailabilityStrategy.DisabledStrategy(),
                    ReadConsistencyStrategy = ReadConsistencyStrategy.LastCommittedSingleWriteRegion,
                });
        }

        /// <summary>
        /// Tests the customer's claim that supplying a REAL cross-region hedging strategy (rather
        /// than DisabledStrategy) makes the read succeed.
        ///
        /// This is the discriminating case. The hedging path injects a CrossRegionAvailabilityContext,
        /// which is one of the terms gating the hub-region routing branch in ClientRetryPolicy. If
        /// this variant completes while the others hang, that dependency is confirmed and the hedged
        /// arm is what rescues the operation — meaning hedging MASKS the defect rather than avoiding
        /// it. If it hangs too, the trigger is purely LastCommittedSingleWriteRegion.
        ///
        /// The threshold is deliberately short so the hedge fires well inside the hang window.
        /// </summary>
        [TestMethod]
        public Task LastCommitted_WithCrossRegionHedgingStrategy_DoesNotHang()
        {
            return this.RunScenarioAsync(
                scenario: "LastCommitted + CrossRegionHedgingStrategy",
                enablePartitionLevelFailover: false,
                buildOptions: () => new ItemRequestOptions
                {
                    AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(
                        threshold: TimeSpan.FromMilliseconds(500),
                        thresholdStep: TimeSpan.FromMilliseconds(200)),
                    ReadConsistencyStrategy = ReadConsistencyStrategy.LastCommittedSingleWriteRegion,
                });
        }

        /// <summary>
        /// Same as above but with partition level failover enabled on the client. The customer's
        /// diagnostics show per-partition hub-region routing in play, which is only reachable with
        /// this enabled — so this variant is the closest match to the reported configuration.
        /// </summary>
        [TestMethod]
        public Task LastCommitted_WithDisabledStrategyAndPartitionLevelFailover_DoesNotHang()
        {
            return this.RunScenarioAsync(
                scenario: "LastCommitted + DisabledStrategy + PartitionLevelFailover",
                enablePartitionLevelFailover: true,
                buildOptions: () => new ItemRequestOptions
                {
                    AvailabilityStrategy = AvailabilityStrategy.DisabledStrategy(),
                    ReadConsistencyStrategy = ReadConsistencyStrategy.LastCommittedSingleWriteRegion,
                });
        }

        private async Task RunScenarioAsync(
            string scenario,
            bool enablePartitionLevelFailover,
            Func<ItemRequestOptions> buildOptions)
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct,

                // The replica region, not the hub. This expands to a proximity-ordered list of
                // ALL account regions (see class remarks).
                ApplicationRegion = ApplicationRegionUnderTest,
            };

            // PPAF is not a public CosmosClientOptions knob — it is driven by this environment
            // variable (see ConfigurationManager.PartitionLevelFailoverEnabled) and is read during
            // client construction, so it must be set before the client is created.
            string previousPpaf = Environment.GetEnvironmentVariable(PartitionLevelFailoverEnvVar);
            if (enablePartitionLevelFailover)
            {
                Environment.SetEnvironmentVariable(PartitionLevelFailoverEnvVar, "True");
            }

            using CosmosClient client = new CosmosClient(this.connectionString, clientOptions);

            try
            {
                Database database = await client.CreateDatabaseIfNotExistsAsync(DatabaseId);

                Container container = await database.CreateContainerIfNotExistsAsync(
                    id: ContainerId,
                    partitionKeyPath: "/pk");

                string itemId = Guid.NewGuid().ToString();
                string partitionKeyValue = Guid.NewGuid().ToString();

                await container.CreateItemAsync(
                    new ReproItem { id = itemId, pk = partitionKeyValue },
                    new PartitionKey(partitionKeyValue));

                // Wait until the item is actually readable from the replica region before starting
                // the measured attempts. A newly created collection returns 404 with
                // "Collection is not yet available for read" until it propagates, which would
                // otherwise be mistaken for a test failure.
                await WaitUntilReadableAsync(container, itemId, partitionKeyValue);

                // Issue the read several times on the SAME client. The failover state that drives
                // the suspected loop is per-partition and persists across operations, so the hang
                // may only surface once that state has been populated by an earlier attempt.
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    await this.RunReadAndAssertCompletesAsync(
                        container,
                        itemId,
                        partitionKeyValue,
                        buildOptions(),
                        $"{scenario} (attempt {attempt})");
                }
            }
            finally
            {
                // The database is intentionally left in place — see the DatabaseId remarks.
                if (enablePartitionLevelFailover)
                {
                    Environment.SetEnvironmentVariable(PartitionLevelFailoverEnvVar, previousPpaf);
                }
            }
        }

        /// <summary>
        /// Polls a plain read until the item is served successfully from the client's application
        /// region. Doubles as cache warming: the reported hang involves the per-partition
        /// hub-region cache, whose behaviour differs between a cold first wire and a later retry.
        /// </summary>
        private static async Task WaitUntilReadableAsync(
            Container container,
            string itemId,
            string partitionKeyValue)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (true)
            {
                using ResponseMessage response = await container.ReadItemStreamAsync(
                    itemId, new PartitionKey(partitionKeyValue));

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Item readable after {stopwatch.ElapsedMilliseconds} ms.");
                    return;
                }

                if (stopwatch.Elapsed > ReadinessTimeout)
                {
                    Assert.Inconclusive(
                        $"Item was still not readable after {ReadinessTimeout.TotalMinutes} minutes " +
                        $"(last status {(int)response.StatusCode}/{response.Headers?.SubStatusCode}). " +
                        "The collection may still be propagating to the read region; retry shortly.");
                }

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        private async Task RunReadAndAssertCompletesAsync(
            Container container,
            string itemId,
            string partitionKeyValue,
            ItemRequestOptions requestOptions,
            string scenario)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Capture SDK traces for the duration of the read. When the operation never completes
            // there are no CosmosDiagnostics to inspect (they materialize only on completion), so
            // the trace stream is the only window into what the retry loop is doing.
            ConcurrentQueue<string> traces = new ConcurrentQueue<string>();
            CapturingTraceListener listener = new CapturingTraceListener(traces);
            SourceLevels previousLevel = DefaultTrace.TraceSource.Switch.Level;
            DefaultTrace.TraceSource.Switch.Level = SourceLevels.All;
            DefaultTrace.TraceSource.Listeners.Add(listener);

            try
            {
                // Cancelling on timeout (rather than abandoning the task) makes the SDK surface a
                // CosmosOperationCanceledException, which carries the diagnostics accumulated so far.
                using CancellationTokenSource cts = new CancellationTokenSource(HangThreshold);

                // Stream API so error status codes (e.g. 403) come back as a response rather than
                // an exception — we care about completion, not success.
                Task<ResponseMessage> readTask = container.ReadItemStreamAsync(
                    id: itemId,
                    partitionKey: new PartitionKey(partitionKeyValue),
                    requestOptions: requestOptions,
                    cancellationToken: cts.Token);

                ResponseMessage response;
                try
                {
                    response = await readTask;
                }
                catch (OperationCanceledException ex)
                {
                    stopwatch.Stop();

                    string logPath = WriteLog(
                        scenario,
                        traces,
                        header: $"HUNG — did not complete within {HangThreshold.TotalSeconds}s " +
                                $"(elapsed {stopwatch.ElapsedMilliseconds} ms).",
                        diagnostics: ex.ToString());

                    Assert.Fail(
                        $"[{scenario}] The read did not complete within {HangThreshold.TotalSeconds}s " +
                        $"(elapsed {stopwatch.ElapsedMilliseconds} ms) — this reproduces the reported " +
                        $"hang. Full trace log: {logPath}");
                    return;
                }

                using (response)
                {
                    stopwatch.Stop();

                    string logPath = WriteLog(
                        scenario,
                        traces,
                        header: $"Completed in {stopwatch.ElapsedMilliseconds} ms with status " +
                                $"{(int)response.StatusCode}/{response.Headers?.SubStatusCode}.",
                        diagnostics: response.Diagnostics?.ToString());

                    Console.WriteLine(
                        $"[{scenario}] Completed in {stopwatch.ElapsedMilliseconds} ms with status " +
                        $"{(int)response.StatusCode}/{response.Headers?.SubStatusCode}. Log: {logPath}");
                }
            }
            finally
            {
                DefaultTrace.TraceSource.Listeners.Remove(listener);
                DefaultTrace.TraceSource.Switch.Level = previousLevel;
            }
        }

        /// <summary>
        /// Writes the full trace stream and diagnostics to a per-scenario log file under
        /// TestResults/, and prints only a short frequency summary to the console. A tight retry
        /// loop shows up as one or two normalized messages with very high counts.
        /// </summary>
        /// <returns>The path of the log file written.</returns>
        private static string WriteLog(
            string scenario,
            ConcurrentQueue<string> traces,
            string header,
            string diagnostics)
        {
            string[] all = traces.ToArray();

            string directory = Path.Combine(
                Path.GetDirectoryName(typeof(DisabledStrategyLastCommittedHangTests).Assembly.Location),
                "HangReproLogs");
            Directory.CreateDirectory(directory);

            string safeScenario = Regex.Replace(scenario, @"[^\w]+", "_").Trim('_');
            string logPath = Path.Combine(
                directory,
                $"{safeScenario}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log");

            IOrderedEnumerable<IGrouping<string, string>> byFrequency = all
                .GroupBy(NormalizeTraceMessage)
                .OrderByDescending(g => g.Count());

            using (StreamWriter writer = new StreamWriter(logPath))
            {
                writer.WriteLine($"Scenario   : {scenario}");
                writer.WriteLine($"Result     : {header}");
                writer.WriteLine($"Trace count: {all.Length}");
                writer.WriteLine();

                writer.WriteLine("=== Message frequency (normalized) ===");
                foreach (IGrouping<string, string> group in byFrequency)
                {
                    writer.WriteLine($"[{group.Count(),6}x] {group.Key}");
                }

                writer.WriteLine();
                writer.WriteLine("=== Diagnostics ===");
                writer.WriteLine(diagnostics ?? "(none)");

                writer.WriteLine();
                writer.WriteLine("=== Full trace stream ===");
                foreach (string entry in all)
                {
                    writer.WriteLine(entry);
                }
            }

            // Console gets the top few only — enough to identify which retry branch is looping.
            Console.WriteLine($"[{scenario}] {header} Captured {all.Length} traces.");
            foreach (IGrouping<string, string> group in byFrequency.Take(5))
            {
                string message = group.Key.Length > 120 ? group.Key.Substring(0, 120) + "…" : group.Key;
                Console.WriteLine($"  [{group.Count(),6}x] {message}");
            }

            return logPath;
        }

        /// <summary>
        /// Collapses the variable parts of a trace message (guids, numbers, timestamps) so that
        /// repeated occurrences of the same log site group together.
        /// </summary>
        private static string NormalizeTraceMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            string normalized = Regex.Replace(message, "[0-9a-fA-F-]{8,}", "<id>");
            normalized = Regex.Replace(normalized, @"\d+", "<n>");
            return normalized.Length > 300 ? normalized.Substring(0, 300) : normalized;
        }

        private sealed class CapturingTraceListener : TraceListener
        {
            private readonly ConcurrentQueue<string> sink;

            public CapturingTraceListener(ConcurrentQueue<string> sink)
            {
                this.sink = sink;
            }

            public override void Write(string message) => this.Add(message);

            public override void WriteLine(string message) => this.Add(message);

            public override void TraceEvent(
                TraceEventCache eventCache,
                string source,
                TraceEventType eventType,
                int id,
                string message)
            {
                this.Add($"{eventType}: {message}");
            }

            public override void TraceEvent(
                TraceEventCache eventCache,
                string source,
                TraceEventType eventType,
                int id,
                string format,
                params object[] args)
            {
                this.Add($"{eventType}: {(args == null ? format : string.Format(format, args))}");
            }

            private void Add(string message)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    this.sink.Enqueue(message.Trim());
                }
            }
        }

        private class ReproItem
        {
#pragma warning disable IDE1006 // Cosmos item property naming
            public string id { get; set; }

            public string pk { get; set; }
#pragma warning restore IDE1006
        }
    }
}
