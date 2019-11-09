namespace CosmosBenchmark
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using CommandLine;
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
        private long itemsThrottled;
        private long itemsFailed;
        private CosmosClient client;
        private double[] RequestUnitsConsumed { get; set; }

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

            using (var ct = new ConsoleColoeContext(ConsoleColor.Green))
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
                RequestTimeout = new TimeSpan(1, 0, 0),
                MaxRetryAttemptsOnRateLimitedRequests = 0,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(60),
            };

            using (var client = new CosmosClient(
                options.EndPoint,
                accountKey,
                clientOptions))
            {
                var program = new Program(client);
                await program.RunAsync(options);
                Console.WriteLine("CosmosBenchmark completed successfully.");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        private static void HandleParseError(IEnumerable<Error> errors)
        {
            using (var ct = new ConsoleColoeContext(ConsoleColor.Red))
            {
                foreach (var e in errors)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            Environment.Exit(errors.Count());
        }

        private static readonly JsonSerializer Serializer = JsonSerializer.Create();

        private static MemoryStream ToStream<T>(T input)
        {
            MemoryStream streamPayload = new MemoryStream();
            using (StreamWriter streamWriter = new StreamWriter(streamPayload, 
                encoding: new UTF8Encoding(false, true), 
                bufferSize: 1024*1024, 
                leaveOpen: true))
            {
                using (JsonWriter writer = new JsonTextWriter(streamWriter))
                {
                    writer.Formatting = Newtonsoft.Json.Formatting.None;
                    Program.Serializer.Serialize(writer, input);
                    writer.Flush();
                    streamWriter.Flush();
                }
            }

            streamPayload.Position = 0;
            return streamPayload;
        }

        /// <summary>
        /// Run samples for Order By queries.
        /// </summary>
        /// <returns>a Task object.</returns>
        private async Task RunAsync(BenchmarkOptions options)
        {
            if (options.CleanupOnStart)
            {
                Database database = client.GetDatabase(options.Database);
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
            string sampleItemString = File.ReadAllText(options.ItemTemplateFile);

            pendingTaskCount = taskCount;

            string partitionKeyPath = containerResponse.Resource.PartitionKeyPath;
            long numberOfItemsToInsert = options.ItemCount / taskCount;

            Stopwatch serializationTime = new Stopwatch();
            serializationTime.Start();
            Tuple<string, MemoryStream>[] inputs = new Tuple<string, MemoryStream>[options.ItemCount];
            string partitionKeyProperty = partitionKeyPath.Replace("/", "");
            for (var i = 0; i < options.ItemCount; i++)
            {
                Dictionary<string, object> newDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(sampleItemString);

                string newPartitionKey = Guid.NewGuid().ToString();
                newDictionary["id"] = Guid.NewGuid().ToString();
                newDictionary[partitionKeyProperty] = newPartitionKey;

                MemoryStream ms = Program.ToStream(newDictionary);
                inputs[i] = new Tuple<string, MemoryStream>(newPartitionKey, ms);
            }
            serializationTime.Stop();
            Console.WriteLine($"Payload serialization took: {serializationTime.ElapsedMilliseconds/1000} seconds");

            Console.WriteLine("Starting STREAM inserts");
            ////var tasks = new List<Task>();
            ////tasks.Add(this.LogOutputStats());

            ////for (var i = 0; i < taskCount; i++)
            ////{
            ////    tasks.Add(this.InsertItem(i, container, inputs, numberOfItemsToInsert));
            ////}

            ////await Task.WhenAll(tasks);

            if (options.CleanupOnFinish)
            {
                Console.WriteLine($"Deleting Database {options.Database}");
                Database database = client.GetDatabase(options.Database);
                await database.DeleteStreamAsync();
            }
        }

        private async Task InsertItem(
            int taskId,
            Container container,
            Tuple<string, MemoryStream>[] inputs,
            long numberOfItemsToInsert)
        {
            this.RequestUnitsConsumed[taskId] = 0;

            long startIndex = taskId * numberOfItemsToInsert;
            long count = numberOfItemsToInsert;
            if (startIndex + numberOfItemsToInsert > inputs.Length)
            {
                count = inputs.Length - startIndex + 1;
            }

            try
            {
                for (var i = 0; i < count; i++)
                {
                    Tuple<string, MemoryStream> operationInput = inputs[startIndex + i];

                    var itemResponse = await container.CreateItemStreamAsync(
                            operationInput.Item2,
                            new PartitionKey(operationInput.Item1));

                    this.RequestUnitsConsumed[taskId] += itemResponse.Headers.RequestCharge;
                    if (itemResponse.IsSuccessStatusCode)
                    {
                        Interlocked.Increment(ref this.itemsInserted);
                    }
                    else if (itemResponse.StatusCode == (HttpStatusCode)429)
                    {
                        Interlocked.Increment(ref this.itemsThrottled);
                    }
                    else
                    {
                        Console.WriteLine($"Insert failed with statuscode: {itemResponse.StatusCode}");
                        Interlocked.Increment(ref this.itemsFailed);
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Insert task failed with {ex.ToString()}");
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref this.pendingTaskCount);
            }
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
                ruPerSecond = (requestUnits / seconds);
                ruPerMonth = ruPerSecond * 86400 * 30;

                Console.WriteLine("Summary: RPS: {0, 10}, RUPS: {1, 10}, Created: {2, 10}, Throttled: {3, 10}, Failed: {4, 10}",
                    Math.Round(this.itemsInserted / seconds),
                    Math.Round(ruPerSecond),
                    currentCount,
                    this.itemsThrottled,
                    this.itemsFailed);

                lastCount = itemsInserted;
                lastSeconds = seconds;
                lastRequestUnits = requestUnits;
            }

            double totalSeconds = watch.Elapsed.TotalSeconds;
            ruPerSecond = (requestUnits / totalSeconds);
            ruPerMonth = ruPerSecond * 86400 * 30;

            using (var ct = new ConsoleColoeContext(ConsoleColor.Green))
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
            Database database = await client.CreateDatabaseIfNotExistsAsync(options.Database);

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
    }
}
