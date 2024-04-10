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
                await Program.CreateItemsConcurrentlyAsync();
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

        private static async Task CreateItemsConcurrentlyAsync()
        {
            Console.WriteLine($"Starting to make {configuration.TotalRequestCount} requests to create items of about {configuration.ItemSize} bytes each with partition key prefix {dataSource.PartitionKeyValuePrefix}");

            ConcurrentBag<TimeSpan> oddBucketLatencies = new ConcurrentBag<TimeSpan>();
            ConcurrentBag<TimeSpan> evenBucketLatencies = new ConcurrentBag<TimeSpan>();

            int taskTriggeredCounter = 0;
            int taskCompleteCounter = 0;

            const int ticksPerMillisecond = 10000;
            const int ticksPerSecond = 1000 * ticksPerMillisecond;
            int ticksPerRequest = configuration.RequestsPerSecond <= 0 ? 0 : (int)(ticksPerSecond / configuration.RequestsPerSecond);
            long usageTicks = 0;

            if (configuration.MaxInFlightRequestCount == -1)
            {
                configuration.MaxInFlightRequestCount = int.MaxValue;
            }

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            if (configuration.MaxRuntimeInSeconds > 0)
            {
                cancellationTokenSource.CancelAfter(configuration.MaxRuntimeInSeconds * 1000);
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

                while (!cancellationToken.IsCancellationRequested && docCounter < configuration.TotalRequestCount)
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

                    if (taskTriggeredCounter - taskCompleteCounter > configuration.MaxInFlightRequestCount)
                    {
                        await Task.Delay((int)((taskTriggeredCounter - taskCompleteCounter - configuration.MaxInFlightRequestCount) * ticksPerRequest / ticksPerMillisecond));
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

                        if (Interlocked.Increment(ref taskCompleteCounter) >= configuration.TotalRequestCount)
                        {
                            stopwatch.Stop();
                            latencyStopwatch.Stop();
                        }
                    });

                    Interlocked.Increment(ref taskTriggeredCounter);
                }
            });

            Console.CancelKeyPress += Console_CancelKeyPress;

            while (!cancellationToken.IsCancellationRequested && taskCompleteCounter < configuration.TotalRequestCount)
            {
                Console.Write($"{DateTime.UtcNow.ToLongTimeString()}> In progress for {stopwatch.Elapsed}. Triggered: {taskTriggeredCounter} Processed: {taskCompleteCounter}, Pending: {configuration.TotalRequestCount - taskCompleteCounter}");
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
                    List<TimeSpan> lastMinuteLatencies;
                    if (Interlocked.Add(ref isOddBucketForLatencyTracing, 0) == 0)
                    {
                        oddBucketLatencies.Clear();
                        Interlocked.Increment(ref isOddBucketForLatencyTracing);
                        lastMinuteLatencies = evenBucketLatencies.ToList();
                    }
                    else
                    {
                        evenBucketLatencies.Clear();
                        Interlocked.Decrement(ref isOddBucketForLatencyTracing);
                        lastMinuteLatencies = oddBucketLatencies.ToList();
                    }

                    lastMinuteLatencies.Sort();
                    Console.Write($", P99 latency: {lastMinuteLatencies[(int)(lastMinuteLatencies.Count * 0.99)].TotalMilliseconds}");
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

            Console.WriteLine($"Partition key prefix: {dataSource.PartitionKeyValuePrefix}");
            Console.WriteLine($"Successful requests: Total {nonFailedCountFinal}; post-warm up {nonFailedCountFinalForLatency} requests in {runtimeSeconds} seconds at {(runtimeSeconds == 0 ? -1 : nonFailedCountFinalForLatency / runtimeSeconds)} items/sec.");
            List<TimeSpan> latenciesList = latencies.ToList();
            latenciesList.Sort();
            int nonWarmupRequestCount = latenciesList.Count;
            if (nonWarmupRequestCount > 0)
            {
                Console.WriteLine("Latencies:"
                + $"   P90: {latenciesList[(int)(nonWarmupRequestCount * 0.90)].TotalMilliseconds}"
                + $"   P95: {latenciesList[(int)(nonWarmupRequestCount * 0.95)].TotalMilliseconds}"
                + $"   P99: {latenciesList[(int)(nonWarmupRequestCount * 0.99)].TotalMilliseconds}"
                + $"   P99.9: {latenciesList[(int)(nonWarmupRequestCount * 0.999)].TotalMilliseconds}"
                + $"   Max: {latenciesList[nonWarmupRequestCount - 1].TotalMilliseconds}");
            }

            Console.WriteLine("Average RUs: " + (totalRequestCharge / (100.0 * nonFailedCountFinal)));

            Console.Write("Counts by StatusCode: ");
            Console.WriteLine(string.Join(", ", countsByStatus.Select(countForStatus => countForStatus.Key + ": " + countForStatus.Value)));

        }
    }
}