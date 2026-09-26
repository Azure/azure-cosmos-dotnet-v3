// ------------------------------------------------------------
// FaultInjection 429 storm benchmark for Cosmos DB .NET SDK.
//
// Phases per OTel mode: warmup / 429-burst / recovery
// Runs twice: OTel-OFF and OTel-ON.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Samples.FaultInjection429Bench
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Runtime;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.FaultInjection;

    internal static class Program
    {
        private const string EmulatorEndpoint = "https://127.0.0.1:8081/";
        private const string EmulatorKey      = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        private const string DatabaseName     = "FI429BenchDb";
        private const string ContainerName    = "FI429BenchContainer";
        private const string CosmosActivitySource = "Azure.Cosmos.Operation";
        private const int    ItemCount        = 64;
        private const int    Concurrency      = 128;
        private const int    PhaseSeconds     = 20;

        private static async Task<int> Main(string[] args)
        {
            ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;

            string sdkVersion = typeof(CosmosClient).Assembly.GetName().Version?.ToString() ?? "unknown";
            Console.WriteLine("== FaultInjection 429 storm benchmark ==");
            Console.WriteLine($"SDK assembly version : {sdkVersion}");
            Console.WriteLine($"GC mode              : Server={GCSettings.IsServerGC}, Latency={GCSettings.LatencyMode}");
            Console.WriteLine($"Concurrency          : {Concurrency}, PhaseSeconds={PhaseSeconds}");
            Console.WriteLine();

            List<BenchmarkResult> all = new List<BenchmarkResult>();
            all.AddRange(await RunMatrixAsync(otelEnabled: false));
            all.AddRange(await RunMatrixAsync(otelEnabled: true));

            Console.WriteLine();
            Console.WriteLine("== All Results ==");
            BenchmarkResult.PrintHeader();
            foreach (BenchmarkResult r in all) r.Print();

            Console.WriteLine();
            Console.WriteLine("== BURST deltas vs same-mode WARMUP ==");
            BenchmarkResult warmOff  = all.First(r => r.Label.StartsWith("OFF-WARM"));
            BenchmarkResult burstOff = all.First(r => r.Label.StartsWith("OFF-BURST"));
            BenchmarkResult recOff   = all.First(r => r.Label.StartsWith("OFF-RECV"));
            BenchmarkResult warmOn   = all.First(r => r.Label.StartsWith("ON-WARM"));
            BenchmarkResult burstOn  = all.First(r => r.Label.StartsWith("ON-BURST"));
            BenchmarkResult recOn    = all.First(r => r.Label.StartsWith("ON-RECV"));
            BenchmarkResult.PrintDelta("OTel-OFF BURST   ", warmOff, burstOff);
            BenchmarkResult.PrintDelta("OTel-OFF RECOVERY", warmOff, recOff);
            BenchmarkResult.PrintDelta("OTel-ON  BURST   ", warmOn,  burstOn);
            BenchmarkResult.PrintDelta("OTel-ON  RECOVERY", warmOn,  recOn);

            Console.WriteLine();
            Console.WriteLine("== Cross-mode comparison (OTel-ON / OTel-OFF, same phase) ==");
            BenchmarkResult.PrintDelta("WARMUP    ON/OFF", warmOff,  warmOn);
            BenchmarkResult.PrintDelta("BURST     ON/OFF", burstOff, burstOn);
            BenchmarkResult.PrintDelta("RECOVERY  ON/OFF", recOff,   recOn);

            Console.WriteLine();
            Console.WriteLine($"sdk_version={sdkVersion}");
            return 0;
        }

        private static async Task<List<BenchmarkResult>> RunMatrixAsync(bool otelEnabled)
        {
            string tag = otelEnabled ? "ON" : "OFF";
            using ActivityListener listener = otelEnabled ? SubscribeListener() : null;
            Console.WriteLine();
            Console.WriteLine($"=== OTel={tag} matrix start (listener {(otelEnabled ? "subscribed" : "absent")}) ===");

            FaultInjectionRule queryRule = new FaultInjectionRuleBuilder(
                    id: $"query-429-{tag}",
                    condition: new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.QueryItem)
                        .Build(),
                    result: FaultInjectionResultBuilder
                        .GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                        .WithInjectionRate(1.0)
                        .Build())
                .Build();
            queryRule.Disable();

            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule> { queryRule });

            CosmosClientOptions options = new CosmosClientOptions
            {
                ConnectionMode    = ConnectionMode.Direct,
                ConsistencyLevel  = ConsistencyLevel.Eventual,
                RequestTimeout    = TimeSpan.FromSeconds(1),
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(2),
                MaxRetryAttemptsOnRateLimitedRequests = 1,
                EnableContentResponseOnWrite          = false,
                LimitToEndpoint                       = true,
                HttpClientFactory = () =>
                {
                    HttpClientHandler handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                    };
                    return new HttpClient(handler);
                },
                CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions
                {
                    DisableDistributedTracing = !otelEnabled,
                },
            };
            CosmosClientOptions clientOptions = injector.GetFaultInjectionClientOptions(options);

            using CosmosClient client = new CosmosClient(EmulatorEndpoint, EmulatorKey, clientOptions);
            Database db = (await client.CreateDatabaseIfNotExistsAsync(DatabaseName)).Database;
            ContainerProperties props = new ContainerProperties(ContainerName, "/pk");
            Container container = (await db.CreateContainerIfNotExistsAsync(props, throughput: 10000)).Container;
            await SeedAsync(container);

            List<BenchmarkResult> results = new List<BenchmarkResult>(3);
            results.Add(await RunPhaseAsync($"{tag}-WARM", container, queryRule, ruleEnabled: false, durationSec: PhaseSeconds));
            results.Add(await RunPhaseAsync($"{tag}-BURST", container, queryRule, ruleEnabled: true, durationSec: PhaseSeconds));
            results.Add(await RunPhaseAsync($"{tag}-RECV", container, queryRule, ruleEnabled: false, durationSec: PhaseSeconds));
            return results;
        }

        private static ActivityListener SubscribeListener()
        {
            ActivityListener listener = new ActivityListener
            {
                ShouldListenTo  = src => src.Name.StartsWith("Azure.Cosmos", StringComparison.Ordinal),
                Sample          = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = _ => { },
                ActivityStopped = _ => { },
            };
            ActivitySource.AddActivityListener(listener);
            return listener;
        }

        private static async Task SeedAsync(Container container)
        {
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < ItemCount; i++)
            {
                string pk = $"pk-{i:D4}";
                tasks.Add(SafeUpsertAsync(container, pk, i));
            }
            await Task.WhenAll(tasks);
        }

        private static async Task SafeUpsertAsync(Container container, string pk, int i)
        {
            try
            {
                await container.UpsertItemAsync(new { id = pk, pk, n = i, payload = new string('x', 128) },
                    new PartitionKey(pk));
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
            }
        }

        private static async Task<BenchmarkResult> RunPhaseAsync(
            string label,
            Container container,
            FaultInjectionRule rule,
            bool ruleEnabled,
            int durationSec)
        {
            if (ruleEnabled) rule.Enable(); else rule.Disable();
            Console.WriteLine();
            Console.WriteLine($"-- Phase {label}  ruleEnabled={ruleEnabled} --");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(500);

            BenchmarkResult result = new BenchmarkResult(label);
            result.StartSnapshot();
            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSec));
            long throttled = 0, succeeded = 0, otherFailed = 0;

            Task[] workers = Enumerable.Range(0, Concurrency).Select(_ => Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        QueryDefinition q = new QueryDefinition("SELECT c.n, c.pk FROM c ORDER BY c.n");
                        using FeedIterator<dynamic> it = container.GetItemQueryIterator<dynamic>(
                            q,
                            requestOptions: new QueryRequestOptions
                            {
                                MaxConcurrency = -1,
                                MaxItemCount = 100,
                            });
                        while (it.HasMoreResults && !cts.IsCancellationRequested)
                        {
                            try
                            {
                                FeedResponse<dynamic> page = await it.ReadNextAsync(cts.Token);
                                _ = page.Count;
                                Interlocked.Increment(ref succeeded);
                            }
                            catch (CosmosException ce) when ((int)ce.StatusCode == 429
                                                          || ce.StatusCode == HttpStatusCode.RequestTimeout
                                                          || ce.StatusCode == HttpStatusCode.ServiceUnavailable)
                            {
                                Interlocked.Increment(ref throttled);
                                break;
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch (Exception)
                            {
                                Interlocked.Increment(ref otherFailed);
                                break;
                            }
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception) { Interlocked.Increment(ref otherFailed); }
                }
            }, cts.Token)).ToArray();

            try { await Task.WhenAll(workers); }
            catch (OperationCanceledException) { }

            result.StopSnapshot();
            result.Successful = succeeded;
            result.Throttled  = throttled;
            result.OtherFailed = otherFailed;
            Console.WriteLine($"   pages_ok={succeeded}  throttled={throttled}  other_failed={otherFailed}");
            return result;
        }
    }

    internal sealed class BenchmarkResult
    {
        public string Label { get; }
        public long Successful { get; set; }
        public long Throttled { get; set; }
        public long OtherFailed { get; set; }

        private long startCpuTicks, startAllocBytes, startContention, startExceptionCount;
        private int startGen0, startGen1, startGen2;
        private readonly Stopwatch sw = new Stopwatch();

        public TimeSpan Wall { get; private set; }
        public TimeSpan Cpu { get; private set; }
        public long AllocBytes { get; private set; }
        public long Contention { get; private set; }
        public long ExceptionCount { get; private set; }
        public int Gen0 { get; private set; }
        public int Gen1 { get; private set; }
        public int Gen2 { get; private set; }

        public BenchmarkResult(string label) { this.Label = label; }

        public void StartSnapshot()
        {
            Process p = Process.GetCurrentProcess();
            this.startCpuTicks       = p.TotalProcessorTime.Ticks;
            this.startAllocBytes     = GC.GetTotalAllocatedBytes(precise: false);
            this.startContention     = Monitor.LockContentionCount;
            this.startExceptionCount = Interlocked.Read(ref ExceptionCounter.Count);
            this.startGen0 = GC.CollectionCount(0);
            this.startGen1 = GC.CollectionCount(1);
            this.startGen2 = GC.CollectionCount(2);
            this.sw.Restart();
        }

        public void StopSnapshot()
        {
            this.sw.Stop();
            Process p = Process.GetCurrentProcess();
            this.Wall           = this.sw.Elapsed;
            this.Cpu            = TimeSpan.FromTicks(p.TotalProcessorTime.Ticks - this.startCpuTicks);
            this.AllocBytes     = GC.GetTotalAllocatedBytes(precise: false) - this.startAllocBytes;
            this.Contention     = Monitor.LockContentionCount - this.startContention;
            this.ExceptionCount = Interlocked.Read(ref ExceptionCounter.Count) - this.startExceptionCount;
            this.Gen0 = GC.CollectionCount(0) - this.startGen0;
            this.Gen1 = GC.CollectionCount(1) - this.startGen1;
            this.Gen2 = GC.CollectionCount(2) - this.startGen2;
        }

        public static void PrintHeader()
        {
            Console.WriteLine($"{"Phase",-10} {"Wall(s)",8} {"CPU(s)",8} {"CPU/W",6} {"AllocMB",10} {"Excs",10} {"LockCont",10} {"Gen0",6} {"Gen1",6} {"Gen2",6} {"PagesOK",10} {"Throt",8}");
        }

        public void Print()
        {
            string cpuRatio = (this.Cpu.TotalSeconds / Math.Max(this.Wall.TotalSeconds, 0.001)).ToString("F2", CultureInfo.InvariantCulture);
            Console.WriteLine($"{this.Label,-10} {this.Wall.TotalSeconds,8:F2} {this.Cpu.TotalSeconds,8:F2} {cpuRatio,6} {this.AllocBytes / 1024.0 / 1024.0,10:F1} {this.ExceptionCount,10} {this.Contention,10} {this.Gen0,6} {this.Gen1,6} {this.Gen2,6} {this.Successful,10} {this.Throttled,8}");
        }

        public static void PrintDelta(string label, BenchmarkResult baseline, BenchmarkResult other)
        {
            double Safe(double a, double b) => b <= 0 ? double.PositiveInfinity : a / b;
            Console.WriteLine($"   {label}  alloc x{Safe(other.AllocBytes, baseline.AllocBytes):F2}  exceptions x{Safe(other.ExceptionCount, baseline.ExceptionCount):F2}  lockCont x{Safe(other.Contention, baseline.Contention):F2}  cpu x{Safe(other.Cpu.TotalSeconds, baseline.Cpu.TotalSeconds):F2}  gen0 x{Safe(other.Gen0, baseline.Gen0):F2}");
        }
    }

    internal static class ExceptionCounter
    {
        public static long Count;
        static ExceptionCounter()
        {
            AppDomain.CurrentDomain.FirstChanceException += (_, _) => Interlocked.Increment(ref Count);
        }
    }
}
