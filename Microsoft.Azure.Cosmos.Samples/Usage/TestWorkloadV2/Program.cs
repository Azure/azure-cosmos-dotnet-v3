namespace TestWorkloadV2
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
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
                Program.driver = new CosmosDBNoSql();
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

                Console.WriteLine("End of demo.");
            }
        }

        private static int isOddBucketForLatencyTracing = 1;

        private static long lastMinuteLatencyEmittedSecondsOnLatencyStopwatch = 0;

        private static async Task InitializeAsync(string[] args)
        {
            IConfigurationRoot configurationRoot = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .AddCommandLine(args)
                    .Build();

            (Program.configuration, Program.dataSource) = await Program.driver.InitializeAsync(configurationRoot);
        }

        private static async Task CreateItemsConcurrentlyAsync()
        {
            Console.WriteLine($"Starting to make {configuration.TotalRequestCount} requests to create items of about {configuration.ItemSize} bytes each with partition key prefix {dataSource.PartitionKeyValuePrefix}");


            ConcurrentDictionary<HttpStatusCode, int> countsByStatus = new ConcurrentDictionary<HttpStatusCode, int>();
            ConcurrentBag<TimeSpan> latencies = new ConcurrentBag<TimeSpan>();
            ConcurrentBag<TimeSpan> oddBucketLatencies = new ConcurrentBag<TimeSpan>();
            ConcurrentBag<TimeSpan> evenBucketLatencies = new ConcurrentBag<TimeSpan>();

            long totalRequestCharge = 0;

            int taskTriggeredCounter = 0;
            int taskCompleteCounter = 0;
            int actualWarmupRequestCount = 0;

            int requestsPerWorker = (int)Math.Ceiling((double)(configuration.TotalRequestCount / configuration.NumWorkers));
            List<Task> workerTasks = new List<Task>();

            const int ticksPerMillisecond = 10000;
            const int ticksPerSecond = 1000 * ticksPerMillisecond;
            int ticksPerRequest = configuration.RequestsPerSecond <= 0 ? 0 : (int)(ticksPerSecond / configuration.RequestsPerSecond);
            long usageTicks = 0;

            if(configuration.MaxInFlightRequestCount == -1)
            {
                configuration.MaxInFlightRequestCount = int.MaxValue;
            }

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            if(configuration.MaxRuntimeInSeconds > 0)
            {
                cancellationTokenSource.CancelAfter(configuration.MaxRuntimeInSeconds * 1000);
            }

            CancellationToken cancellationToken = cancellationTokenSource.Token;
            Stopwatch stopwatch = Stopwatch.StartNew();
            Stopwatch latencyStopwatch = new Stopwatch();

            for (int workerIndex = 0; workerIndex < configuration.NumWorkers; workerIndex++)
            {
                int workerIndexLocal = workerIndex;
                workerTasks.Add(Task.Run(async () =>
                {
                    int docCounter = 0;
                    bool isErrorPrinted = false;

                    while (!cancellationToken.IsCancellationRequested && docCounter < requestsPerWorker)
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

                        if(taskTriggeredCounter - taskCompleteCounter > configuration.MaxInFlightRequestCount)
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
                }));
            }

            while (taskCompleteCounter < configuration.TotalRequestCount)
            {
                Console.Write($"In progress for {stopwatch.Elapsed}. Triggered: {taskTriggeredCounter} Processed: {taskCompleteCounter}, Pending: {configuration.TotalRequestCount - taskCompleteCounter}");
                int nonFailedCount = 0;
                foreach (KeyValuePair<HttpStatusCode, int> countForStatus in countsByStatus)
                {
                    Console.Write(", " + countForStatus.Key + ": " + countForStatus.Value);
                    if(countForStatus.Key < HttpStatusCode.BadRequest)
                    {
                        nonFailedCount += countForStatus.Value;
                    }
                }

                if(actualWarmupRequestCount == 0 && stopwatch.ElapsedMilliseconds > configuration.WarmupSeconds * 1000)
                {
                    actualWarmupRequestCount = nonFailedCount;
                    latencyStopwatch.Start();
                }

                long elapsedSeconds = latencyStopwatch.ElapsedMilliseconds / 1000;
                Console.Write($", RPS: {(elapsedSeconds == 0 ? -1 : (nonFailedCount - actualWarmupRequestCount) / elapsedSeconds)}");

                if(elapsedSeconds - lastMinuteLatencyEmittedSecondsOnLatencyStopwatch > configuration.LatencyTracingIntervalInSeconds)
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
                    lastMinuteLatencyEmittedSecondsOnLatencyStopwatch = elapsedSeconds;
                }

                Console.WriteLine();

                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Could not make {configuration.TotalRequestCount} requests in {configuration.MaxRuntimeInSeconds} seconds.");
                    break;
                }

                await Task.Delay(1000);
            }

            long elapsedFinal = latencyStopwatch.ElapsedMilliseconds / 1000;
            int nonFailedCountFinal = countsByStatus.Where(x => x.Key < HttpStatusCode.BadRequest).Sum(p => p.Value);
            int nonFailedCountFinalForLatency = nonFailedCountFinal - actualWarmupRequestCount;
            Console.WriteLine($"Successfully made {nonFailedCountFinal} requests; {nonFailedCountFinalForLatency} requests in {elapsedFinal} seconds at {(elapsedFinal == 0 ? -1 : nonFailedCountFinalForLatency / elapsedFinal)} items/sec.");
            Console.WriteLine($"Partition key prefix: {dataSource.PartitionKeyValuePrefix}");
            Console.WriteLine("Counts by StatusCode:");
            Console.WriteLine(string.Join(", ", countsByStatus.Select(countForStatus => countForStatus.Key + ": " + countForStatus.Value)));

            List<TimeSpan> latenciesList = latencies.ToList();
            latenciesList.Sort();
            int nonWarmupRequestCount = latenciesList.Count;
            if(nonWarmupRequestCount > 0)
            {
                Console.WriteLine("Latencies (non-failed):"
                + $"   P90: {latenciesList[(int)(nonWarmupRequestCount * 0.90)].TotalMilliseconds}"
                + $"   P95: {latenciesList[(int)(nonWarmupRequestCount * 0.95)].TotalMilliseconds}"
                + $"   P99: {latenciesList[(int)(nonWarmupRequestCount * 0.99)].TotalMilliseconds}"
                + $"   P99.9: {latenciesList[(int)(nonWarmupRequestCount * 0.999)].TotalMilliseconds}"
                + $"   Max: {latenciesList[nonWarmupRequestCount - 1].TotalMilliseconds}");
            }

            Console.WriteLine("Average RUs (non-failed): " + (totalRequestCharge / (100.0 * nonFailedCountFinal)));
        }

    }
}