//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using CommandLine;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// This sample demonstrates how to achieve high performance writes using Azure Comsos DB.
    /// </summary>
    public sealed class Program
    {
        private CosmosClient client;

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
            BenchmarkConfig options = null;
            Parser.Default.ParseArguments<BenchmarkConfig>(args)
                .WithParsed<BenchmarkConfig>(e => options = e)
                .WithNotParsed<BenchmarkConfig>(e => Program.HandleParseError(e));

            ThreadPool.SetMinThreads(options.MinThreadPoolSize, options.MinThreadPoolSize);

            string accountKey = options.Key;
            options.Key = null; // Don't print 

            using (ConsoleColorContext ct = new ConsoleColorContext(ConsoleColor.Green))
            {

                Console.WriteLine($"{nameof(CosmosBenchmark)} started with arguments");
                Console.WriteLine("--------------------------------------------------------------------- ");
                Console.WriteLine(JsonHelper.ToString(options));
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

                await program.RunAsync(options);

                Console.WriteLine("CosmosBenchmark completed successfully.");
            }

            using (StreamWriter fileWriter = new StreamWriter("HistogramResults.hgrm"))
            {
                TelemetrySpan.LatencyHistogram.OutputPercentileDistribution(fileWriter);
            }

            TelemetrySpan.LatencyHistogram.OutputPercentileDistribution(Console.Out);

            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        private static void HandleParseError(IEnumerable<Error> errors)
        {
            using (ConsoleColorContext ct = new ConsoleColorContext(ConsoleColor.Red))
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
        private async Task RunAsync(BenchmarkConfig options)
        {
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

            Console.WriteLine("Starting Inserts with {0} tasks", taskCount);
            Console.WriteLine();
            string sampleItem = File.ReadAllText(options.ItemTemplateFile);

            string partitionKeyPath = containerResponse.Resource.PartitionKeyPath;
            int numberOfItemsToInsert = options.ItemCount / taskCount;

            IBenchmarkOperatrion insertBenchmarkOperatrion = new InsertBenchmarkOperation(container, partitionKeyPath, sampleItem);
            IExecutionStrategy execution = IExecutionStrategy.StartNew(options, insertBenchmarkOperatrion);
            await execution.ExecuteAsync(taskCount, numberOfItemsToInsert, 0.01);

            if (options.CleanupOnFinish)
            {
                Console.WriteLine($"Deleting Database {options.Database}");
                Database database = this.client.GetDatabase(options.Database);
                await database.DeleteStreamAsync();
            }
        }

        /// <summary>
        /// Create a partitioned container.
        /// </summary>
        /// <returns>The created container.</returns>
        private async Task<ContainerResponse> CreatePartitionedContainerAsync(BenchmarkConfig options)
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
    }
}
