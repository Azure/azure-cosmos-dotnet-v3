//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
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

        internal struct OperationResult
        {
            public string DatabseName { get; set; }
            public string ContainerName { get; set; }
            public double RuCharges { get; set; }

            public Func<string> lazyDiagnostics { get; set; }
        }

        private interface IBenchmarkOperatrion
        {
            void Prepare();

            Task<OperationResult> ExecuteOnceAsync();
        }

        private class InsertBenchmarkOperation : IBenchmarkOperatrion
        {
            private readonly Container container;
            private readonly string partitionKeyPath;
            private readonly Dictionary<string, object> sampleJObject;

            private readonly string databsaeName;
            private readonly string containerName;

            private Stream nextExecutionItemPayload;
            private string nextExecutionItemPartitionKey;

            public InsertBenchmarkOperation(
                Container container,
                string partitionKeyPath,
                string sampleJson)
            {
                this.container = container;
                this.partitionKeyPath = partitionKeyPath.Replace("/", "");

                this.databsaeName = container.Database.Id;
                this.containerName = container.Id;

                this.sampleJObject = JsonHelper.Deserialize<Dictionary<string, object>>(sampleJson);
            }

            public async Task<OperationResult> ExecuteOnceAsync()
            {
                using (Stream inputStream = this.nextExecutionItemPayload)
                {
                    ResponseMessage itemResponse = await this.container.CreateItemStreamAsync(
                            inputStream,
                            new PartitionKey(this.nextExecutionItemPartitionKey));

                    double ruCharges = itemResponse.Headers.RequestCharge;
                    return new OperationResult()
                    {
                        DatabseName = databsaeName,
                        ContainerName = containerName,
                        RuCharges = ruCharges,
                        lazyDiagnostics = () => itemResponse.Diagnostics.ToString(),
                    };
                }
            }

            public void Prepare()
            {
                string newPartitionKey = Guid.NewGuid().ToString();
                this.sampleJObject["id"] = Guid.NewGuid().ToString();
                this.sampleJObject[this.partitionKeyPath] = newPartitionKey;

                this.nextExecutionItemPayload = JsonHelper.ToStream(this.sampleJObject);
                this.nextExecutionItemPartitionKey = newPartitionKey;
            }
        }

        private delegate Task<OperationResult> BenchmarkOperation();

        private interface IExecutionStrategy
        {
            public static IExecutionStrategy StartNew(
                BenchmarkConfig config,
                IBenchmarkOperatrion benchmarkOperation)
            {
                return new ParallelExecutionStrategy(benchmarkOperation);
            }

            public Task ExecuteAsync(
                int serialExecutorConcurrency,
                int serialExecutorIterationCount,
                double warmupFraction);

        }

        private class ParallelExecutionStrategy : IExecutionStrategy
        {
            private readonly IBenchmarkOperatrion benchmarkOperation;

            private volatile int pendingExecutorCount;

            public ParallelExecutionStrategy(
                IBenchmarkOperatrion benchmarkOperation)
            {
                this.benchmarkOperation = benchmarkOperation;
            }

            public async Task ExecuteAsync(
                int serialExecutorConcurrency,
                int serialExecutorIterationCount,
                double warmupFraction)
            {
                IExecutor warmupExecutor = new SerialOperationExecutor(
                            executorId: "Warmup",
                            benchmarkOperation: this.benchmarkOperation);
                await warmupExecutor.ExecuteAsync(
                        (int)(serialExecutorIterationCount * warmupFraction),
                        isWarmup: true,
                        completionCallback: () => { });

                IExecutor[] executors = new IExecutor[serialExecutorConcurrency];
                for (int i = 0; i < serialExecutorConcurrency; i++)
                {
                    executors[i] = new SerialOperationExecutor(
                                executorId: i.ToString(),
                                benchmarkOperation: this.benchmarkOperation);
                }

                this.pendingExecutorCount = serialExecutorConcurrency;
                for (int i = 0; i < serialExecutorConcurrency; i++)
                {
                    _ = executors[i].ExecuteAsync(
                            iterationCount: serialExecutorIterationCount,
                            isWarmup: false,
                            completionCallback: () => Interlocked.Decrement(ref this.pendingExecutorCount));
                }

                await this.LogOutputStats(executors);
            }

            private async Task LogOutputStats(IExecutor[] executors)
            {
                const int outputLoopDelayInSeconds = 5;
                Summary lastSummary = new Summary();

                Stopwatch watch = new Stopwatch();
                watch.Start();

                bool isLastIterationCompleted = false;
                do
                {
                    isLastIterationCompleted = this.pendingExecutorCount <= 0;

                    Summary currentTotalSummary = new Summary();
                    for (int i = 0; i < executors.Length; i++)
                    {
                        IExecutor executor = executors[i];
                        Summary executorSummary = new Summary()
                        {
                            succesfulOpsCount = executor.SuccessOperationCount,
                            failedOpsCount = executor.FailedOperationCount,
                            ruCharges = executor.TotalRuCharges,
                        };

                        currentTotalSummary += executorSummary;
                    }

                    // In-theory summary might be lower than real as its not transactional on time
                    currentTotalSummary.elapsedMs = watch.Elapsed.TotalMilliseconds;

                    Summary diff = currentTotalSummary - lastSummary;
                    lastSummary = currentTotalSummary;

                    diff.Print(currentTotalSummary.failedOpsCount + currentTotalSummary.succesfulOpsCount);

                    await Task.Delay(TimeSpan.FromSeconds(outputLoopDelayInSeconds));
                }
                while (!isLastIterationCompleted);

                using (ConsoleColorContext ct = new ConsoleColorContext(ConsoleColor.Green))
                {
                    Console.WriteLine();
                    Console.WriteLine("Summary:");
                    Console.WriteLine("--------------------------------------------------------------------- ");
                    lastSummary.Print(lastSummary.failedOpsCount + lastSummary.succesfulOpsCount);
                    Console.WriteLine("--------------------------------------------------------------------- ");
                }
            }

            private struct Summary
            {
                public long succesfulOpsCount;
                public long failedOpsCount;
                public double ruCharges;
                public double elapsedMs;

                public double Rups()
                {
                    return Math.Round(this.ruCharges / this.elapsedMs * 1000, 2);
                }

                public double Rps()
                {
                    return Math.Round((this.succesfulOpsCount + this.failedOpsCount) / this.elapsedMs * 1000, 2);
                }

                public void Print(long globalTotal)
                {
                    Console.WriteLine("Stats, total: {0,5}   success: {1,5}   fail: {2,3}   RPS: {3,5}   rups: {4,5}",
                        globalTotal,
                        this.succesfulOpsCount,
                        this.failedOpsCount,
                        this.Rps(),
                        this.Rups());
                }

                public static Summary operator +(Summary arg1, Summary arg2)
                {
                    return new Summary()
                    {
                        succesfulOpsCount = arg1.succesfulOpsCount + arg2.succesfulOpsCount,
                        failedOpsCount = arg1.failedOpsCount + arg2.failedOpsCount,
                        ruCharges = arg1.ruCharges + arg2.ruCharges,
                        elapsedMs = arg1.elapsedMs + arg2.elapsedMs,
                    };
                }

                public static Summary operator -(Summary arg1, Summary arg2)
                {
                    return new Summary()
                    {
                        succesfulOpsCount = arg1.succesfulOpsCount - arg2.succesfulOpsCount,
                        failedOpsCount = arg1.failedOpsCount - arg2.failedOpsCount,
                        ruCharges = arg1.ruCharges - arg2.ruCharges,
                        elapsedMs = arg1.elapsedMs - arg2.elapsedMs,
                    };
                }
            }

        }

        private interface IExecutor
        {
            public int SuccessOperationCount { get; }
            public int FailedOperationCount { get; }
            public double TotalRuCharges { get; }

            public Task ExecuteAsync(
                    int iterationCount,
                    bool isWarmup,
                    Action completionCallback);
        }

        private class SerialOperationExecutor : IExecutor
        {
            private readonly IBenchmarkOperatrion operation;
            private readonly string executorId;

            public SerialOperationExecutor(
                string executorId,
                IBenchmarkOperatrion benchmarkOperation)
            {
                this.executorId = executorId;
                this.operation = benchmarkOperation;

                this.SuccessOperationCount = 0;
                this.FailedOperationCount = 0;
            }

            public int SuccessOperationCount { get; private set; }
            public int FailedOperationCount { get; private set; }

            public double TotalRuCharges { get; private set; }

            public async Task ExecuteAsync(
                    int iterationCount,
                    bool isWarmup,
                    Action completionCallback)
            {
                Trace.TraceInformation($"Executor {this.executorId} started");

                try
                {
                    int currentIterationCount = 0;
                    do
                    {
                        OperationResult? operationResult = null;

                        this.operation.Prepare();

                        using (TelemetrySpan telemetrySpan = TelemetrySpan.StartNew(
                                    () => operationResult.Value,
                                    disableTelemetry: isWarmup))
                        {
                            try
                            {
                                operationResult = await this.operation.ExecuteOnceAsync();

                                // Success case
                                this.SuccessOperationCount++;
                                this.TotalRuCharges += operationResult.Value.RuCharges;
                            }
                            catch (Exception ex)
                            {
                                // failure case
                                this.FailedOperationCount++;

                                // Special case of cosmos exception
                                double opCharge = 0;
                                if (ex is CosmosException cosmosException)
                                {
                                    opCharge = cosmosException.RequestCharge;
                                    this.TotalRuCharges += opCharge;
                                }

                                operationResult = new OperationResult()
                                {
                                    // TODO: Populate account, databse, collection context into ComsosDiagnostics
                                    RuCharges = opCharge,
                                    lazyDiagnostics = () => ex.ToString(),
                                };
                            }
                        }

                        currentIterationCount++;
                    } while (currentIterationCount < iterationCount);

                    Trace.TraceInformation($"Executor {this.executorId} completed");
                }
                finally
                {
                    completionCallback();
                }
            }
        }
    }
}
