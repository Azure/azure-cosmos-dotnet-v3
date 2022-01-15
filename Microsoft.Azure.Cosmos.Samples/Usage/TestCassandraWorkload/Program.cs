namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Cassandra;
    using Microsoft.Extensions.Configuration;

    public class Program
    {
        private class Configuration
        {
            public string EndpointUrl { get; set; }
            public string AuthorizationKey { get; set; }
            public string DatabaseName { get; set; }
            public string ContainerName { get; set; }

            public bool ShouldRecreateContainerOnStart { get; set; }
            public int ThroughputToProvision { get; set; }
            public bool IsSharedThroughput { get; set; }
            public bool IsAutoScale { get; set; }
            public bool ShouldIndexAllProperties { get; set; }

            public int ItemsToCreate { get; set; }
            public int ItemSize { get; set; }
            public int ItemPropertyCount { get; set; }
            public int PartitionKeyCount { get; set; }

            public int RequestsPerSecond { get; set; }
            public int WarmUpRequestCount { get; set; }
            public int MaxInFlightRequestCount { get; set; }

            public int MaxRuntimeInSeconds { get; set; }
            public int NumWorkers { get; set; }
            public bool ShouldDeleteContainerOnFinish { get; set; }
        }

        private static Configuration configuration;
        private static ISession session;
        private static readonly string partitionKeyValuePrefix = DateTime.UtcNow.ToString("MMddHHmm-");

        public static async Task Main(string[] args)
        {
            try
            {
                await Program.InitializeAsync(args);
                await Program.CreateItemsConcurrentlyAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex);
            }
            finally
            {
                if (configuration.ShouldDeleteContainerOnFinish)
                {
                    Program.CleanupContainer();
                }

                session.Dispose();

                Console.WriteLine("End of demo.");
            }
        }

        private static async Task CreateItemsConcurrentlyAsync()
        {
            Console.WriteLine($"Starting creation of {configuration.ItemsToCreate} items of about {configuration.ItemSize} bytes each"
            + $" within {configuration.MaxRuntimeInSeconds} seconds using {configuration.NumWorkers} workers.");

            DataSource dataSource = new DataSource(configuration.ItemsToCreate,
                configuration.ItemPropertyCount, configuration.ItemSize, configuration.PartitionKeyCount);
            Console.WriteLine("Datasource initialized; starting ingestion");

            ConcurrentDictionary<HttpStatusCode, int> countsByStatus = new ConcurrentDictionary<HttpStatusCode, int>();
            ConcurrentBag<TimeSpan> latencies = new ConcurrentBag<TimeSpan>();
            long totalRequestCharge = 0;

            int taskTriggeredCounter = 0;
            int taskCompleteCounter = 0;
            int actualWarmupRequestCount = 0;

            int itemsToCreatePerWorker = (int)Math.Ceiling((double)(configuration.ItemsToCreate / configuration.NumWorkers));
            List<Task> workerTasks = new List<Task>();

            const int ticksPerMillisecond = 10000;
            const int ticksPerSecond = 1000 * ticksPerMillisecond;
            int eachTicks = configuration.RequestsPerSecond <= 0 ? 0 : (int)(ticksPerSecond / configuration.RequestsPerSecond);
            long usageTicks = 0;

            if(configuration.MaxInFlightRequestCount == -1)
            {
                configuration.MaxInFlightRequestCount = int.MaxValue;
            }

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(configuration.MaxRuntimeInSeconds * 1000);
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
                    PreparedStatement ps = session.Prepare("INSERT INTO " + configuration.ContainerName + "(pk, ck, mvpk, other) VALUES (?, ?, ?, ?)");

                    while (!cancellationToken.IsCancellationRequested && docCounter < itemsToCreatePerWorker)
                    {
                        docCounter++;

                        IStatement boundStatement = dataSource.GetNextStatement(workerIndexLocal, ps);

                        long elapsedTicks = stopwatch.ElapsedTicks;
                        if (usageTicks < elapsedTicks)
                        {
                            usageTicks = elapsedTicks;
                        }

                        usageTicks += eachTicks;
                        if (usageTicks - elapsedTicks > ticksPerSecond)
                        {
                            await Task.Delay((int)((usageTicks - elapsedTicks - ticksPerSecond) / ticksPerMillisecond));
                        }

                        if(taskTriggeredCounter - taskCompleteCounter > configuration.MaxInFlightRequestCount)
                        {
                            await Task.Delay((int)((taskTriggeredCounter - taskCompleteCounter - configuration.MaxInFlightRequestCount) / configuration.MaxInFlightRequestCount));
                        }

                        _ = session.ExecuteAsync(boundStatement)
                            .ContinueWith((Task<RowSet> task) =>
                            {
                                if (task.IsCompletedSuccessfully)
                                {
                                    using (RowSet rowSet = task.Result)
                                    {
                                        countsByStatus.AddOrUpdate(HttpStatusCode.OK, 1, (_, old) => old + 1);

                                        double requestCharge = BitConverter.ToDouble(rowSet.Info.IncomingPayload["RequestCharge"].Reverse().ToArray(), 0);
                                        Interlocked.Add(ref totalRequestCharge, (int)(requestCharge * 100));
                                    }
                                }
                                else
                                {
                                    Exception ex = task.Exception;
                                    bool isCounted = false;
                                    if(ex is AggregateException && ex.InnerException != null)
                                    {
                                        if(ex.InnerException.Message.Contains("OverloadedException")
                                            && ex.InnerException.Message.Contains("3200"))
                                            {
                                                countsByStatus.AddOrUpdate((HttpStatusCode)429, 1, (_, old) => old + 1);
                                                isCounted = true;
                                            }
                                    }

                                    if(!isCounted)
                                    {
                                        if(!isErrorPrinted)
                                        {
                                            Console.WriteLine(ex.ToString());
                                            isErrorPrinted = true;
                                        }

                                        countsByStatus.AddOrUpdate(HttpStatusCode.InternalServerError, 1, (_, old) => old + 1);
                                    }
                                }

                                task.Dispose();
                                if (Interlocked.Increment(ref taskCompleteCounter) >= configuration.ItemsToCreate)
                                {
                                    stopwatch.Stop();
                                    latencyStopwatch.Stop();
                                }
                            });

                        Interlocked.Increment(ref taskTriggeredCounter);
                    }
                }));
            }

            while (taskCompleteCounter < configuration.ItemsToCreate)
            {
                Console.Write($"In progress. Triggered: {taskTriggeredCounter} Processed: {taskCompleteCounter}, Pending: {configuration.ItemsToCreate - taskCompleteCounter}");
                int nonFailedCount = 0;
                foreach (KeyValuePair<HttpStatusCode, int> countForStatus in countsByStatus)
                {
                    Console.Write(", " + countForStatus.Key + ": " + countForStatus.Value);
                    if(countForStatus.Key < HttpStatusCode.BadRequest)
                    {
                        nonFailedCount += countForStatus.Value;
                    }
                }

                if(actualWarmupRequestCount == 0 && nonFailedCount >= configuration.WarmUpRequestCount)
                {
                    actualWarmupRequestCount = nonFailedCount;
                    latencyStopwatch.Start();
                }

                long elapsedSeconds = latencyStopwatch.ElapsedMilliseconds / 1000;
                Console.Write($", RPS: {(elapsedSeconds == 0 ? -1 : (nonFailedCount - actualWarmupRequestCount) / elapsedSeconds)}");
                Console.WriteLine();

                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Could not handle {configuration.ItemsToCreate} items in {configuration.MaxRuntimeInSeconds} seconds.");
                    break;
                }

                await Task.Delay(1000);
            }

            long elapsedFinal = latencyStopwatch.ElapsedMilliseconds / 1000;
            int nonFailedCountFinal = countsByStatus.Where(x => x.Key < HttpStatusCode.BadRequest).Sum(p => p.Value);
            int nonFailedCountFinalForLatency = nonFailedCountFinal - actualWarmupRequestCount;
            Console.WriteLine($"Successfully handled {nonFailedCountFinal} items; handled {nonFailedCountFinalForLatency} in {elapsedFinal} seconds at {(elapsedFinal == 0 ? -1 : nonFailedCountFinalForLatency / elapsedFinal)} items/sec.");

            Console.WriteLine("Counts by StatusCode:");
            Console.WriteLine(string.Join(',', countsByStatus.Select(countForStatus => countForStatus.Key + ": " + countForStatus.Value)));

            List<TimeSpan> latenciesList = latencies.ToList();
            latenciesList.Sort();
            int nonWarmupRequestCount = latenciesList.Count;
            if(nonWarmupRequestCount > 0)
            {
                Console.WriteLine("Latencies (non-failed):"
                + $"   P90: {latenciesList[(int)(nonWarmupRequestCount * 0.90)].TotalMilliseconds}"
                + $"   P99: {latenciesList[(int)(nonWarmupRequestCount * 0.99)].TotalMilliseconds}"
                + $"   P99.9: {latenciesList[(int)(nonWarmupRequestCount * 0.999)].TotalMilliseconds}"
                + $"   Max: {latenciesList[nonWarmupRequestCount - 1].TotalMilliseconds}");
            }

            Console.WriteLine("Average RUs (non-failed): " + (totalRequestCharge / (100.0 * nonFailedCountFinal)));
        }

        private static async Task InitializeAsync(string[] args)
        {
            IConfigurationRoot configurationRoot = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .AddCommandLine(args)
                    .Build();

            Program.configuration = new Configuration();
            configurationRoot.Bind(Program.configuration);

            Program.session = CreateSession(configuration.EndpointUrl, configuration.AuthorizationKey, configuration.DatabaseName);
            if (configuration.ShouldRecreateContainerOnStart)
            {
                Program.RecreateContainer(
                    configuration.DatabaseName,
                    configuration.ContainerName, 
                    configuration.ShouldIndexAllProperties, 
                    configuration.ThroughputToProvision, 
                    configuration.IsSharedThroughput, 
                    configuration.IsAutoScale);
                await Task.Delay(5000);
            }

            try
            {
                RowSet tables = session.Execute("SELECT * FROM system_schema.tables where table_name = '" + configuration.ContainerName + "' ALLOW FILTERING");
                if(tables.FirstOrDefault() == null)
                {
                    throw new Exception("Did not find table " + configuration.ContainerName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in reading table: {0}", ex.Message);
                throw;
            }
        }

        private static ISession CreateSession(
            string endpoint,
            string authKey,
            string keyspaceName)
        {
            SSLOptions options = new Cassandra.SSLOptions(SslProtocols.Tls12, true, ValidateServerCertificate);
            options.SetHostNameResolver((ipAddress) => endpoint);
            Cluster cluster = Cluster.Builder()
                .WithCredentials(endpoint.Split('.', 2)[0], authKey)
                .WithPort(10350)
                .AddContactPoint(endpoint)
                .WithSSL(options)
                .Build();

            ISession session = cluster.Connect();
            session.CreateKeyspaceIfNotExists(keyspaceName);
            session.ChangeKeyspace(keyspaceName);
            return session;
        }

        private static bool ValidateServerCertificate(
                 object sender,
                 X509Certificate certificate,
                 X509Chain chain,
                 SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            Console.WriteLine("Certificate error: {0}", sslPolicyErrors);
            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }

        private static void RecreateContainer(
            string databaseName,
            string containerName,
            bool shouldIndexAllProperties,
            int throughputToProvision,
            bool isSharedThroughput,
            bool isAutoScale)
        {
             Console.WriteLine("Deleting old table if it exists.");
            Program.CleanupContainer();

            Console.WriteLine($"Creating a {throughputToProvision} RU/s {(isAutoScale ? "auto-scale" : "manual throughput")} table...");

            // todo: shouldIndexAllProperties
            string throughputToken = "cosmosdb_provisioned_throughput"; 
            if(isAutoScale)
            {
                throughputToken = "cosmosdb_autoscale_max_throughput";
            }

            session.Execute("CREATE TABLE " + containerName + "(pk text, ck text, mvpk text, other text, primary key(pk, ck)) WITH " + throughputToken + " = " + throughputToProvision);
        }

        private static void CleanupContainer()
        {
            if (session != null)
            {
                try
                {
                    session.Execute("DROP TABLE " + configuration.ContainerName);
                }
                catch (Exception)
                {
                }
            }
        }

        // private static (string, int) GetThroughputTypeAndValue(ThroughputProperties throughputProperties)
        // {
        //     string type = throughputProperties.AutoscaleMaxThroughput.HasValue ? "auto-scale" : "manual";
        //     int value = throughputProperties.AutoscaleMaxThroughput ?? throughputProperties.Throughput.Value;
        //     return (type, value);
        // }

        private class DataSource
        { 
            // private readonly List<string> additionalProperties = new List<string>();
            private readonly int itemSize;
            private readonly int partitionKeyCount;
            private readonly string padding = string.Empty;
            private readonly string[] partitionKeys;
            private int itemIndex = 0;

            public DataSource(int itemCount, int itemPropertyCount, int itemSize, int partitionKeyCount)
            {
                this.partitionKeyCount = Math.Min(partitionKeyCount, itemCount);
                this.itemSize = itemSize;
                this.partitionKeys = new string[this.partitionKeyCount];

                // Determine padding length - setup initial values so we can create a sample doc
                this.padding = string.Empty;

                // Setup properties - reduce some for standard properties like PK and Id we are adding
                // for (int i = 0; i < itemPropertyCount - 10; i++)
                // {
                //     this.additionalProperties.Add(i.ToString());
                // }

                this.padding = this.itemSize > 400 ? new string('x', this.itemSize - 400) : string.Empty;

                for (int i = 0; i < this.partitionKeyCount; i++)
                {
                    this.partitionKeys[i] =  partitionKeyValuePrefix + i;
                }
            }

            public IStatement GetNextStatement(int workerIndex, PreparedStatement ps)
            {
                int incremented = Interlocked.Increment(ref this.itemIndex);
                int currentPKIndex = incremented % this.partitionKeyCount;
                string partitionKey = this.partitionKeys[currentPKIndex];
                return ps.Bind(partitionKey, incremented.ToString(), Guid.NewGuid().ToString(), this.padding);
            }
        }
    }
}