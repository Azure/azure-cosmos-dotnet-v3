//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents.Client;

    /// <summary>
    /// This sample demonstrates how to achieve high performance writes using Azure Comsos DB.
    /// </summary>
    public sealed class Program
    {
        private CosmosClient client;
        private DocumentClient documentClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="client">The Azure Cosmos DB client instance.</param>
        private Program(CosmosClient client, DocumentClient documentClient)
        {
            this.client = client;
            this.documentClient = documentClient;
        }

        /// <summary>
        /// Main method for the sample.
        /// </summary>
        /// <param name="args">command line arguments.</param>
        public static async Task Main(string[] args)
        {
            BenchmarkConfig config = BenchmarkConfig.From(args);
            ThreadPool.SetMinThreads(config.MinThreadPoolSize, config.MinThreadPoolSize);

            string accountKey = config.Key;
            config.Key = null; // Don't print
            config.Print();

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = ConsistencyLevel.Eventual,
                ApplicationName = "cosmosdbdotnetbenchmark",
                RequestTimeout = new TimeSpan(1, 0, 0),
                MaxRetryAttemptsOnRateLimitedRequests = 0,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(60),
            };

            using (DocumentClient documentClient = new DocumentClient(new Uri(config.EndPoint),
                accountKey,
                new ConnectionPolicy()
                {
                    ConnectionMode = Microsoft.Azure.Documents.Client.ConnectionMode.Direct,
                    ConnectionProtocol = Protocol.Tcp,
                },
                Microsoft.Azure.Documents.ConsistencyLevel.Eventual))
            {
                await documentClient.OpenAsync();

                using (CosmosClient client = new CosmosClient(
                    config.EndPoint,
                    accountKey,
                    clientOptions))
                {
                    Program program = new Program(client, documentClient);
                    await program.ExecuteAsync(config);
                }
            }

            TelemetrySpan.LatencyHistogram.OutputPercentileDistribution(Console.Out);
            using (StreamWriter fileWriter = new StreamWriter("HistogramResults.hgrm"))
            {
                TelemetrySpan.LatencyHistogram.OutputPercentileDistribution(fileWriter);
            }

            Console.WriteLine($"{nameof(CosmosBenchmark)} completed successfully.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        /// <summary>
        /// Run samples for Order By queries.
        /// </summary>
        /// <returns>a Task object.</returns>
        private async Task ExecuteAsync(BenchmarkConfig config)
        {
            if (config.CleanupOnStart)
            {
                Database database = this.client.GetDatabase(config.Database);
                await database.DeleteStreamAsync();
            }

            ContainerResponse containerResponse = await this.CreatePartitionedContainerAsync(config);
            Container container = containerResponse;

            int? currentContainerThroughput = await container.ReadThroughputAsync();
            Console.WriteLine($"Using container {config.Container} with {currentContainerThroughput} RU/s");

            int taskCount = config.GetTaskCount(currentContainerThroughput.Value);

            Console.WriteLine("Starting Inserts with {0} tasks", taskCount);
            Console.WriteLine();

            string partitionKeyPath = containerResponse.Resource.PartitionKeyPath;
            int numberOfItemsToInsert = config.ItemCount / taskCount;
            string sampleItem = File.ReadAllText(config.ItemTemplateFile);

            Func<IBenchmarkOperatrion> benchmarkOperationFactory = () =>
            {
                IBenchmarkOperatrion benchmarkOperation = null;
                switch (config.WorkloadType.ToLower())
                {
                    case "insert":
                        if (config.UseV2Client)
                        {
                            benchmarkOperation = new InsertV2BenchmarkOperation(
                                this.documentClient,
                                config.Database,
                                config.Container,
                                partitionKeyPath,
                                sampleItem);
                        }
                        else
                        {
                            benchmarkOperation = new InsertV3BenchmarkOperation(
                                config.Database,
                                container,
                                partitionKeyPath,
                                sampleItem);
                        }
                        break;
                    case "read":
                        if (config.UseV2Client)
                        {
                            benchmarkOperation = new ReadExistsV2BenchmarkOperation(
                                this.documentClient,
                                config.Database,
                                config.Container,
                                partitionKeyPath,
                                sampleItem);
                        }
                        else
                        {
                            benchmarkOperation = new ReadExistsV3BenchmarkOperation(
                                config.Database,
                                container,
                                partitionKeyPath,
                                sampleItem);
                        }
                        break;
                    case "readnotfound":
                        if (config.UseV2Client)
                        {
                            benchmarkOperation = new ReadNotExistsV2BenchmarkOperation(
                                this.documentClient,
                                config.Database,
                                config.Container);
                        }
                        else
                        {
                            benchmarkOperation = new ReadNotExistsV3BenchmarkOperation(
                                config.Database,
                                container);
                        }
                        break;
                    default:
                        throw new NotImplementedException($"Unsupported workload type {config.WorkloadType}");
                }

                return benchmarkOperation;
            };

            IExecutionStrategy execution = IExecutionStrategy.StartNew(config, benchmarkOperationFactory);
            await execution.ExecuteAsync(taskCount, numberOfItemsToInsert, 0.01);

            if (config.CleanupOnFinish)
            {
                Console.WriteLine($"Deleting Database {config.Database}");
                Database database = this.client.GetDatabase(config.Database);
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
