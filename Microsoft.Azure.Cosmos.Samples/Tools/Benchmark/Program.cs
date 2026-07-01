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
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Monitor.OpenTelemetry.Exporter;
    using CosmosBenchmark.Fx;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;
    using OpenTelemetry;
    using OpenTelemetry.Metrics;
    using Container = Microsoft.Azure.Cosmos.Container;

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
                Environment.SetEnvironmentVariable("AZURE_COSMOS_THIN_CLIENT_ENABLED", config.IsThinClientEnabled.ToString());

                await AddAzureInfoToRunSummary();

                MeterProvider meterProvider = BuildMeterProvider(config);
                CosmosBenchmarkEventListener listener = new CosmosBenchmarkEventListener(meterProvider, config);

                ThreadPool.SetMinThreads(config.MinThreadPoolSize, config.MinThreadPoolSize);

                DiagnosticDataListener diagnosticDataListener = null;
                if (!string.IsNullOrEmpty(config.DiagnosticsStorageConnectionString))
                {
                    diagnosticDataListener = new DiagnosticDataListener(config);
                }

                if (config.EnableLatencyPercentiles)
                {
                    TelemetrySpan.IncludePercentile = true;
                    TelemetrySpan.ResetLatencyHistogram(config.ItemCount);
                }

                config.Print();

                Program program = new Program();

                RunSummary runSummary = await program.ExecuteAsync(config);

                if (!string.IsNullOrEmpty(config.DiagnosticsStorageConnectionString))
                {
                    diagnosticDataListener.UploadDiagnostcs();
                }
            }
            catch (Exception e)
            {
                Utility.TeeTraceInformation("Exception ocured:" + e.ToString());
            }
            finally
            {
                Environment.SetEnvironmentVariable("AZURE_COSMOS_THIN_CLIENT_ENABLED", "False");

                Utility.TeeTraceInformation($"{nameof(CosmosBenchmark)} completed successfully.");
                if (Debugger.IsAttached)
                {
                    Utility.TeeTraceInformation("Press any key to exit...");
                    Console.ReadLine();
                }
            }
        }

        /// <summary>
        /// Create a MeterProvider. If the App Insights connection string is not set, do not create an AppInsights Exporter.
        /// </summary>
        /// <returns></returns>
        private static MeterProvider BuildMeterProvider(BenchmarkConfig config)
        {
            MeterProviderBuilder meterProviderBuilder = Sdk.CreateMeterProviderBuilder();
            if (string.IsNullOrWhiteSpace(config.AppInsightsConnectionString))
            {
                foreach(string benchmarkName in MetricsCollector.GetBenchmarkMeterNames())
                {
                    meterProviderBuilder = meterProviderBuilder.AddMeter(benchmarkName);
                };

                return meterProviderBuilder.Build();
            }

            OpenTelemetry.Trace.TracerProviderBuilder tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
                .AddAzureMonitorTraceExporter();

            meterProviderBuilder = meterProviderBuilder.AddAzureMonitorMetricExporter(configure: new Action<AzureMonitorExporterOptions>(
                    (options) => options.ConnectionString = config.AppInsightsConnectionString));
            foreach (string benchmarkName in MetricsCollector.GetBenchmarkMeterNames())
            {
                meterProviderBuilder = meterProviderBuilder.AddMeter(benchmarkName);
            };

            return meterProviderBuilder.Build();
        }

        /// <summary>
        /// Adds Azure VM information to run summary.
        /// </summary>
        /// <returns></returns>
        private static async Task AddAzureInfoToRunSummary()
        {
            using HttpClient httpClient = new HttpClient();
            using HttpRequestMessage httpRequest = new HttpRequestMessage(
                HttpMethod.Get,
                "http://169.254.169.254/metadata/instance?api-version=2020-06-01");
            httpRequest.Headers.Add("Metadata", "true");

            try
            {
                using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequest);
                string jsonVmInfo = await httpResponseMessage.Content.ReadAsStringAsync();
                JObject jObject = JObject.Parse(jsonVmInfo);
                RunSummary.AzureVmInfo = jObject;
                RunSummary.Location = jObject["compute"]["location"].ToString();
                Utility.TeeTraceInformation($"Azure VM Location:{RunSummary.Location}");
            }
            catch (Exception e)
            {
                Utility.TeeTraceInformation("Failed to get Azure VM info:" + e.ToString());
            }
        }

        /// <summary>
        /// Executing benchmarks for V2/V3 cosmosdb SDK.
        /// </summary>
        /// <returns>a Task object.</returns>
        private async Task<RunSummary> ExecuteAsync(BenchmarkConfig config)
        {
            // V3 SDK client initialization

            using (CosmosClient cosmosClient = config.CreateCosmosClient())
            {
                Microsoft.Azure.Cosmos.Database database = cosmosClient.GetDatabase(config.Database);
                if (config.CleanupOnStart)
                {
                    await database.DeleteStreamAsync();
                }

                ContainerResponse containerResponse = await Program.CreatePartitionedContainerAsync(config, cosmosClient);
                Container container = containerResponse;

                // ReadThroughputAsync reads the offer, a control-plane operation that Cosmos
                // data-plane RBAC (AAD) does not support. Under keyless auth (--aad /
                // disableLocalAuth=true) it returns 403 Forbidden (substatus 5302). The value is
                // only needed to auto-derive the task count when --pl is not supplied; when --pl
                // is set (the perf / DR-drill deployments always set it) it is unused. So tolerate
                // the denial and fall back to the configured --throughput (-t) value instead of
                // crashing, keeping the keyless steady-state workload runnable.
                int? currentContainerThroughput = null;
                bool throughputReadDenied = false;
                try
                {
                    currentContainerThroughput = await container.ReadThroughputAsync();
                }
                catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.Forbidden)
                {
                    throughputReadDenied = true;
                    currentContainerThroughput = config.Throughput;
                    Utility.TeeTraceInformation(
                        $"ReadThroughputAsync denied ({(int)ce.StatusCode}/{ce.SubStatusCode}); throughput " +
                        $"(offer) reads are unavailable under data-plane RBAC / keyless auth (--aad). " +
                        $"Falling back to the configured --throughput value ({config.Throughput} RU/s). " +
                        $"Use --pl to size the task count explicitly.");
                }

                if (!throughputReadDenied && !currentContainerThroughput.HasValue)
                {
                    // Container throughput is not configured. It is shared database throughput
                    ThroughputResponse throughputResponse = await database.ReadThroughputAsync(requestOptions: null);
                    throw new InvalidOperationException($"Using database {config.Database} with {throughputResponse.Resource.Throughput} RU/s. " +
                        $"Container {config.Container} must have a configured throughput.");
                }

                Container resultContainer = await GetResultContainer(config, cosmosClient);

                BenchmarkProgress benchmarkProgressItem = await CreateBenchmarkProgressItem(resultContainer);

                Utility.TeeTraceInformation($"Using container {config.Container} with {currentContainerThroughput} RU/s");
                int taskCount = config.GetTaskCount(currentContainerThroughput.Value);

                Utility.TeePrint("Starting Inserts with {0} tasks", taskCount);

                string partitionKeyPath = containerResponse.Resource.PartitionKeyPath;
                int opsPerTask = config.ItemCount / taskCount;

                // Optional per-window metrics sink (W3). When configured, route per-operation
                // latency/RU/error samples plus .NET runtime metrics to the dedicated dashboard
                // schema. Defaults to null (no-op) so existing count-based runs are unaffected.
                IMetricsSink metricsSink = MetricsSinkFactory.Create(config);
                PerfMetricsReporter perfReporter = null;
                if (metricsSink != null)
                {
                    PerfRunContext perfRunContext = new PerfRunContext
                    {
                        Operation = string.IsNullOrWhiteSpace(config.WorkloadName) ? config.WorkloadType : config.WorkloadName,
                        Hostname = Environment.MachineName,
                        SdkVersion = config.ResolveSdkVersion(),
                        CommitSha = config.ResolveSdkSourceRef() ?? config.CommitId,
                        ConfigConcurrency = taskCount,
                        ConfigApplicationRegion = config.ApplicationPreferredRegions,
                        RunTag = config.RunTag,
                    };

                    perfReporter = new PerfMetricsReporter(metricsSink, perfRunContext, config.MetricsReportingIntervalInSec);
                    perfReporter.Start();
                }

                // TBD: 2 clients SxS some overhead
                RunSummary runSummary;

                // V2 SDK client initialization
                using (Microsoft.Azure.Documents.Client.DocumentClient documentClient = config.CreateDocumentClient(config.Key))
                {
                    Func<IBenchmarkOperation> benchmarkOperationFactory = this.GetBenchmarkFactory(
                        config,
                        partitionKeyPath,
                        cosmosClient,
                        documentClient);

                    if (config.DisableCoreSdkLogging)
                    {
                        // Do it after client initialization (HACK)
                        Program.ClearCoreSdkListeners();
                    }

                    try
                    {
                        IExecutionStrategy execution = IExecutionStrategy.StartNew(benchmarkOperationFactory);
                        runSummary = await execution.ExecuteAsync(config, taskCount, opsPerTask, 0.01);
                    }
                    finally
                    {
                        if (perfReporter != null)
                        {
                            await perfReporter.StopAndFlushAsync();
                        }
                    }
                }

                if (config.CleanupOnFinish)
                {
                    Utility.TeeTraceInformation($"Deleting Database {config.Database}");
                    await database.DeleteStreamAsync();
                }

                string consistencyLevel = config.ConsistencyLevel;
                if (string.IsNullOrWhiteSpace(consistencyLevel))
                {
                    AccountProperties accountProperties = await cosmosClient.ReadAccountAsync();
                    consistencyLevel = accountProperties.Consistency.DefaultConsistencyLevel.ToString();
                }
                runSummary.ConsistencyLevel = consistencyLevel;

                BenchmarkProgress benchmarkProgress = await CompleteBenchmarkProgressStatus(benchmarkProgressItem, resultContainer);
                if (config.PublishResults)
                {
                    Utility.TeeTraceInformation("Publishing results");
                    runSummary.Diagnostics = CosmosDiagnosticsLogger.GetDiagnostics();
                    await this.PublishResults(
                        config,
                        runSummary,
                        cosmosClient);
                }

                return runSummary;
            }
        }

        private async Task PublishResults(
            BenchmarkConfig config, 
            RunSummary runSummary, 
            CosmosClient benchmarkClient)
        {
            if (string.IsNullOrEmpty(config.ResultsEndpoint))
            {
                Container resultContainer = benchmarkClient.GetContainer(
                    databaseId: config.ResultsDatabase ?? config.Database,
                    containerId: config.ResultsContainer);

                await resultContainer.CreateItemAsync(runSummary, new PartitionKey(runSummary.pk));
            }
            else
            {
                using CosmosClient cosmosClient = new CosmosClient(config.ResultsEndpoint, config.ResultsKey);
                Container resultContainer = cosmosClient.GetContainer(config.ResultsDatabase, config.ResultsContainer);
                await resultContainer.CreateItemAsync(runSummary, new PartitionKey(runSummary.pk));
            }

        }

        private Func<IBenchmarkOperation> GetBenchmarkFactory(
            BenchmarkConfig config,
            string partitionKeyPath,
            CosmosClient cosmosClient,
            Microsoft.Azure.Documents.Client.DocumentClient documentClient)
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
                if (documentClient == null)
                {
                    throw new NotSupportedException(
                        $"Workload type {config.WorkloadType} uses the V2 DocumentClient, which requires master-key auth (-k). " +
                        "Use a V3 workload (the six tracked operations are all V3) when running with --aad.");
                }

                ci = benchmarkTypeName.GetConstructor(new Type[] { typeof(Microsoft.Azure.Documents.Client.DocumentClient), typeof(string), typeof(string), typeof(string), typeof(string) });
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

            return () => (IBenchmarkOperation)ci.Invoke(ctorArguments);
        }

        private static Type[] AvailableBenchmarks()
        {
            Type benchmarkType = typeof(IBenchmarkOperation);
            return typeof(Program).Assembly.GetTypes()
                .Where(p => benchmarkType.IsAssignableFrom(p))
                .ToArray();
        }

        /// <summary>
        /// Get or Create a partitioned container and display cost of running this test.
        /// </summary>
        /// <returns>The created container.</returns>
        private static async Task<ContainerResponse> CreatePartitionedContainerAsync(BenchmarkConfig options, CosmosClient cosmosClient)
        {
            Microsoft.Azure.Cosmos.Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(options.Database);
            
            string partitionKeyPath = options.PartitionKeyPath;
            return await database.CreateContainerIfNotExistsAsync(options.Container, partitionKeyPath, options.Throughput);
        }

        /// <summary>
        /// Creating a progress item in ComsosDb when the benchmark start
        /// </summary>
        /// <param name="resultContainer">An instance of <see cref="Container "/> that represents operations performed on a database container.</param>
        private static async Task<BenchmarkProgress> CreateBenchmarkProgressItem(Container resultContainer)
        {
            BenchmarkProgress benchmarkProgress = new BenchmarkProgress
            {
                id = Environment.MachineName,
                MachineName = Environment.MachineName,
                JobStatus = "STARTED",
                JobStartTime = DateTime.Now,
                pk = Environment.MachineName
            };

            ItemResponse<BenchmarkProgress> itemResponse = await resultContainer.UpsertItemAsync(
                benchmarkProgress, new PartitionKey(benchmarkProgress.pk));

            return itemResponse.Resource;
        }

        /// <summary>
        /// Change a progress item status to Complete in ComsosDb when the benchmark compleated
        /// </summary>
        /// <param name="resultContainer">An instance of <see cref="Container "/> that represents operations performed on a database container.</param>
        /// <param name="benchmarkProgress">An instance of <see cref="BenchmarkProgress"/> that represents the document to be modified.</param>
        public static async Task<BenchmarkProgress> CompleteBenchmarkProgressStatus(BenchmarkProgress benchmarkProgress, Container resultContainer)
        {
            benchmarkProgress.JobStatus = "COMPLETED";
            benchmarkProgress.JobEndTime = DateTime.Now;
            ItemResponse<BenchmarkProgress> itemResponse = await resultContainer.UpsertItemAsync(benchmarkProgress);
            return itemResponse.Resource;
        }

        /// <summary>
        /// Configure and prepare the Cosmos DB Container instance for the result container.
        /// </summary>
        /// <param name="config">An instance of <see cref="BenchmarkConfig "/> containing the benchmark tool input parameters.</param>
        /// <param name="cosmosClient">An instance of <see cref="CosmosClient "/> that represents operations performed on a CosmosDb database.</param>
        private static async Task<Container> GetResultContainer(BenchmarkConfig config, CosmosClient cosmosClient)
        {
            Database database = cosmosClient.GetDatabase(config.ResultsDatabase ?? config.Database);
            ContainerResponse containerResponse = await database
                .CreateContainerIfNotExistsAsync(
                            id: config.ResultsContainer, 
                            partitionKeyPath: "/pk");
            return containerResponse.Container;
        }

        private static void ClearCoreSdkListeners()
        {
            Type defaultTrace = Type.GetType("Microsoft.Azure.Cosmos.Core.Trace.DefaultTrace,Microsoft.Azure.Cosmos.Direct");
            TraceSource traceSource = (TraceSource)defaultTrace.GetProperty("TraceSource").GetValue(null);
            traceSource.Switch.Level = SourceLevels.All;
            traceSource.Listeners.Clear();
        }
    }
}
