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

            public int ItemCount { get; set; }
            public int ValuesCount { get; set; }
            public bool IsAddMode { get; set; }
            public int ItemPropertyCount { get; set; }
            public int IdLength { get; set; }
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
                await Program.MakeRequestsConcurrentlyAsync();
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

        private static async Task MakeRequestsConcurrentlyAsync()
        {
            DataSource dataSource = new DataSource();
            Console.WriteLine("Datasource initialized; starting operations");

            ConcurrentDictionary<HttpStatusCode, int> countsByStatus = new ConcurrentDictionary<HttpStatusCode, int>();
            ConcurrentBag<TimeSpan> latencies = new ConcurrentBag<TimeSpan>();
            long totalRequestCharge = 0;

            int taskTriggeredCounter = 0;
            int taskCompleteCounter = 0;
            int actualWarmupRequestCount = 0;

            int itemsToCreatePerWorker = (int)Math.Ceiling((double)(configuration.ItemCount / configuration.NumWorkers));
            List<Task> workerTasks = new List<Task>();

            const int ticksPerMillisecond = 10000;
            const int ticksPerSecond = 1000 * ticksPerMillisecond;
            int eachTicks = configuration.RequestsPerSecond <= 0 ? 0 : (int)(ticksPerSecond / configuration.RequestsPerSecond);
            long usageTicks = 0;

            if (configuration.MaxInFlightRequestCount == -1)
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

                    while (!cancellationToken.IsCancellationRequested && docCounter < itemsToCreatePerWorker)
                    {
                        docCounter++;

                        IStatement statement = dataSource.GetNextStatement(workerIndexLocal);

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

                        if (taskTriggeredCounter - taskCompleteCounter > configuration.MaxInFlightRequestCount)
                        {
                            await Task.Delay((int)((taskTriggeredCounter - taskCompleteCounter - configuration.MaxInFlightRequestCount) / configuration.MaxInFlightRequestCount));
                        }

                        _ = session.ExecuteAsync(statement)
                            .ContinueWith((Task<RowSet> task) =>
                            {
                                if (task.IsCompletedSuccessfully)
                                {
                                    using (RowSet rowSet = task.Result)
                                    {
                                        countsByStatus.AddOrUpdate(HttpStatusCode.OK, 1, (_, old) => old + 1);

                                        double requestCharge = BitConverter.ToDouble(rowSet.Info.IncomingPayload["RequestCharge"].Reverse().ToArray(), 0);
                                        // System.Console.WriteLine(requestCharge);
                                        Interlocked.Add(ref totalRequestCharge, (int)(requestCharge * 100));
                                    }
                                }
                                else
                                {
                                    Exception ex = task.Exception;
                                    bool isCounted = false;
                                    if (ex is AggregateException && ex.InnerException != null)
                                    {
                                        if (ex.InnerException.Message.Contains("OverloadedException")
                                            && ex.InnerException.Message.Contains("3200"))
                                        {
                                            countsByStatus.AddOrUpdate((HttpStatusCode)429, 1, (_, old) => old + 1);
                                            isCounted = true;
                                        }
                                    }

                                    if (!isCounted)
                                    {
                                        if (!isErrorPrinted)
                                        {
                                            Console.WriteLine(ex.ToString());
                                            isErrorPrinted = true;
                                        }

                                        countsByStatus.AddOrUpdate(HttpStatusCode.InternalServerError, 1, (_, old) => old + 1);
                                    }
                                }

                                task.Dispose();
                                if (Interlocked.Increment(ref taskCompleteCounter) >= configuration.ItemCount)
                                {
                                    stopwatch.Stop();
                                    latencyStopwatch.Stop();
                                }
                            });

                        Interlocked.Increment(ref taskTriggeredCounter);
                    }
                }));
            }

            while (taskCompleteCounter < configuration.ItemCount)
            {
                Console.Write($"In progress for {stopwatch.Elapsed}. Triggered: {taskTriggeredCounter} Processed: {taskCompleteCounter}, Pending: {configuration.ItemCount - taskCompleteCounter}");
                int nonFailedCount = 0;
                foreach (KeyValuePair<HttpStatusCode, int> countForStatus in countsByStatus)
                {
                    Console.Write(", " + countForStatus.Key + ": " + countForStatus.Value);
                    if (countForStatus.Key < HttpStatusCode.BadRequest)
                    {
                        nonFailedCount += countForStatus.Value;
                    }
                }

                if (actualWarmupRequestCount == 0 && nonFailedCount >= configuration.WarmUpRequestCount)
                {
                    actualWarmupRequestCount = nonFailedCount;
                    latencyStopwatch.Start();
                }

                long elapsedSeconds = latencyStopwatch.ElapsedMilliseconds / 1000;
                Console.Write($", RPS: {(elapsedSeconds == 0 ? -1 : (nonFailedCount - actualWarmupRequestCount) / elapsedSeconds)}");
                Console.WriteLine();

                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Could not handle {configuration.ItemCount} items in {configuration.MaxRuntimeInSeconds} seconds.");
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
            if (nonWarmupRequestCount > 0)
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
                if (tables.FirstOrDefault() == null)
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
            if (isAutoScale)
            {
                throughputToken = "cosmosdb_autoscale_max_throughput";
            }

            session.Execute("CREATE TABLE " + containerName + "(userid Text, myarr Set<Int>, primary key(userid)) WITH " + throughputToken + " = " + throughputToProvision);
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

        private class DataSource
        {
            private readonly int partitionKeyCount;
            private readonly string[] partitionKeys;
            private int itemIndex = 0;


            private Random random = new Random(314);

            private readonly List<int> values = new List<int>();

            public DataSource()
            {
                this.partitionKeyCount = Math.Min(configuration.PartitionKeyCount, configuration.ItemCount);
                this.partitionKeys = new string[this.partitionKeyCount];

                for (int i = 0; i < this.partitionKeyCount; i++)
                {
                    this.partitionKeys[i] = (partitionKeyValuePrefix + i).PadLeft(configuration.IdLength);
                }

                for (int i = 0; i < configuration.ValuesCount; i++)
                {
                    values.Add(1000000 + random.Next(9 * 1000 * 1000));
                }
            }

            public IStatement GetNextStatement(int workerIndex)
            {
                int incremented = Interlocked.Increment(ref this.itemIndex);
                int currentPKIndex = incremented % this.partitionKeyCount;
                string partitionKey = this.partitionKeys[currentPKIndex];

                if (configuration.IsAddMode)
                {
                    HashSet<int> held = new HashSet<int>();
                    int addCnt = configuration.ItemPropertyCount;
                    List<int> addList = new List<int>();
                    for (int i = 0; i < addCnt; i++)
                    {

                        int add;
                        do
                        {
                            add = this.values[random.Next(this.values.Count)];

                        } while (!held.Add(add));

                        addList.Add(add);
                    }

                    return new SimpleStatement("UPDATE " + configuration.ContainerName + " SET myarr = ? WHERE userid = ?",
                    new object[] { addList, partitionKey });
                }
                else
                {
                    int rem = this.values[random.Next(this.values.Count)];
                    return new SimpleStatement("UPDATE " + configuration.ContainerName + " SET myarr = myarr - ? WHERE userid = ?",
                                            new object[] { new List<int> { rem }, partitionKey });
                }
            }
        }
    }
}
