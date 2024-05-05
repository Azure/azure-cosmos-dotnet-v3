namespace TestWorkloadV2
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;

    public class Program
    {

        private static CommonConfiguration configuration;

        private static DataSource dataSource;

        private static IDriver driver;

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
            Console.WriteLine("Configuration: " + JsonSerializer.Serialize(configuration));
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
                        + $" P99: {GetLatencyToDisplay(lastBucketLatencies, lastBucketLatencies.Count * 0.99)}");
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

        private static void OnEnd()
        {
            long runtimeSeconds = latencyStopwatch.ElapsedMilliseconds / 1000;
            DateTime runEndTime = DateTime.UtcNow;
            int nonFailedCountFinal = countsByStatus.Where(x => x.Key < HttpStatusCode.BadRequest).Sum(p => p.Value);
            int nonFailedCountFinalForLatency = nonFailedCountFinal - warmupNonFailedRequestCount;

            Console.WriteLine();
            Console.WriteLine($"Run duration: {runStartTime} to {runEndTime} UTC");
            Console.WriteLine("Configuration: " + JsonSerializer.Serialize(configuration));

            Console.WriteLine($"Partition key prefix: {dataSource.PartitionKeyValuePrefix} Initial ItemId: {dataSource.InitialItemId} ItemId: {dataSource.ItemId}");
            Console.WriteLine($"Successful requests: Total {nonFailedCountFinal}; post-warm up {nonFailedCountFinalForLatency} requests in {runtimeSeconds} seconds at {(runtimeSeconds == 0 ? -1 : nonFailedCountFinalForLatency / runtimeSeconds)} items/sec.");
            List<TimeSpan> latenciesList = latencies.ToList();
            latenciesList.Sort();
            int nonWarmupRequestCount = latenciesList.Count;
            if (nonWarmupRequestCount > 0)
            {
                Console.WriteLine("Latencies:"
                + $"   Avg: {Math.Round(latenciesList.Average(t => t.TotalMilliseconds), 1, MidpointRounding.AwayFromZero)}"
                + $"   P50: {GetLatencyToDisplay(latenciesList, nonWarmupRequestCount * 0.50)}"
                + $"   P90: {GetLatencyToDisplay(latenciesList, nonWarmupRequestCount * 0.90)}"
                + $"   P95: {GetLatencyToDisplay(latenciesList, nonWarmupRequestCount * 0.95)}"
                + $"   P99: {GetLatencyToDisplay(latenciesList, nonWarmupRequestCount * 0.99)}"
                + $"   P99.9: {GetLatencyToDisplay(latenciesList, nonWarmupRequestCount * 0.999)}"
                + $"   Max: {GetLatencyToDisplay(latenciesList, nonWarmupRequestCount -1)}");
            }

            Console.WriteLine("Average RUs: " + (totalRequestCharge / (100.0 * nonFailedCountFinal)));

            Console.Write("Counts by StatusCode: ");
            Console.WriteLine(string.Join(", ", countsByStatus.Select(countForStatus => countForStatus.Key + ": " + countForStatus.Value)));

        }

        private static double GetLatencyToDisplay(List<TimeSpan> latencyList, double index)
        {
            return Math.Round(latencyList[(int)index].TotalMilliseconds, 1, MidpointRounding.AwayFromZero);
        }
    }
}