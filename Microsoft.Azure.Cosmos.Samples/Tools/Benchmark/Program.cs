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
    using System.Diagnostics;

    /// <summary>
    /// This sample demonstrates how to achieve high performance writes using Azure Comsos DB.
    /// </summary>
    public sealed class Program
    {
        /// <summary>
        /// Main method for the sample.
        /// </summary>
        /// <param name="args">command line arguments.</param>
        public static async Task Main(string[] args)
        {
            try
            {
                BenchmarkConfig config = BenchmarkConfig.From(args);
                ThreadPool.SetMinThreads(config.MinThreadPoolSize, config.MinThreadPoolSize);
                TelemetrySpan.IncludePercentile = config.EnableLatencyPercentiles;

                string accountKey = config.Key;
                config.Key = null; // Don't print
                config.Print();

                Program program = new Program();

                RunSummary runSummary = await program.ExecuteAsync(config, accountKey);

                if (TelemetrySpan.IncludePercentile)
                {
                    TelemetrySpan.LatencyHistogram.OutputPercentileDistribution(Console.Out);
                    using (StreamWriter fileWriter = new StreamWriter("HistogramResults.hgrm"))
                    {
                        TelemetrySpan.LatencyHistogram.OutputPercentileDistribution(fileWriter);
                    }
                }
            }
            finally
            {
                Console.WriteLine($"{nameof(CosmosBenchmark)} completed successfully.");
                if (Debugger.IsAttached)
                {
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadLine();
                }
            }
        }

        /// <summary>
        /// Run samples for Order By queries.
        /// </summary>
        /// <returns>a Task object.</returns>
        private async Task<RunSummary> ExecuteAsync(BenchmarkConfig config, string accountKey)
        {
            using (CosmosClient cosmosClient = config.CreateCosmosClient(accountKey))
            {
                if (config.CleanupOnStart)
                {
                    Microsoft.Azure.Cosmos.Database database = cosmosClient.GetDatabase(config.Database);
                    await database.DeleteStreamAsync();
                }

                ContainerResponse containerResponse = await Program.CreatePartitionedContainerAsync(config, cosmosClient);
                Container container = containerResponse;

                int? currentContainerThroughput = await container.ReadThroughputAsync();
                Console.WriteLine($"Using container {config.Container} with {currentContainerThroughput} RU/s");

                int taskCount = config.GetTaskCount(currentContainerThroughput.Value);

                Console.WriteLine("Starting Inserts with {0} tasks", taskCount);
                Console.WriteLine();

                string partitionKeyPath = containerResponse.Resource.PartitionKeyPath;
                int numberOfItemsToInsert = config.ItemCount / taskCount;

                // TBD: 2 clients SxS some overhead
                RunSummary runSummary;
                using (DocumentClient documentClient = config.CreateDocumentClient(accountKey))
                {
                    Func<IBenchmarkOperatrion> benchmarkOperationFactory = this.GetBenchmarkFactory(
                        config,
                        partitionKeyPath,
                        cosmosClient,
                        documentClient);

                    IExecutionStrategy execution = IExecutionStrategy.StartNew(config, benchmarkOperationFactory);
                    runSummary = await execution.ExecuteAsync(taskCount, numberOfItemsToInsert, config.TraceFailures, 0.01);
                }

                if (config.CleanupOnFinish)
                {
                    Console.WriteLine($"Deleting Database {config.Database}");
                    Microsoft.Azure.Cosmos.Database database = cosmosClient.GetDatabase(config.Database);
                    await database.DeleteStreamAsync();
                }

                runSummary.id = config.RunId;
                runSummary.Commit = config.CommitId;

                if (config.PublicResults)
                {
                    Container resultsContainer = cosmosClient.GetContainer(config.Database, config.ResultsContainer);
                    await resultsContainer.CreateItemAsync(runSummary, new PartitionKey(runSummary.Pk));
                }

                return runSummary;
            }
        }

        private Func<IBenchmarkOperatrion> GetBenchmarkFactory(
            BenchmarkConfig config,
            string partitionKeyPath,
            CosmosClient cosmosClient,
            DocumentClient documentClient)
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
                        cosmosClient,
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
                        documentClient,
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

            return () => (IBenchmarkOperatrion)ci.Invoke(ctorArguments);
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
        private static async Task<ContainerResponse> CreatePartitionedContainerAsync(BenchmarkConfig options, CosmosClient cosmosClient)
        {
            Microsoft.Azure.Cosmos.Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(options.Database);

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
