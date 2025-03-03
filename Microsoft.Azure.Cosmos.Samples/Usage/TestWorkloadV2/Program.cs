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

        private CommonConfiguration configuration;

        private DataSource dataSource;

        private IDriver driver;

        private readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static async Task Main(string[] args)
        {
            await new Program().MainAsync(args);
        }

        private async Task MainAsync(string[] args)
        {
            try
            {
                this.driver = new CosmosDBNoSql();
                await this.InitializeAsync(args);
                await this.PerformOperationsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex);
            }
            finally
            {
                await this.driver.CleanupAsync();
            }
        }

        private async Task InitializeAsync(string[] args)
        {
            IConfigurationRoot configurationRoot = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .AddCommandLine(args)
                    .Build();

            (this.configuration, this.dataSource) = await this.driver.InitializeAsync(configurationRoot);
            if(this.configuration.ConnectionStringForLogging == null)
            {
                throw new Exception("ConnectionStringForLogging is not set in the driver's InitializeAsync method.");
            }
        }

        private DateTime runStartTime;

        private int warmupNonFailedRequestCount = 0;

        private readonly ConcurrentDictionary<HttpStatusCode, int> countsByStatus = new ConcurrentDictionary<HttpStatusCode, int>();

        private readonly ConcurrentBag<TimeSpan> latencies = new ConcurrentBag<TimeSpan>();

        private long totalRequestCharge = 0;

        private readonly Stopwatch latencyStopwatch = new Stopwatch();

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private Task mainTask;

        private async Task PerformOperationsAsync()
        {
            this.WriteConfiguration();
            Console.WriteLine($"Starting to make requests with partition key prefix {this.dataSource.PartitionKeyValuePrefix} and initial ItemId {this.dataSource.InitialItemId}");

            ConcurrentBag<TimeSpan> oddBucketLatencies = new ConcurrentBag<TimeSpan>();
            ConcurrentBag<TimeSpan> evenBucketLatencies = new ConcurrentBag<TimeSpan>();

            int taskTriggeredCounter = 0;

            int taskCompleteCounter = 0;

            const int ticksPerMillisecond = 10000;
            const int ticksPerSecond = 1000 * ticksPerMillisecond;
            int ticksPerRequest = this.configuration.RequestsPerSecond.HasValue ? (int)(ticksPerSecond / this.configuration.RequestsPerSecond) : 0;
            long usageTicks = 0;

            int totalRequestCount = this.configuration.TotalRequestCount ?? int.MaxValue;

            if (this.configuration.MaxRuntimeInSeconds.HasValue)
            {
                this.cancellationTokenSource.CancelAfter(this.configuration.MaxRuntimeInSeconds.Value * 1000);
            }

            CancellationToken cancellationToken = this.cancellationTokenSource.Token;
            this.runStartTime = DateTime.UtcNow;
            Stopwatch stopwatch = Stopwatch.StartNew();
            int isOddBucketForLatencyTracing = 1;
            long lastLatencyEmittedSeconds = 0;

            this.mainTask = Task.Run(async () =>
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

                    while (taskTriggeredCounter - taskCompleteCounter > this.configuration.MaxInFlightRequestCount)
                    {
                        // adding a delay > 0 msec introduces 15 msec delay due to system clock resolution which is too much; so we instead have a tight loop.
                    }

                    long requestStartTicks = stopwatch.ElapsedTicks;

                    // While we could have passed cancellationToken below, we pass None to not fail in-progress requests
                    // when the time ends or upon Ctrl+C.
                    _ = this.driver.MakeRequestAsync(CancellationToken.None, out object context).ContinueWith((Task task) =>
                    {
                        TimeSpan requestLatency = TimeSpan.FromTicks(stopwatch.ElapsedTicks - requestStartTicks);
                        ResponseAttributes responseAttributes = this.driver.HandleResponse(task, context);

                        this.countsByStatus.AddOrUpdate(responseAttributes.StatusCode, 1, (_, old) => old + 1);

                        if (responseAttributes.StatusCode < HttpStatusCode.BadRequest)
                        {
                            Interlocked.Add(ref this.totalRequestCharge, (int)(responseAttributes.RequestCharge * 100));
                            if (this.latencyStopwatch.IsRunning)
                            {
                                this.latencies.Add(requestLatency);
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
                            this.latencyStopwatch.Stop();
                        }
                    });

                    Interlocked.Increment(ref taskTriggeredCounter);
                }
            });

            Console.CancelKeyPress += this.Console_CancelKeyPress;

            while (!cancellationToken.IsCancellationRequested && taskCompleteCounter < totalRequestCount)
            {
                Console.Write($"{DateTime.UtcNow.ToLongTimeString()}> In progress for {stopwatch.Elapsed}. Triggered: {taskTriggeredCounter} Processed: {taskCompleteCounter}");
                if (this.configuration.TotalRequestCount.HasValue)
                {
                    Console.Write($", Pending: {totalRequestCount - taskCompleteCounter}");
                }

                int nonFailedCount = 0;
                foreach (KeyValuePair<HttpStatusCode, int> countForStatus in this.countsByStatus)
                {
                    Console.Write(", " + countForStatus.Key + ": " + countForStatus.Value);
                    if (countForStatus.Key < HttpStatusCode.BadRequest)
                    {
                        nonFailedCount += countForStatus.Value;
                    }
                }

                if (this.warmupNonFailedRequestCount == 0 && stopwatch.ElapsedMilliseconds > this.configuration.WarmupSeconds * 1000)
                {
                    this.warmupNonFailedRequestCount = nonFailedCount;
                    this.latencyStopwatch.Start();
                }

                long elapsedSeconds = this.latencyStopwatch.ElapsedMilliseconds / 1000;
                Console.Write($", SuccessRPS: {(elapsedSeconds == 0 ? -1 : (nonFailedCount - this.warmupNonFailedRequestCount) / elapsedSeconds)}");

                if (elapsedSeconds - lastLatencyEmittedSeconds > this.configuration.LatencyTracingIntervalInSeconds)
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
                    if(lastBucketLatencies.Count > 0)
                    {
                        Console.Write($", Latency Avg: {Math.Round(lastBucketLatencies.Average(t => t.TotalMilliseconds), 1, MidpointRounding.AwayFromZero)}"
                            + $" P99: {this.GetRoundedLatency(lastBucketLatencies, lastBucketLatencies.Count * 0.99)}");
                    }

                    lastLatencyEmittedSeconds = elapsedSeconds;
                }

                Console.WriteLine();
                await Task.Delay(1000);
            }

            this.OnEnd();
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            this.cancellationTokenSource.Cancel();
            this.OnEnd();
        }

        private void OnEnd()
        {
            while (!this.mainTask.IsCompleted) { }

            long runtimeSeconds = this.latencyStopwatch.ElapsedMilliseconds / 1000;
            DateTime runEndTime = DateTime.UtcNow;
            int nonFailedCountFinal = this.countsByStatus.Where(x => x.Key < HttpStatusCode.BadRequest).Sum(p => p.Value);
            int nonFailedCountFinalForLatency = nonFailedCountFinal - this.warmupNonFailedRequestCount;

            RunResult.LatencyValues latencyValues = null;
            List<TimeSpan> latenciesList = this.latencies.ToList();
            latenciesList.Sort();
            int nonWarmupRequestCount = latenciesList.Count;
            if (nonWarmupRequestCount > 0)
            {
                latencyValues = new()
                {
                    Avg = Math.Round((decimal)latenciesList.Average(t => t.TotalMilliseconds), 1, MidpointRounding.AwayFromZero),
                    P50 = this.GetRoundedLatency(latenciesList, nonWarmupRequestCount * 0.50),
                    P90 = this.GetRoundedLatency(latenciesList, nonWarmupRequestCount * 0.90),
                    P95 = this.GetRoundedLatency(latenciesList, nonWarmupRequestCount * 0.95),
                    P99 = this.GetRoundedLatency(latenciesList, nonWarmupRequestCount * 0.99),
                    P999 = this.GetRoundedLatency(latenciesList, nonWarmupRequestCount * 0.999),
                    Max = this.GetRoundedLatency(latenciesList, nonWarmupRequestCount - 1)
                };
            }

            RunResult runResult = new()
            {
                RunStartTime = this.runStartTime,
                RunEndTime = runEndTime,
                Configuration = this.configuration,
                PartitionKeyValuePrefix = this.dataSource.PartitionKeyValuePrefix,
                InitialItemId = this.dataSource.InitialItemId,
                ItemId = this.dataSource.ItemId,
                NonFailedRequests = nonFailedCountFinal,
                NonFailedRequestsAfterWarmup = nonFailedCountFinalForLatency,
                RunDuration = runtimeSeconds,
                AverageRUs = this.totalRequestCharge / (100.0 * nonFailedCountFinal),
                CountsByStatus = this.countsByStatus.ToDictionary(x => x.Key, x => x.Value),
                Latencies = latencyValues,
                AchievedRequestsPerSecond = runtimeSeconds == 0 ? -1 : (nonFailedCountFinalForLatency / runtimeSeconds)
            };

            this.LogRunResultToConsole(runResult);
            using (StreamWriter writer = new StreamWriter(String.Format("runresult-{0}.json", this.runStartTime.ToString("yyyyMMdd-HHmmss"))))
            {
                writer.Write(JsonSerializer.Serialize(runResult, this.jsonSerializerOptions));
            }
        }

        private decimal GetRoundedLatency(List<TimeSpan> latencyList, double index)
        {
            return Math.Round((decimal)latencyList[(int)index].TotalMilliseconds, 1, MidpointRounding.AwayFromZero);
        }

        private void LogRunResultToConsole(RunResult runResult)
        {
            Console.WriteLine();
            Console.WriteLine($"Machine name: {runResult.MachineName}");
            Console.WriteLine($"Run duration: {runResult.RunStartTime} to {runResult.RunEndTime} UTC");
            this.WriteConfiguration();

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

        private void WriteConfiguration()
        {
            Console.WriteLine("Configuration: " + JsonSerializer.Serialize(this.configuration, this.jsonSerializerOptions));
        }
    }
}