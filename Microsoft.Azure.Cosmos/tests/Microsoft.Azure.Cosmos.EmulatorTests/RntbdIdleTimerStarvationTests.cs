//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Integration tests for the RNTBD idle-timer thread-pool starvation
    /// fix (issue #4393). Reads connection info from the COSMOS_ENDPOINT
    /// and COSMOS_KEY environment variables so the tests can run against
    /// a live account from any Linux/Docker pipeline without touching
    /// App.config.
    /// </summary>
    [TestClass]
    [TestCategory("RntbdIdleTimer")]
    public class RntbdIdleTimerStarvationTests
    {
        private const string EndpointEnvVar = "COSMOS_ENDPOINT";
        private const string KeyEnvVar = "COSMOS_KEY";

        private const string TestDatabaseId = "rntbd-starvation-test";
        private static string GetContainerId(int n) => $"items-{n}";

        private static (string Endpoint, string Key) ReadCredentialsOrSkip()
        {
            string endpoint = Environment.GetEnvironmentVariable(EndpointEnvVar);
            string key = Environment.GetEnvironmentVariable(KeyEnvVar);

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
            {
                Assert.Inconclusive(
                    $"{EndpointEnvVar} and/or {KeyEnvVar} are not set; skipping test that requires a live Cosmos DB account.");
            }

            return (endpoint, key);
        }

        [TestMethod]
        public async Task SmokeTest_CanReachEndpoint()
        {
            (string endpoint, string key) = ReadCredentialsOrSkip();

            CosmosClientOptions options = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct,
            };

            using CosmosClient client = new CosmosClient(endpoint, key, options);

            AccountProperties account = await client.ReadAccountAsync();

            Assert.IsNotNull(account, "ReadAccountAsync returned null AccountProperties.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(account.Id), "AccountProperties.Id was empty.");
        }

        // REGRESSION GUARD for the idle timer fix wiring (issue #4393, PR #5722).
        //
        // What this test validates:
        //   - Idle timers arm and fire correctly when the SDK runs against a
        //     real Cosmos DB account.
        //   - The .Unwrap() in Dispatcher.ScheduleIdleTimer continues to track
        //     OnIdleTimerAsync completion correctly. If .Unwrap() is removed
        //     in the future, the timer-fire-count assertion catches it (the
        //     production trace at OnIdleTimerAsync entry stops being emitted
        //     because the continuation chain breaks).
        //   - No thread-count explosion or unhandled exceptions during
        //     idle-fire at N=50 over an ~13-minute window.
        //
        // What this test does NOT validate:
        //   - That the fix prevents thread pool starvation. This was
        //     investigated extensively (see PR #5722 stages 3.5 - 3.11) and
        //     the conclusion is that a single test client cannot reliably
        //     reproduce the production starvation pattern.
        //
        //     The bug WAS reproduced once during investigation at N=50 in
        //     this environment: 48-second probe latency, 46-thread pool
        //     growth, 12.5-second median .Wait() durations on
        //     genuinely-pending receive tasks. This confirmed the production
        //     pathology is real here.
        //
        //     But six subsequent runs across N=50 and N=200 did not
        //     reproduce it (max latency ~1001ms — a probe-implementation
        //     artifact, not a Dispatcher signal). The production bug
        //     reported in #4393 involved sustained multi-client scale and
        //     timing conditions that this single-client test cannot
        //     synthesize on demand.
        //
        // Canonical starvation-prevention evidence:
        //   See DispatcherIdleTimerFixTests in the unit test project. It
        //   directly invokes OnIdleTimerAsync with a receive task that is
        //   genuinely pending for the duration of the measurement window,
        //   deterministically demonstrating that the post-fix async path
        //   yields thread pool threads where the pre-fix sync path blocked
        //   them.
        //
        // References:
        //   Issue #4393 (production bug report with stack trace)
        //   PR #5722 (this fix)
        //   DispatcherIdleTimerFixTests.cs (canonical fix evidence)
        [DataTestMethod]
        [DataRow(50)]
        public async Task IdleTimerFire_WiringStillFunctional(int connectionCount)
        {
            (string endpoint, string key) = ReadCredentialsOrSkip();

            // Shrink the thread pool so that blocked .Wait() calls in
            // Dispatcher.OnIdleTimer produce visible scheduling latency
            // on the probe. Production customers see this symptom on
            // busy machines where the pool is effectively saturated; we
            // simulate that here by making the min pool tiny.
            ThreadPool.GetMinThreads(out int origWorker, out int origIo);
            ThreadPool.SetMinThreads(2, 2);

            CosmosClient client = null;
            TimerFireCountingListener timerListener = null;
            IDisposable traceListenerScope = null;
            try
            {
                traceListenerScope = TimerFireCountingListener.Install(out timerListener);

                CosmosClientOptions options = new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct,
                    // The SDK enforces a minimum IdleTcpConnectionTimeout of 600 s
                    // (see StoreClientFactory ctor: "valid value: >= 10 minutes").
                    // Using the minimum so the test triggers idle-timer fires as
                    // fast as possible.
                    IdleTcpConnectionTimeout = TimeSpan.FromSeconds(600),
                    OpenTcpConnectionTimeout = TimeSpan.FromSeconds(3),
                    MaxRequestsPerTcpConnection = 1,
                    MaxTcpConnectionsPerEndpoint = Math.Max(connectionCount * 4, 256),
                };

                client = new CosmosClient(endpoint, key, options);

                Database db = (await client.CreateDatabaseIfNotExistsAsync(TestDatabaseId)).Database;
                Container container = (await db.CreateContainerIfNotExistsAsync(
                    id: GetContainerId(connectionCount),
                    partitionKeyPath: "/pk",
                    throughput: 10000)).Container;

                List<(string Id, string Pk)> keys = new List<(string, string)>(connectionCount);
                for (int i = 0; i < connectionCount; i++)
                {
                    keys.Add(($"doc-{i:D4}", $"pk-{i:D4}"));
                }

                // Seed items (idempotent across runs — reuses the same doc ids).
                foreach ((string id, string pk) in keys)
                {
                    try
                    {
                        await container.CreateItemAsync(
                            new TestDoc { id = id, pk = pk, payload = "x" },
                            new PartitionKey(pk));
                    }
                    catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.Conflict)
                    {
                    }
                }

                // Warm-up: run connectionCount parallel read loops for
                // a few seconds. MaxRequestsPerTcpConnection=1 means the
                // LoadBalancingPartition spawns a new channel whenever
                // every existing one has >=1 in-flight request, so a
                // sustained burst of N concurrent readers saturates the
                // channel pool to ~N channels. A single Task.WhenAll over
                // N fast reads is not enough: they complete faster than
                // new channels are opened.
                using (CancellationTokenSource warmCts = new CancellationTokenSource())
                {
                    Task[] readers = new Task[connectionCount];
                    for (int i = 0; i < connectionCount; i++)
                    {
                        int idx = i;
                        readers[idx] = Task.Run(async () =>
                        {
                            while (!warmCts.IsCancellationRequested)
                            {
                                try
                                {
                                    await container.ReadItemAsync<TestDoc>(
                                        keys[idx].Id,
                                        new PartitionKey(keys[idx].Pk));
                                }
                                catch
                                {
                                }
                            }
                        });
                    }

                    await Task.Delay(TimeSpan.FromSeconds(15));
                    warmCts.Cancel();
                    await Task.WhenAll(readers);
                }

                // Let transient worker threads retire.
                await Task.Delay(TimeSpan.FromSeconds(2));

                int baselineThreadCount = ThreadPool.ThreadCount;

                // Probe phase.
                List<long> probeLatencies = new List<long>();
                int neverExecuted = 0;
                int peakThreadCount = baselineThreadCount;

                using CancellationTokenSource probeCts = new CancellationTokenSource();

                Task probeTask = Task.Run(async () =>
                {
                    while (!probeCts.IsCancellationRequested)
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        TaskCompletionSource<long> tcs = new TaskCompletionSource<long>(
                            TaskCreationOptions.RunContinuationsAsynchronously);

                        ThreadPool.QueueUserWorkItem(_ => tcs.TrySetResult(sw.ElapsedMilliseconds));

                        Task winner = await Task.WhenAny(tcs.Task, Task.Delay(500));
                        if (winner == tcs.Task)
                        {
                            probeLatencies.Add(tcs.Task.Result);
                        }
                        else
                        {
                            probeLatencies.Add(500);
                            Interlocked.Increment(ref neverExecuted);
                        }

                        try
                        {
                            await Task.Delay(100, probeCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                });

                Task sampleTask = Task.Run(async () =>
                {
                    while (!probeCts.IsCancellationRequested)
                    {
                        int current = ThreadPool.ThreadCount;
                        if (current > peakThreadCount)
                        {
                            peakThreadCount = current;
                        }

                        try
                        {
                            await Task.Delay(500, probeCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                });

                // Trigger: no more traffic. The SDK arms the dispatcher idle
                // timer at (IdleTcpConnectionTimeout + 2*(sendHang + receiveHang))
                // = 600 + 2*(10 + 65) = 750 s after the last receive (see
                // Connection.IsActive / Connection.idleConnectionClosureTimeout).
                // Wait 810 s so every idle timer has fired well within the
                // observation window.
                await Task.Delay(TimeSpan.FromSeconds(810));

                probeCts.Cancel();
                await Task.WhenAll(probeTask, sampleTask);

                // Analyze.
                long maxLatency = probeLatencies.Max();
                List<long> sorted = probeLatencies.OrderBy(x => x).ToList();
                long p50 = sorted[sorted.Count / 2];
                long p95 = sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.95))];
                long p99 = sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.99))];
                int delta = peakThreadCount - baselineThreadCount;
                int timersFired = timerListener?.Count ?? 0;

                string summary =
                    $"N={connectionCount} probes={probeLatencies.Count} " +
                    $"maxLatencyMs={maxLatency} p50={p50} p95={p95} p99={p99} " +
                    $"neverExecuted={neverExecuted} baselineThreads={baselineThreadCount} " +
                    $"peakThreads={peakThreadCount} delta={delta} timersFired={timersFired}";

                Console.WriteLine(summary);
                Trace.WriteLine(summary);

                Assert.IsTrue(
                    delta < 10,
                    $"Thread count delta {delta} >= 10 threshold. {summary}");
                Assert.IsTrue(
                    timersFired > 0,
                    $"No idle-timer fires were observed. The .Unwrap() chain in " +
                    $"Dispatcher.ScheduleIdleTimer or the production trace at " +
                    $"OnIdleTimerAsync entry may have regressed. {summary}");
            }
            finally
            {
                try
                {
                    client?.Dispose();
                }
                catch
                {
                }

                traceListenerScope?.Dispose();
                ThreadPool.SetMinThreads(origWorker, origIo);
            }
        }

        // Counts idle-timer fires emitted by the SDK's DefaultTrace source.
        // Uses reflection so the test compiles against both
        // Microsoft.Azure.Cosmos.EmulatorTests (which has InternalsVisibleTo)
        // and Microsoft.Azure.Cosmos.LinuxSmoke (which does not).
        private sealed class TimerFireCountingListener : TraceListener
        {
            private const string IdleTimerFiredMarker = "Idle timer fired.";

            private int count;

            public int Count => Volatile.Read(ref this.count);

            public override void Write(string message)
            {
            }

            public override void WriteLine(string message)
            {
            }

            public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
            {
                this.MaybeIncrement(message);
            }

            public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
            {
                if (format != null && format.IndexOf(IdleTimerFiredMarker, StringComparison.Ordinal) >= 0)
                {
                    Interlocked.Increment(ref this.count);
                }
            }

            private void MaybeIncrement(string message)
            {
                if (message != null && message.IndexOf(IdleTimerFiredMarker, StringComparison.Ordinal) >= 0)
                {
                    Interlocked.Increment(ref this.count);
                }
            }

            public static IDisposable Install(out TimerFireCountingListener listener)
            {
                Assembly cosmosAsm = typeof(CosmosClient).Assembly;
                Type defaultTraceType = cosmosAsm.GetType("Microsoft.Azure.Cosmos.Core.Trace.DefaultTrace", throwOnError: false);
                if (defaultTraceType == null)
                {
                    listener = null;
                    return new NoopDisposable();
                }

                PropertyInfo sourceProp = defaultTraceType.GetProperty("TraceSource", BindingFlags.Public | BindingFlags.Static);
                if (sourceProp == null)
                {
                    listener = null;
                    return new NoopDisposable();
                }

                TraceSource source = (TraceSource)sourceProp.GetValue(null);
                if (source == null)
                {
                    listener = null;
                    return new NoopDisposable();
                }

                listener = new TimerFireCountingListener();
                SourceLevels originalLevel = source.Switch.Level;
                source.Switch.Level = SourceLevels.All;
                source.Listeners.Add(listener);

                TimerFireCountingListener captured = listener;
                return new ActionDisposable(() =>
                {
                    try
                    {
                        source.Listeners.Remove(captured);
                    }
                    catch
                    {
                    }
                    try
                    {
                        source.Switch.Level = originalLevel;
                    }
                    catch
                    {
                    }
                });
            }

            private sealed class NoopDisposable : IDisposable
            {
                public void Dispose()
                {
                }
            }

            private sealed class ActionDisposable : IDisposable
            {
                private Action action;

                public ActionDisposable(Action action)
                {
                    this.action = action;
                }

                public void Dispose()
                {
                    Action local = Interlocked.Exchange(ref this.action, null);
                    local?.Invoke();
                }
            }
        }

        private sealed class TestDoc
        {
            public string id { get; set; }

            public string pk { get; set; }

            public string payload { get; set; }
        }
    }
}
