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
                System.Diagnostics.Trace.TraceInformation(nameof(System.Diagnostics.Trace.TraceInformation));
                DefaultTrace.TraceInformation(nameof(DefaultTrace.TraceInformation));
                System.Diagnostics.Trace.Flush();
                DefaultTrace.Flush();

                DefaultTrace.PrintListeners();
                DefaultTrace.ClearListeners();

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
                int opsPerTask = config.ItemCount / taskCount;

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
                    runSummary = await execution.ExecuteAsync(taskCount, opsPerTask, config.TraceFailures, 0.01);
                }

                if (config.CleanupOnFinish)
                {
                    Console.WriteLine($"Deleting Database {config.Database}");
                    Microsoft.Azure.Cosmos.Database database = cosmosClient.GetDatabase(config.Database);
                    await database.DeleteStreamAsync();
                }

                runSummary.WorkloadType = config.WorkloadType;
                runSummary.id = $"{DateTime.UtcNow.ToString("yyyy-MM-dd:HH-mm")}-{config.CommitId}";
                runSummary.Commit = config.CommitId;
                runSummary.CommitDate = config.CommitDate;
                runSummary.CommitTime = config.CommitTime;

                runSummary.Date = DateTime.UtcNow.ToString("yyyy-MM-dd");
                runSummary.Time = DateTime.UtcNow.ToString("HH-mm");
                runSummary.BranchName = config.BranchName;
                runSummary.TotalOps = config.ItemCount;
                runSummary.Concurrency = taskCount;
                runSummary.Database = config.Database;
                runSummary.Container = config.Container;
                runSummary.AccountName = config.EndPoint;
                runSummary.pk = config.ResultsPartitionKeyValue;

                string consistencyLevel = config.ConsistencyLevel;
                if (string.IsNullOrWhiteSpace(consistencyLevel))
                {
                    AccountProperties accountProperties = await cosmosClient.ReadAccountAsync();
                    consistencyLevel = accountProperties.Consistency.DefaultConsistencyLevel.ToString();
                }
                runSummary.ConsistencyLevel = consistencyLevel;


                if (config.PublishResults)
                {
                    Container resultsContainer = cosmosClient.GetContainer(config.Database, config.ResultsContainer);
                    await resultsContainer.CreateItemAsync(runSummary, new PartitionKey(runSummary.pk));
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

        internal static class DefaultTrace
        {
            private static readonly TraceSource TraceSourceInternal = new TraceSource("DocDBTrace");

            static DefaultTrace()
            {
                // From MSDN: http://msdn.microsoft.com/en-us/library/system.diagnostics.trace.usegloballock%28v=vs.110%29.aspx
                // The global lock is always used if the trace listener is not thread safe,
                // regardless of the value of UseGlobalLock. The IsThreadSafe property is used to determine
                // if the listener is thread safe. The global lock is not used only if the value of
                // UseGlobalLock is false and the value of IsThreadSafe is true.
                // The default behavior is to use the global lock.
                System.Diagnostics.Trace.UseGlobalLock = false;

                SourceSwitch sourceSwitch = new SourceSwitch("ClientSwitch", "Information");
                DefaultTrace.TraceSourceInternal.Switch = sourceSwitch;
            }

            public static void PrintListeners()
            {
                foreach(TraceListener listener in DefaultTrace.TraceSourceInternal.Listeners)
                {
                    Console.WriteLine($"Attached listener {listener.Name} -> {listener.GetType().FullName}");
                }
            }

            public static void ClearListeners()
            {
                DefaultTrace.TraceSourceInternal.Listeners.Clear();
            }

            public static TraceSource TraceSource
            {
                get { return DefaultTrace.TraceSourceInternal; }
            }

            public static void Flush()
            {
                DefaultTrace.TraceSource.Flush();
            }

            public static void TraceVerbose(string message)
            {
                DefaultTrace.TraceSource.TraceEvent(TraceEventType.Verbose, 0, message);
            }

            public static void TraceVerbose(string format, params object[] args)
            {
                DefaultTrace.TraceSource.TraceEvent(TraceEventType.Verbose, 0, format, args);
            }

            public static void TraceInformation(string message)
            {
                DefaultTrace.TraceSource.TraceInformation(message);
            }

            public static void TraceInformation(string format, params object[] args)
            {
                DefaultTrace.TraceSource.TraceInformation(format, args);
            }

            public static void TraceWarning(string message)
            {
                DefaultTrace.TraceSource.TraceEvent(TraceEventType.Warning, 0, message);
            }

            public static void TraceWarning(string format, params object[] args)
            {
                DefaultTrace.TraceSource.TraceEvent(TraceEventType.Warning, 0, format, args);
            }

            public static void TraceError(string message)
            {
                DefaultTrace.TraceSource.TraceEvent(TraceEventType.Error, 0, message);
            }

            public static void TraceError(string format, params object[] args)
            {
                DefaultTrace.TraceSource.TraceEvent(TraceEventType.Error, 0, format, args);
            }

            public static void TraceCritical(string message)
            {
                DefaultTrace.TraceSource.TraceEvent(TraceEventType.Critical, 0, message);
            }

            public static void TraceCritical(string format, params object[] args)
            {
                DefaultTrace.TraceSource.TraceEvent(TraceEventType.Critical, 0, format, args);
            }

            /// <summary>
            /// Emit a trace for a set of metric values.
            /// This is intended to be used next to MDM metrics
            /// Details:
            /// Produce a semi-typed trace format as a pipe delimited list of metrics values.
            /// 'TraceMetrics' prefix provides a search term for indexing.
            /// 'name' is an identifier to correlate to call site
            /// Example: TraceMetric|LogServicePoolInfo|0|123|1.
            /// </summary>
            /// <param name="name">metric name.</param>
            /// <param name="values">sequence of values to be emitted in the trace.</param>
            internal static void TraceMetrics(string name, params object[] values)
            {
                DefaultTrace.TraceInformation(string.Join("|", new object[] { "TraceMetrics", name }.Concat(values)));
            }
        }
    }
}
