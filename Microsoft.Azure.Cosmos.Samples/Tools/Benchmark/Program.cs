namespace CosmosBenchmark
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using CommandLine;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;

    /// <summary>
    /// This sample demonstrates how to achieve high performance writes using Azure Comsos DB.
    /// </summary>
    public sealed class Program
    {
        private static readonly string InstanceId = Dns.GetHostEntry("LocalHost").HostName + Process.GetCurrentProcess().Id;

        private int pendingTaskCount;
        private long itemsInserted;
        private CosmosClient client;
        private double[] RequestUnitsConsumed { get; set; }
        internal static HistogramBase latencyHistogram = new IntConcurrentHistogram(1, 10*1000, 0);

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="client">The Azure Cosmos DB client instance.</param>
        private Program(CosmosClient client)
        {
            this.client = client;
        }

        /// <summary>
        /// Main method for the sample.
        /// </summary>
        /// <param name="args">command line arguments.</param>
        public static async Task Main(string[] args)
        {
            BenchmarkOptions options = null;
            Parser.Default.ParseArguments<BenchmarkOptions>(args)
                .WithParsed<BenchmarkOptions>(e => options = e)
                .WithNotParsed<BenchmarkOptions>(e => Program.HandleParseError(e));

            ThreadPool.SetMinThreads(options.MinThreadPoolSize, options.MinThreadPoolSize);

            string accountKey = options.Key;
            options.Key = null; // Don't print 

            using (ConsoleColoeContext ct = new ConsoleColoeContext(ConsoleColor.Green))
            {

                Console.WriteLine($"{nameof(CosmosBenchmark)} started with arguments");
                Console.WriteLine("--------------------------------------------------------------------- ");
                Console.WriteLine(JsonConvert.SerializeObject(options, new JsonSerializerSettings()
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented,
                }));
                Console.WriteLine("--------------------------------------------------------------------- ");
                Console.WriteLine();
            }

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ApplicationName = "cosmosdbdotnetbenchmark",
                RequestTimeout = new TimeSpan(1, 0, 0),
                MaxRetryAttemptsOnRateLimitedRequests = 0,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(60),
                MaxRequestsPerTcpConnection = 2,
            };

            using (CosmosClient client = new CosmosClient(
                options.EndPoint,
                accountKey,
                clientOptions))
            {
                Program program = new Program(client);

                int tmp = options.ItemCount;

                options.ItemCount = tmp / 10;
                await program.RunAsync(options, true);
                Console.WriteLine("Press ENTER to resume");
                Console.ReadLine();

                options.ItemCount = tmp;
                await program.RunAsync(options, false);

                Console.WriteLine("CosmosBenchmark completed successfully.");
            }

            using (StreamWriter fileWriter = new StreamWriter("HistogramResults.hgrm"))
            {
                Program.latencyHistogram.OutputPercentileDistribution(fileWriter);
            }

            Program.latencyHistogram.OutputPercentileDistribution(Console.Out);

            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        private static void HandleParseError(IEnumerable<Error> errors)
        {
            using (ConsoleColoeContext ct = new ConsoleColoeContext(ConsoleColor.Red))
            {
                foreach (Error e in errors)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            Environment.Exit(errors.Count());
        }

        /// <summary>
        /// Run samples for Order By queries.
        /// </summary>
        /// <returns>a Task object.</returns>
        private async Task RunAsync(BenchmarkOptions options, bool warmup)
        {
            this.itemsInserted = 0;
            if (options.CleanupOnStart)
            {
                Database database = this.client.GetDatabase(options.Database);
                await database.DeleteStreamAsync();
            }

            ContainerResponse containerResponse = await this.CreatePartitionedContainerAsync(options);
            Container container = containerResponse;

            int? currentContainerThroughput = await container.ReadThroughputAsync();
            Console.WriteLine($"Using container {options.Container} with {currentContainerThroughput} RU/s");

            int taskCount = options.DegreeOfParallelism;
            if (taskCount == -1)
            {
                // set TaskCount = 10 for each 10k RUs, minimum 1, maximum { #processor * 50 }
                taskCount = Math.Max(currentContainerThroughput.Value / 1000, 1);
                taskCount = Math.Min(taskCount, Environment.ProcessorCount * 50);
            }

            this.RequestUnitsConsumed = new double[taskCount];

            Console.WriteLine("Starting Inserts with {0} tasks", taskCount);
            Console.WriteLine();
            string sampleItem = File.ReadAllText(options.ItemTemplateFile);

            this.pendingTaskCount = taskCount;
            List<Task> tasks = new List<Task>();
            tasks.Add(this.LogOutputStats());

            string partitionKeyPath = containerResponse.Resource.PartitionKeyPath;
            long numberOfItemsToInsert = options.ItemCount / taskCount;
            for (int i = 0; i < taskCount; i++)
            {
                tasks.Add(this.InsertItem(i, container, partitionKeyPath, sampleItem, numberOfItemsToInsert, warmup));
            }

            await Task.WhenAll(tasks);

            if (options.CleanupOnFinish)
            {
                Console.WriteLine($"Deleting Database {options.Database}");
                Database database = this.client.GetDatabase(options.Database);
                await database.DeleteStreamAsync();
            }
        }

        private async Task InsertItem(
            int taskId, 
            Container container, 
            string partitionKeyPath,
            string sampleJson, 
            long numberOfItemsToInsert,
            bool warmup)
        {
            this.RequestUnitsConsumed[taskId] = 0;
            string partitionKeyProperty = partitionKeyPath.Replace("/", "");
            Dictionary<string, object> newDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(sampleJson);

            int currentTraceLatency = 50;

            Stopwatch sw = new Stopwatch();
            for (int i = 0; i < numberOfItemsToInsert; i++)
            {
                string newPartitionKey = Guid.NewGuid().ToString();
                newDictionary["id"] = Guid.NewGuid().ToString();
                newDictionary[partitionKeyProperty] = newPartitionKey;

                using (Stream inputStream = Program.ToStream(newDictionary))
                {
                    try
                    {
                        sw.Restart();
                        ResponseMessage itemResponse = null;
                        try
                        {
                            itemResponse = await container.CreateItemStreamAsync(
                                    inputStream,
                                    new PartitionKey(newPartitionKey));
                        }
                        finally
                        {
                            sw.Stop();
                            if (! warmup)
                            {
                                Program.latencyHistogram.RecordValue(sw.ElapsedMilliseconds);
                                if (sw.ElapsedMilliseconds > currentTraceLatency && itemResponse != null)
                                {
                                    LatencyEventSource.Instance.LatencyDiagnostics(
                                        (int)sw.ElapsedMilliseconds, 
                                        itemResponse.Diagnostics.ToString());
                                }
                            }
                        }

                        string partition = itemResponse.Headers.Session.Split(':')[0];
                        this.RequestUnitsConsumed[taskId] += itemResponse.Headers.RequestCharge;
                        Interlocked.Increment(ref this.itemsInserted);
                    }
                    catch (CosmosException ex)
                    {
                        if (ex.StatusCode != HttpStatusCode.Forbidden)
                        {
                            Trace.TraceError($"Failed to write {JsonConvert.SerializeObject(newDictionary)}. Exception was {ex}");
                        }
                        else
                        {
                            Interlocked.Increment(ref this.itemsInserted);
                        }
                    }
                }
            }

            Interlocked.Decrement(ref this.pendingTaskCount);
        }

        private async Task LogOutputStats()
        {
            long lastCount = 0;
            double lastRequestUnits = 0;
            double lastSeconds = 0;
            double requestUnits = 0;
            double ruPerSecond = 0;
            double ruPerMonth = 0;

            Stopwatch watch = new Stopwatch();
            watch.Start();

            while (this.pendingTaskCount > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                double seconds = watch.Elapsed.TotalSeconds;

                requestUnits = this.RequestUnitsConsumed.Sum();

                long currentCount = this.itemsInserted;
                ruPerSecond = requestUnits / seconds;
                ruPerMonth = ruPerSecond * 86400 * 30;

                Console.WriteLine("Inserted {0} docs @ {1} writes/s, {2} RU/s ({3}B max monthly 1KB reads)",
                    currentCount,
                    Math.Round(this.itemsInserted / seconds),
                    Math.Round(ruPerSecond),
                    Math.Round(ruPerMonth / (1000 * 1000 * 1000)));

                lastCount = this.itemsInserted;
                lastSeconds = seconds;
                lastRequestUnits = requestUnits;
            }

            double totalSeconds = watch.Elapsed.TotalSeconds;
            ruPerSecond = requestUnits / totalSeconds;
            ruPerMonth = ruPerSecond * 86400 * 30;

            using (ConsoleColoeContext ct = new ConsoleColoeContext(ConsoleColor.Green))
            {
                Console.WriteLine();
                Console.WriteLine("Summary:");
                Console.WriteLine("--------------------------------------------------------------------- ");
                Console.WriteLine("Inserted {0} items @ {1} writes/s, {2} RU/s ({3}B max monthly 1KB reads)",
                    lastCount,
                    Math.Round(this.itemsInserted / watch.Elapsed.TotalSeconds),
                    Math.Round(ruPerSecond),
                    Math.Round(ruPerMonth / (1000 * 1000 * 1000)));
                Console.WriteLine("--------------------------------------------------------------------- ");
            }
        }

        /// <summary>
        /// Create a partitioned container.
        /// </summary>
        /// <returns>The created container.</returns>
        private async Task<ContainerResponse> CreatePartitionedContainerAsync(BenchmarkOptions options)
        {
            Database database = await this.client.CreateDatabaseIfNotExistsAsync(options.Database);

            Container container = database.GetContainer(options.Container);

            try
            {
                return await container.ReadContainerAsync();
            }
            catch(CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            { 
                // Show user cost of running this test
                double estimatedCostPerMonth = 0.06 * options.Throughput;
                double estimatedCostPerHour = estimatedCostPerMonth / (24 * 30);
                Console.WriteLine($"The container will cost an estimated ${Math.Round(estimatedCostPerHour, 2)} per hour (${Math.Round(estimatedCostPerMonth, 2)} per month)");
                Console.WriteLine("Press enter to continue ...");
                Console.ReadLine();

                string partitionKeyPath = options.PartitionKeyPath;
                return await database.CreateContainerAsync(options.Container, partitionKeyPath, options.Throughput);
            }
        }

        private class ConsoleColoeContext : IDisposable
        {
            ConsoleColor beforeContextColor;

            public ConsoleColoeContext(ConsoleColor color)
            {
                this.beforeContextColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
            }

            public void Dispose()
            {
                Console.ForegroundColor = this.beforeContextColor;
            }
        }

        private static readonly Encoding DefaultEncoding = new UTF8Encoding(false, true);
        private static readonly JsonSerializer serializer = JsonSerializer.Create();

        private static Stream ToStream<T>(T input)
        {
            MemoryStream streamPayload = new MemoryStream();
            using (StreamWriter streamWriter = new StreamWriter(streamPayload, encoding: Program.DefaultEncoding, bufferSize: 1024, leaveOpen: true))
            {
                using (JsonWriter writer = new JsonTextWriter(streamWriter))
                {
                    writer.Formatting = Newtonsoft.Json.Formatting.None;
                    JsonSerializer jsonSerializer = Program.serializer;
                    jsonSerializer.Serialize(writer, input);
                    writer.Flush();
                    streamWriter.Flush();
                }
            }

            streamPayload.Position = 0;
            return streamPayload;
        }

        [EventSource(Name = "Azure.Cosmos.Benchmark")]
        internal class LatencyEventSource : EventSource
        {
            public static LatencyEventSource Instance = new LatencyEventSource();

            private LatencyEventSource() 
            { 
            }

            [Event(1, Level = EventLevel.Informational)]
            public void LatencyDiagnostics(int latencyinMs, string dianostics)
            {
                this.WriteEvent(1, latencyinMs, dianostics);
            }
        }
    }
}
