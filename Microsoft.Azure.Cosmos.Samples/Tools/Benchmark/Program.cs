//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.IO;
    using System.Net;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents.Client;
    using System.Collections.Generic;
    using System.Reflection;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This sample demonstrates how to achieve high performance writes using Azure Comsos DB.
    /// </summary>
    public sealed class Program
    {
        private CosmosClient cosmosClient;
        private DocumentClient documentClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="client">The Azure Cosmos DB client instance.</param>
        private Program(CosmosClient client, DocumentClient documentClient)
        {
            this.cosmosClient = client;
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
                ApplicationName = "cosmosdbdotnetbenchmark",
                RequestTimeout = new TimeSpan(1, 0, 0),
                MaxRetryAttemptsOnRateLimitedRequests = 0,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(60),
            };

            Microsoft.Azure.Documents.ConsistencyLevel? consistencyLevel = null;
            if (!string.IsNullOrWhiteSpace(config.ConsistencyLevel))
            {
                clientOptions.ConsistencyLevel = (Microsoft.Azure.Cosmos.ConsistencyLevel)Enum.Parse(typeof(Microsoft.Azure.Cosmos.ConsistencyLevel), config.ConsistencyLevel, ignoreCase: true);
                consistencyLevel = (Microsoft.Azure.Documents.ConsistencyLevel)Enum.Parse(typeof(Microsoft.Azure.Documents.ConsistencyLevel), config.ConsistencyLevel, ignoreCase: true);
            }

            using (DocumentClient documentClient = new DocumentClient(new Uri(config.EndPoint),
                accountKey,
                new ConnectionPolicy()
                {
                    ConnectionMode = Microsoft.Azure.Documents.Client.ConnectionMode.Direct,
                    ConnectionProtocol = Protocol.Tcp,
                },
                desiredConsistencyLevel: consistencyLevel))
            {
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
                Microsoft.Azure.Cosmos.Database database = this.cosmosClient.GetDatabase(config.Database);
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

            Func<IBenchmarkOperatrion> benchmarkOperationFactory = this.GetBenchmarkFactory(config, partitionKeyPath);
            IExecutionStrategy execution = IExecutionStrategy.StartNew(config, benchmarkOperationFactory);
            await execution.ExecuteAsync(taskCount, numberOfItemsToInsert, 0.01);

            if (config.CleanupOnFinish)
            {
                Console.WriteLine($"Deleting Database {config.Database}");
                Microsoft.Azure.Cosmos.Database database = this.cosmosClient.GetDatabase(config.Database);
                await database.DeleteStreamAsync();
            }
        }

        private Func<IBenchmarkOperatrion> GetBenchmarkFactory(BenchmarkConfig config, string partitionKeyPath)
        {
            string sampleItem = File.ReadAllText(config.ItemTemplateFile);

            Type[] availableBenchmarks = Program.AvailableBenchmarks();
            IEnumerable<Type> res = availableBenchmarks
                .Where(e => e.Name.Equals(config.WorkloadType, StringComparison.OrdinalIgnoreCase) || e.Name.Equals(config.WorkloadType + "BenchmarkOperation", StringComparison.OrdinalIgnoreCase));

            if (res.Count() != 1)
            {
                throw new NotImplementedException($"Unsupported workload type {config.WorkloadType}. Available ones are " +
                    string.Join(", \r\n", availableBenchmarks.Select(e => e.Name)));
            }

            ConstructorInfo ci = null;
            object[] ctorArguments = null;
            Type benchmarkTypeName = res.Single();

            if (benchmarkTypeName.Name.EndsWith("V3BenchmarkOperation"))
            {
                ci = benchmarkTypeName.GetConstructor(new Type[] { typeof(CosmosClient), typeof(string), typeof(string), typeof(string), typeof(string) });
                ctorArguments = new object[]
                    {
                        this.cosmosClient,
                        config.Database,
                        config.Container,
                        partitionKeyPath,
                        sampleItem
                    };
            }
            else if (benchmarkTypeName.Name.EndsWith("V2BenchmarkOperation"))
            {
                ci = benchmarkTypeName.GetConstructor(new Type[] { typeof(DocumentClient), typeof(string), typeof(string), typeof(string), typeof(string) });
                ctorArguments = new object[]
                    {
                        this.documentClient,
                        config.Database,
                        config.Container,
                        partitionKeyPath,
                        sampleItem
                    };
            }

            if (ci == null)
            {
                throw new NotImplementedException($"Unsupported CTOR for workload type {config.WorkloadType} ");
            }

            Func<IBenchmarkOperatrion> benchmarkOperationFactory = () =>
            {
                return (IBenchmarkOperatrion)ci.Invoke(ctorArguments);
            };
            return benchmarkOperationFactory;
        }

        private static Type[] AvailableBenchmarks()
        {
            Type benchmarkType = typeof(IBenchmarkOperatrion);
            return typeof(Program).Assembly.GetTypes()
                .Where(p => benchmarkType.IsAssignableFrom(p))
                .ToArray();
        }

        /// <summary>
        /// Create a partitioned container.
        /// </summary>
        /// <returns>The created container.</returns>
        private async Task<ContainerResponse> CreatePartitionedContainerAsync(BenchmarkConfig options)
        {
            Microsoft.Azure.Cosmos.Database database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(options.Database);

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
