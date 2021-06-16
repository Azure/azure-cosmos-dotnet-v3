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
    using Newtonsoft.Json;

    public class Program
    {
        private static string keyspaceName;
        private static string tableName;
        private static ISession session;
        private static int itemsToCreate;
        private static int itemSize;
        private static int itemPropertyCount;
        private static int maxRuntimeInSeconds;
        private static bool shouldDeleteTableOnFinish;
        private static int numWorkers;
        private static int partitionKeyCount;
        private static int rps;
        private static readonly string partitionKeyValuePrefix = DateTime.UtcNow.ToString("MMddHHmm-");

        public static async Task Main(string[] args)
        {
            try
            {
                Program.Initialize();
                await Program.CreateItemsConcurrentlyAsync();
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Error: {0}", e);
            }
            finally
            {
                if (Program.shouldDeleteTableOnFinish)
                {
                    Program.CleanupTable();
                }

                session.Dispose();

                Console.WriteLine("End of demo.");
            }
        }

        private static async Task CreateItemsConcurrentlyAsync()
        {
            Console.WriteLine($"Starting creation of {Program.itemsToCreate} items of about {Program.itemSize} bytes each"
            + $" within {maxRuntimeInSeconds} seconds using {numWorkers} workers.");
            DataSource dataSource = new DataSource(itemsToCreate, itemPropertyCount, itemSize, partitionKeyCount);
            Console.WriteLine("Datasource initialized; starting ingestion");

            ConcurrentDictionary<HttpStatusCode, int> countsByStatus = new ConcurrentDictionary<HttpStatusCode, int>();
            ConcurrentBag<TimeSpan> latencies = new ConcurrentBag<TimeSpan>();
            long totalRequestCharge = 0;

            int taskTriggeredCounter = 0;
            int taskCompleteCounter = 0;

            int itemsToCreatePerWorker = (int)Math.Ceiling((double)(itemsToCreate / numWorkers));
            List<Task> workerTasks = new List<Task>();

            const int ticksPerMillisecond = 10000;
            const int ticksPerSecond = 1000 * ticksPerMillisecond;
            int eachTicks = (int)(ticksPerSecond / Program.rps);
            long usageTicks = 0;

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(maxRuntimeInSeconds * 1000);
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            Stopwatch stopwatch = Stopwatch.StartNew();

            for (int workerIndex = 0; workerIndex < numWorkers; workerIndex++)
            {
                int workerIndexLocal = workerIndex;
                workerTasks.Add(Task.Run(async () =>
                {
                    int docCounter = 0;
                    bool isErrorPrinted = false;
                    var ps = session.Prepare("INSERT INTO " + tableName + "(pk, ck, other) VALUES (?, ?, ?)");

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
                                        
                                        // if (rowSet.StatusCode < HttpStatusCode.BadRequest)
                                        // {
                                        //      latencies.Add(rowSet.Diagnostics.GetClientElapsedTime());
                                        // }
                                        // else
                                        // {
                                        //     if ((int)rowSet.StatusCode != 429 && !isErrorPrinted)
                                        //     {
                                        //         Console.WriteLine(rowSet.ErrorMessage);
                                        //         Console.WriteLine(rowSet.Diagnostics.ToString());
                                        //         isErrorPrinted = true;
                                        //     }
                                        // }
                                    }
                                }
                                else
                                {
                                    Exception ex = task.Exception;
                                    if(!isErrorPrinted)
                                    {
                                        Console.WriteLine(ex);
                                        isErrorPrinted = true;
                                    }

                                    countsByStatus.AddOrUpdate(HttpStatusCode.InternalServerError, 1, (_, old) => old + 1);
                                }

                                task.Dispose();
                                if (Interlocked.Increment(ref taskCompleteCounter) >= itemsToCreate)
                                {
                                    stopwatch.Stop();
                                }
                            });

                        Interlocked.Increment(ref taskTriggeredCounter);
                    }
                }));
            }

            while (taskCompleteCounter < itemsToCreate)
            {
                Console.Write($"In progress. Triggered: {taskTriggeredCounter} Processed: {taskCompleteCounter}, Pending: {itemsToCreate - taskCompleteCounter}");
                int nonFailedCount = 0;
                foreach (KeyValuePair<HttpStatusCode, int> countForStatus in countsByStatus)
                {
                    Console.Write(", " + countForStatus.Key + ": " + countForStatus.Value);
                    if (countForStatus.Key < HttpStatusCode.BadRequest)
                    {
                        nonFailedCount += countForStatus.Value;
                    }
                }

                long elapsedSeconds = stopwatch.ElapsedMilliseconds / 1000;
                Console.Write($", RPS: {(elapsedSeconds == 0 ? -1 : nonFailedCount / elapsedSeconds)}");
                Console.WriteLine();

                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Could not insert {itemsToCreate} items in {maxRuntimeInSeconds} seconds.");
                    break;
                }

                await Task.Delay(1000);
            }

            long elapsedFinal = stopwatch.ElapsedMilliseconds / 1000;
            int nonFailedCountFinal = countsByStatus.Where(x => x.Key < HttpStatusCode.BadRequest).Sum(p => p.Value);
            Console.WriteLine($"Successfully handled {nonFailedCountFinal} items in {elapsedFinal} seconds at {(elapsedFinal == 0 ? -1 : nonFailedCountFinal / elapsedFinal)} items/sec.");

            Console.WriteLine("Counts by StatusCode:");
            Console.WriteLine(string.Join(',', countsByStatus.Select(countForStatus => countForStatus.Key + ": " + countForStatus.Value)));

            // List<TimeSpan> latenciesList = latencies.ToList();
            // latenciesList.Sort();
            // int requestCount = latenciesList.Count;
            // Console.WriteLine("Latencies (non-failed):"
            // + $"   P90: {latenciesList[(int)(requestCount * 0.90)].TotalMilliseconds}"
            // + $"   P99: {latenciesList[(int)(requestCount * 0.99)].TotalMilliseconds}"
            // + $"   P99.9: {latenciesList[(int)(requestCount * 0.999)].TotalMilliseconds}"
            // + $"   Max: {latenciesList[requestCount - 1].TotalMilliseconds}");

            Console.WriteLine("Average RUs (non-failed): " + totalRequestCharge / (100.0 * nonFailedCountFinal));
        }

        // private class MyDocument
        // {
        //     public string ck { get; set; }
        //     public string pk { get; set; }
        //     public string other { get; set; }
        // }

        private static void Initialize()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();

            string endpointUrl = configuration["EndPointUrl"];
            if (string.IsNullOrEmpty(endpointUrl))
            {
                throw new ArgumentNullException("Please specify a valid EndPointUrl in the appSettings.json");
            }

            string authKey = configuration["AuthorizationKey"];
            if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
            {
                throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json");
            }

            Program.itemsToCreate = GetConfig(configuration, "ItemsToCreate", 100000);
            Program.itemSize = GetConfig(configuration, "ItemSize", 1024);
            Program.itemPropertyCount = GetConfig(configuration, "ItemPropertyCount", 10);
            Program.partitionKeyCount = GetConfig(configuration, "PartitionKeyCount", int.MaxValue);
            Program.rps = GetConfig(configuration, "RequestsPerSec", 1000);
            Program.numWorkers = GetConfig(configuration, "NumWorkers", 1);
            Program.maxRuntimeInSeconds = GetConfig(configuration, "MaxRuntimeInSeconds", 300);
            Program.shouldDeleteTableOnFinish = GetConfig(configuration, "ShouldDeleteTableOnFinish", false);

            Program.keyspaceName = GetConfig(configuration, "KeyspaceName", "demokeyspace");
            Program.tableName = GetConfig(configuration, "TableName", "demotable");
            bool shouldRecreateTableOnStart = GetConfig(configuration, "ShouldRecreateTableOnStart", false);
            int tableThroughput = GetConfig(configuration, "TableThroughput", 10000);
            bool isTableAutoScale = GetConfig(configuration, "IsTableAutoscale", true);
            bool shouldIndexAllProperties = GetConfig(configuration, "ShouldTableIndexAllProperties", false);

            CreateSession(endpointUrl, authKey);
            if (shouldRecreateTableOnStart)
            {
                Program.RecreateTable(keyspaceName, tableName, shouldIndexAllProperties, tableThroughput, isTableAutoScale);
            }

            try
            {
                RowSet tables = session.Execute("SELECT * FROM system_schema.tables where table_name = '" + tableName + "' ALLOW FILTERING");
                if(tables.FirstOrDefault() == null)
                {
                    throw new Exception("Did not find table " + tableName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in reading table: {0}", ex.Message);
                throw;
            }
        }

        private static void CreateSession(
            string endpoint,
            string authKey)
        {
            var options = new Cassandra.SSLOptions(SslProtocols.Tls12, true, ValidateServerCertificate);
            options.SetHostNameResolver((ipAddress) => endpoint);
            Cluster cluster = Cluster.Builder()
                .WithCredentials(endpoint.Split('.', 2)[0], authKey)
                .WithPort(10350)
                .AddContactPoint(endpoint)
                .WithSSL(options)
                .Build();

            session = cluster.Connect();
            session.CreateKeyspaceIfNotExists(keyspaceName);
            session.ChangeKeyspace(keyspaceName);
        }

        public static bool ValidateServerCertificate(
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
        private static void CleanupTable()
        {
            if (session != null)
            {
                try
                {
                    session.Execute("DROP TABLE " + tableName);
                }
                catch (Exception)
                {
                }
            }
        }

        private static void RecreateTable(
            string keyspaceName,
            string containerName,
            bool shouldIndexAllProperties,
            int throughputToProvision,
            bool isAutoScale)
        {
            Console.WriteLine("Deleting old table if it exists.");
            Program.CleanupTable();

            Console.WriteLine($"Creating a {throughputToProvision} RU/s {(isAutoScale ? "auto-scale" : "manual throughput")} table...");

            // todo: shouldIndexAllProperties
            string throughputToken = "cosmosdb_provisioned_throughput"; 
            if(isAutoScale)
            {
                throughputToken = "cosmosdb_autoscale_max_throughput";
            }

            session.Execute("CREATE TABLE " + tableName + "(pk text, ck text, other text, primary key(pk, ck)) WITH " + throughputToken + " = " + throughputToProvision);
        }

        private static int GetConfig(IConfigurationRoot iConfigurationRoot, string configName, int defaultValue)
        {
            if (!string.IsNullOrEmpty(iConfigurationRoot[configName]))
            {
                return int.Parse(iConfigurationRoot[configName]);
            }

            return defaultValue;
        }

        private static bool GetConfig(IConfigurationRoot iConfigurationRoot, string configName, bool defaultValue)
        {
            if (!string.IsNullOrEmpty(iConfigurationRoot[configName]))
            {
                return bool.Parse(iConfigurationRoot[configName]);
            }

            return defaultValue;
        }

        private static string GetConfig(IConfigurationRoot iConfigurationRoot, string configName, string defaultValue)
        {
            if (!string.IsNullOrEmpty(iConfigurationRoot[configName]))
            {
                return iConfigurationRoot[configName];
            }

            return defaultValue;
        }

        private class DataSource
        {
            // private readonly List<string> additionalProperties = new List<string>();
            private readonly int itemSize;
            private readonly int partitionKeyCount;
            private string padding = string.Empty;
            private string[] partitionKeys;
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
                int incremented = Interlocked.Increment(ref itemIndex);
                int currentPKIndex = incremented % partitionKeyCount;
                string partitionKey = this.partitionKeys[currentPKIndex];
                return ps.Bind(partitionKey, incremented.ToString(), padding);
            }
        }
    }
}