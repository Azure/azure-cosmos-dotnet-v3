namespace TestWorkloadV2
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;

    public class Program
    {

        private static CommonConfiguration configuration;

        private static DataSource dataSource;

        private static IDriver driver;

        private static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static async Task Main(string[] args)
        {
            try
            {
                Program.driver = new Mongo();
                await Program.InitializeAsync(args);
                await Program.PerformOperationsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex);
            }
            finally
            {
                await Program.driver.CleanupAsync();
            }
        }



        private static async Task InitializeAsync(string[] args)
        {
            IConfigurationRoot configurationRoot = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .AddCommandLine(args)
                    .Build();

            (Program.configuration, Program.dataSource) = await Program.driver.InitializeAsync(configurationRoot);
            if(Program.configuration.ConnectionStringForLogging == null)
            {
                throw new Exception("ConnectionStringForLogging is not set in the driver's InitializeAsync method.");
            }
        }

        private static DateTime runStartTime;

        private static int warmupNonFailedRequestCount = 0;

        private static readonly ConcurrentDictionary<HttpStatusCode, int> countsByStatus = new ConcurrentDictionary<HttpStatusCode, int>();

        private static readonly ConcurrentBag<TimeSpan> latencies = new ConcurrentBag<TimeSpan>();

        private static long totalRequestCharge = 0;

        private static readonly Stopwatch latencyStopwatch = new Stopwatch();

        private static async Task PerformOperationsAsync()
        {
            WriteConfiguration();
            Console.WriteLine($"Starting to make requests with partition key prefix {dataSource.PartitionKeyValuePrefix} and initial ItemId {dataSource.InitialItemId}");

            ConcurrentBag<TimeSpan> oddBucketLatencies = new ConcurrentBag<TimeSpan>();
            ConcurrentBag<TimeSpan> evenBucketLatencies = new ConcurrentBag<TimeSpan>();

            int taskTriggeredCounter = 0;
            int taskCompleteCounter = 0;

            const int ticksPerMillisecond = 10000;
            const int ticksPerSecond = 1000 * ticksPerMillisecond;
            int ticksPerRequest = configuration.RequestsPerSecond.HasValue ? (int)(ticksPerSecond / configuration.RequestsPerSecond) : 0;
            long usageTicks = 0;

            int totalRequestCount = configuration.TotalRequestCount ?? int.MaxValue;

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            if (configuration.MaxRuntimeInSeconds.HasValue)
            {
                cancellationTokenSource.CancelAfter(configuration.MaxRuntimeInSeconds.Value * 1000);
            }

            CancellationToken cancellationToken = cancellationTokenSource.Token;
            runStartTime = DateTime.UtcNow;
            Stopwatch stopwatch = Stopwatch.StartNew();
            int isOddBucketForLatencyTracing = 1;
            long lastLatencyEmittedSeconds = 0;

            _ = Task.Run(async () =>
            {
                int docCounter = 0;
                bool isErrorPrinted = false;

                while (!cancellationToken.IsCancellationRequested && docCounter < totalRequestCount)
                {
                    docCounter++;

                    long elapsedTicks = stopwatch.ElapsedTicks;
                    if (usageTicks < elapsedTicks)
                    {
                        usageTicks = elapsedTicks;
                    }

                    usageTicks += ticksPerRequest;
                    if (usageTicks - elapsedTicks > ticksPerSecond)
                    {
                        await Task.Delay((int)((usageTicks - elapsedTicks - ticksPerSecond) / ticksPerMillisecond));
                    }

                    while (taskTriggeredCounter - taskCompleteCounter > configuration.MaxInFlightRequestCount)
                    {
                        // adding a delay > 0 msec introduces 15 msec delay due to system clock resolution which is too much; so we instead have a tight loop.
                    }

                    long requestStartTicks = stopwatch.ElapsedTicks;
                    _ = Program.driver.MakeRequestAsync(cancellationToken, out object context).ContinueWith((Task task) =>
                    {
                        TimeSpan requestLatency = TimeSpan.FromTicks(stopwatch.ElapsedTicks - requestStartTicks);
                        ResponseAttributes responseAttributes = Program.driver.HandleResponse(task, context);

                        countsByStatus.AddOrUpdate(responseAttributes.StatusCode, 1, (_, old) => old + 1);

                        if (responseAttributes.StatusCode < HttpStatusCode.BadRequest)
                        {
                            Interlocked.Add(ref totalRequestCharge, (int)(responseAttributes.RequestCharge * 100));
                            if (latencyStopwatch.IsRunning)
                            {
                                latencies.Add(requestLatency);
                                if (Interlocked.Add(ref isOddBucketForLatencyTracing, 0) == 1)
                                {
                                    oddBucketLatencies.Add(requestLatency);
                                }
                                else
                                {
                                    evenBucketLatencies.Add(requestLatency);
                                }
                            }
                        }
                        else
                        {
                            if ((int)responseAttributes.StatusCode != 429 && !isErrorPrinted)
                            {
                                //Console.WriteLine(responseAttributes.ErrorMessage);
                                //Console.WriteLine(responseAttributes.Diagnostics.ToString());
                                isErrorPrinted = true;
                            }
                        }

                        task.Dispose();

                        if (Interlocked.Increment(ref taskCompleteCounter) >= totalRequestCount)
                        {
                            stopwatch.Stop();
                            latencyStopwatch.Stop();
                        }
                    });

                    Interlocked.Increment(ref taskTriggeredCounter);
                }
            });

            Console.CancelKeyPress += Console_CancelKeyPress;

            while (!cancellationToken.IsCancellationRequested && taskCompleteCounter < totalRequestCount)
            {
                Console.Write($"{DateTime.UtcNow.ToLongTimeString()}> In progress for {stopwatch.Elapsed}. Triggered: {taskTriggeredCounter} Processed: {taskCompleteCounter}");
                if (configuration.TotalRequestCount.HasValue)
                {
                    Console.Write(", Pending: {totalRequestCount - taskCompleteCounter}");
                }

                int nonFailedCount = 0;
                foreach (KeyValuePair<HttpStatusCode, int> countForStatus in countsByStatus)
                {
                    Console.Write(", " + countForStatus.Key + ": " + countForStatus.Value);
                    if (countForStatus.Key < HttpStatusCode.BadRequest)
                    {
                        nonFailedCount += countForStatus.Value;
                    }
                }

                if (warmupNonFailedRequestCount == 0 && stopwatch.ElapsedMilliseconds > configuration.WarmupSeconds * 1000)
                {
                    warmupNonFailedRequestCount = nonFailedCount;
                    latencyStopwatch.Start();
                }

                long elapsedSeconds = latencyStopwatch.ElapsedMilliseconds / 1000;
                Console.Write($", SuccessRPS: {(elapsedSeconds == 0 ? -1 : (nonFailedCount - warmupNonFailedRequestCount) / elapsedSeconds)}");

                if (elapsedSeconds - lastLatencyEmittedSeconds > configuration.LatencyTracingIntervalInSeconds)
                {
                    List<TimeSpan> lastBucketLatencies;
                    if (Interlocked.Add(ref isOddBucketForLatencyTracing, 0) == 0)
                    {
                        oddBucketLatencies.Clear();
                        Interlocked.Increment(ref isOddBucketForLatencyTracing);
                        lastBucketLatencies = evenBucketLatencies.ToList();
                    }
                    else
                    {
                        evenBucketLatencies.Clear();
                        Interlocked.Decrement(ref isOddBucketForLatencyTracing);
                        lastBucketLatencies = oddBucketLatencies.ToList();
                    }

                    lastBucketLatencies.Sort();
                    Console.Write($", Latency Avg: {Math.Round(lastBucketLatencies.Average(t => t.TotalMilliseconds), 1, MidpointRounding.AwayFromZero)}"
                        + $" P99: {GetRoundedLatency(lastBucketLatencies, lastBucketLatencies.Count * 0.99)}");
                    lastLatencyEmittedSeconds = elapsedSeconds;
                }

                Console.WriteLine();
                await Task.Delay(1000);
            }

            OnEnd();
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            OnEnd();
        }

        class RunResult
        {
            internal class LatencyValues
            {
                public decimal Avg { get; set; }
                public decimal P50 { get; set; }
                public decimal P90 { get; set; }
                public decimal P95 { get; set; }

                public decimal P99 { get; set; }
                public decimal P999 { get; set; }

                public decimal Max { get; set; }
            }

            public string MachineName => Environment.MachineName;

            public DateTime RunStartTime => runStartTime;

            public DateTime RunEndTime { get; set; }

            public CommonConfiguration Configuration { get; set; }

            public string PartitionKeyValuePrefix => dataSource.PartitionKeyValuePrefix;

            public long InitialItemId => dataSource.InitialItemId;

            public long ItemId => dataSource.ItemId;

            public int NonFailedRequests { get; set; }

            public int NonFailedRequestsAfterWarmup { get; set; }

            public long RunDuration { get; set; }

            public LatencyValues Latencies { get; set; }

            public double AverageRUs { get; set; }

            public Dictionary<HttpStatusCode, int> CountsByStatus { get; set; }

            public long AchievedRequestsPerSecond { get; set; }
        }


        private static void OnEnd()
        {
            long runtimeSeconds = latencyStopwatch.ElapsedMilliseconds / 1000;
            DateTime runEndTime = DateTime.UtcNow;
            int nonFailedCountFinal = countsByStatus.Where(x => x.Key < HttpStatusCode.BadRequest).Sum(p => p.Value);
            int nonFailedCountFinalForLatency = nonFailedCountFinal - warmupNonFailedRequestCount;

            RunResult.LatencyValues latencyValues = null;
            List<TimeSpan> latenciesList = latencies.ToList();
            latenciesList.Sort();
            int nonWarmupRequestCount = latenciesList.Count;
            if (nonWarmupRequestCount > 0)
            {
                latencyValues = new()
                {
                    Avg = Math.Round((decimal)latenciesList.Average(t => t.TotalMilliseconds), 1, MidpointRounding.AwayFromZero),
                    P50 = GetRoundedLatency(latenciesList, nonWarmupRequestCount * 0.50),
                    P95 = GetRoundedLatency(latenciesList, nonWarmupRequestCount * 0.95),
                    P99 = GetRoundedLatency(latenciesList, nonWarmupRequestCount * 0.99),
                    P999 = GetRoundedLatency(latenciesList, nonWarmupRequestCount * 0.999),
                    Max = GetRoundedLatency(latenciesList, nonWarmupRequestCount - 1)
                };
            }

            RunResult runResult = new()
            {
                RunEndTime = runEndTime,
                Configuration = configuration,
                NonFailedRequests = nonFailedCountFinal,
                NonFailedRequestsAfterWarmup = nonFailedCountFinalForLatency,
                RunDuration = runtimeSeconds,
                AverageRUs = totalRequestCharge / (100.0 * nonFailedCountFinal),
                CountsByStatus = countsByStatus.ToDictionary(x => x.Key, x => x.Value),
                Latencies = latencyValues,
                AchievedRequestsPerSecond = runtimeSeconds == 0 ? -1 : (nonFailedCountFinalForLatency / runtimeSeconds)
            };

            LogRunResultToConsole(runResult);
            using (StreamWriter writer = new StreamWriter(String.Format("runresult-{0}.json", runStartTime.ToString("yyyyMMdd-HHmmss"))))
            {
                writer.Write(JsonSerializer.Serialize(runResult, jsonSerializerOptions));
            }
        }

        private static decimal GetRoundedLatency(List<TimeSpan> latencyList, double index)
        {
            return Math.Round((decimal)latencyList[(int)index].TotalMilliseconds, 1, MidpointRounding.AwayFromZero);
        }

        private static void LogRunResultToConsole(RunResult runResult)
        {
            Console.WriteLine();
            Console.WriteLine($"Machine name: {runResult.MachineName}");
            Console.WriteLine($"Run duration: {runResult.RunStartTime} to {runResult.RunEndTime} UTC");
            WriteConfiguration();

            Console.WriteLine($"Partition key prefix: {runResult.PartitionKeyValuePrefix} Initial ItemId: {runResult.InitialItemId} ItemId: {runResult.ItemId}");
            Console.WriteLine($"Successful requests: Total {runResult.NonFailedRequests}; post-warm up {runResult.NonFailedRequestsAfterWarmup} requests in {runResult.RunDuration} seconds at {runResult.AchievedRequestsPerSecond} items/sec.");
            if (runResult.Latencies != null)
            {
                Console.WriteLine("Latencies:"
                  + $"   Avg: {runResult.Latencies.Avg}"
                  + $"   P50: {runResult.Latencies.P50}"
                  + $"   P95: {runResult.Latencies.P95}"
                  + $"   P99: {runResult.Latencies.P99}"
                  + $"   P99.9: {runResult.Latencies.P999}"
                  + $"   Max: {runResult.Latencies.Max}");
            }

            Console.WriteLine("Average RUs: " + runResult.AverageRUs);

            Console.Write("Counts by StatusCode: ");
            Console.WriteLine(string.Join(", ", runResult.CountsByStatus.Select(countForStatus => countForStatus.Key + ": " + countForStatus.Value)));
        }

        private static void WriteConfiguration()
        {
            Console.WriteLine("Configuration: " + JsonSerializer.Serialize(configuration, jsonSerializerOptions));
        }
    }
}