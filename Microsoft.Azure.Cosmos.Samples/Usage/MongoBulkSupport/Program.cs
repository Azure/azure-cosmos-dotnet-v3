//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.Linq;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;
    using MongoDB.Bson;
    using MongoDB.Driver;
    using MongoDB.Driver.Core.Servers;

    /// <summary>
    /// This sample demonstrates some multi-master scenarios using Azure Cosmos DB for MongoDB API account.
    /// </summary>
    internal static class Program
    {
        private const string HelpText = @"
Performs a demonstration of multi-master scenarios against the account identified 
by the supplied connection string, database, and collection.

To use, supply a value for 'connectionstring' in the application config file.

Use a mongodb:// style connection string, such as the one from the Azure Portal. 
To specify a preferred write region, append the appName=[app]@<azure region> option.

  Example with no preferred write region:
    mongodb://user:password@example.documents.azure.com:10255/?ssl=true&replicaSet=globaldb

  Examples with preferred write region of 'East US':
    mongodb://user:password@example.documents.azure.com:10255/?ssl=true&replicaSet=globaldb&appName=@East US
    mongodb://user:password@example.documents.azure.com:10255/?ssl=true&replicaSet=globaldb&appName=myapp@East US

  The available region closest geographically to the preferred write region will be presented to the MongoClient 
  as the PRIMARY replica set member and will accept writes.

  Note that database and collection creation must be performed using the default write region.
";

        /// <summary>
        /// Main entry point
        /// </summary>
        public static void Main(string[] args)
        {
            Program.MainAsync(args).Wait();
        }

        /// <summary>
        /// Main entry point (async)
        /// </summary>
        private static async Task MainAsync(string[] args)
        {
            int numWorkers = args.Length > 0 ? int.Parse(args[0]) : 1;
            int numDocs = args.Length > 1 ? int.Parse(args[1]) : 100;
            try
            {
                string connectionString =
  @"mongodb://abpaistgcosmongo36:PYZkV7yFm0LQBvWfXjkiC1nhZMpcGgVvT1MBjkRey8lcy7mzu2WveDq8IljCCoZBBm2zAfsazkTgTHNMV7qHxA==@abpaistgcosmongo36.mongo.cosmos.windows-ppe.net:10255/?ssl=true&replicaSet=globaldb&maxIdleTimeMS=120000&retrywrites=false&appName=@abpaistgcosmongo36@";

                //string globalDatabaseName = "batchdocumentservicemongo";
                //string endPoint = "batchdocumentservicemongo-southeastasia.mongo.cosmos.windows-int.net";
                //string authKey = "IhFXhp3FvwrJmNRyqAqmzgQTdUKEAW8UMHsXISqJiYX7pc3QnODfzy3RyfKzu0OiEF88SHm7VUL51aKCD3hEAA==";
                //string connectionString = $@"mongodb://{globalDatabaseName}:{authKey}@{endPoint}:10255/?ssl=true&replicaSet=globaldb&maxIdleTimeMS=120000&retrywrites=false&appName=@{globalDatabaseName}@";

                //string connectionString = "mongodb://localhost:C2y6yDjf5%2FR%2Bob0N8A7Cgv30VRDJIWEHLM%2B4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw%2FJw%3D%3D@localhost:10250/admin?ssl=true&retrywrites=false";
                MongoClientSettings settings = MongoClientSettings.FromUrl(
                  new MongoUrl(connectionString)
                );
                settings.SslSettings =
                  new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
                var mongoClient = new MongoClient(settings);

                //Console.WriteLine($"Using account: {globalDatabaseName}, endpoint: {endPoint}");
                await Program.WaitForConnectionsToAllServersAsync(mongoClient, TimeSpan.FromSeconds(30));
                Console.WriteLine("Starting execution");

                await Program.ExecuteIBulkWriteWorkloadAsync(mongoClient, Guid.NewGuid(), numWorkers, numDocs);
                //await Program.ExecuteInsertManyWorkloadAsync(mongoClient, Guid.NewGuid(), numWorkers, numDocs);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(Program.HelpText);
            }

            Console.WriteLine();
            Console.WriteLine("Press any key...");
            Console.ReadLine();
        }

        /// <summary>
        /// The write workload writes a set of documents
        /// </summary>
        private static async Task ExecuteInsertManyWorkloadAsync(MongoClient mongoClient, Guid runGuid, int numWorkers, int numDocs)
        {
            string databaseName = "test";
            string collectionName = "batchTestingnew";

            // create collection
            BsonDocumentCommand<BsonDocument> command = new BsonDocumentCommand<BsonDocument>(
                new BsonDocument
                {
                            {"customAction", "CreateCollection"},
                            {"collection", collectionName},
                            {"shardKey", "pk"},
                            {"offerThroughput", 3000}
                });

            IMongoDatabase mongoDatabase = null;
            IMongoCollection<BsonDocument> mongoCollection =
                CreateCollection(databaseName, collectionName, mongoClient, command, out mongoDatabase);

            ServerDescription primaryServerDescription = mongoClient.Cluster.Description.Servers.First(x => x.Type == ServerType.ReplicaSetPrimary);
            string region = Helpers.TryGetRegionFromTags(primaryServerDescription.Tags) ?? string.Empty;

            string partitionKey = Guid.NewGuid().ToString();
            string id = Guid.NewGuid().ToString();
            int docSize = 400;
            string padding = docSize > 300 ? new string('x', docSize - 300) : string.Empty;

            BsonDocument template = new BsonDocument(
                                (IEnumerable<BsonElement>)new[]
                                    {
                                        new BsonElement("pk", new BsonString(partitionKey)),
                                        new BsonElement("_id", new BsonString(id)),
                                        new BsonElement("other", new BsonString(padding))
                                    });

            Console.WriteLine($"Writing {numDocs} documents to {region} in colection {collectionName}.");

            List<Task> workerTasks = new List<Task>();
            Stopwatch stopwatch = Stopwatch.StartNew();
            long startMilliseconds = stopwatch.ElapsedMilliseconds;

            for (int worker = 0; worker < numWorkers; worker++)
            {
                List<BsonDocument> documents = new List<BsonDocument>();
                int localNumToWrite = numDocs / numWorkers;

                BsonDocument toInsert1 = (BsonDocument)template.DeepClone();
                toInsert1["_id"] = new BsonString("80");
                toInsert1["pk"] = new BsonString("1000");
                toInsert1["other"] = new BsonString("400");

                documents.Add(toInsert1);

                for (int i = 0; i < localNumToWrite; ++i)
                {
                    BsonDocument toInsert = (BsonDocument)template.DeepClone();
                    toInsert["_id"] = new BsonString(Guid.NewGuid().ToString());
                    toInsert["pk"] = new BsonString(Guid.NewGuid().ToString());

                    documents.Add(toInsert);
                }

                BsonDocument toInsert2 = (BsonDocument)template.DeepClone();
                toInsert2["_id"] = new BsonString("80");
                toInsert2["pk"] = new BsonString("1000");
                toInsert2["other"] = new BsonString("600");

                documents.Add(toInsert2);

                InsertManyOptions options = new InsertManyOptions();
                options.IsOrdered = false;
                Console.WriteLine("Starting Bulk executions ExecuteInsertManyWorkloadAsync IsOrdered = false for worker: " + worker + " localNumToWrite: " + localNumToWrite);
                workerTasks.Add(mongoCollection.InsertManyAsync(documents, options));
            }

            await Task.WhenAll(workerTasks);

            Console.WriteLine($"Inserted {numDocs} items in {(stopwatch.ElapsedMilliseconds - startMilliseconds)} milliSeconds");
            Console.WriteLine();
        }

        private static IMongoCollection<BsonDocument> CreateCollection(
            string databaseName,
            string collectionName,
            MongoClient client,
            BsonDocumentCommand<BsonDocument> createCollectionCommand,
            out IMongoDatabase database)
        {
            database = client.GetDatabase(databaseName);
            //BsonDocument response = database.RunCommandAsync(createCollectionCommand).Result;

            return ReadCollection(collectionName, database);
        }

        private static IMongoCollection<BsonDocument> ReadCollection(
          string collectionName,
          IMongoDatabase database)
        {
            IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>(collectionName);
            return collection;
        }

        private static async Task ExecuteIBulkWriteWorkloadAsync(MongoClient mongoClient, Guid runGuid, int numWorkers, int numDocs)
        {
            string databaseName = "test";
            string collectionName = "batchTestingnew";

            // create collection
            BsonDocumentCommand<BsonDocument> command = new BsonDocumentCommand<BsonDocument>(
                new BsonDocument
                {
                            {"customAction", "CreateCollection"},
                            {"collection", collectionName},
                            {"shardKey", "pk"},
                            {"offerThroughput", 30000}
                });

            IMongoDatabase mongoDatabase = null;
            IMongoCollection<BsonDocument> mongoCollection =
                CreateCollection(databaseName, collectionName, mongoClient, command, out mongoDatabase);


            ServerDescription primaryServerDescription = mongoClient.Cluster.Description.Servers.First(x => x.Type == ServerType.ReplicaSetPrimary);
            string region = Helpers.TryGetRegionFromTags(primaryServerDescription.Tags) ?? string.Empty;

            string partitionKey = Guid.NewGuid().ToString();
            string id = Guid.NewGuid().ToString();
            int docSize = 400;
            string padding = docSize > 110 ? new string('x', docSize - 110) : string.Empty;

            BsonDocument template = new BsonDocument(
                                (IEnumerable<BsonElement>)new[]
                                    {
                                        new BsonElement("pk", new BsonString(partitionKey)),
                                        new BsonElement("_id", new BsonString(id)),
                                        new BsonElement("other", new BsonString(padding))
                                    });

            Console.WriteLine($"Writing {numDocs} documents to {region} in colection {collectionName}.");

            List<Task> workerTasks = new List<Task>();
            Stopwatch stopwatch = Stopwatch.StartNew();
            long startMilliseconds = stopwatch.ElapsedMilliseconds;


            for (int worker = 0; worker < numWorkers; worker++)
            {
                List<InsertOneModel<BsonDocument>> documents = new List<InsertOneModel<BsonDocument>>();
                int localNumToWrite = numDocs / numWorkers;

                BsonDocument toInsert1 = (BsonDocument)template.DeepClone();
                toInsert1["_id"] = new BsonString("81s01");
                toInsert1["pk"] = new BsonString("10000");

                documents.Add(ParseInsertOne(toInsert1));

                for (int i = 0; i < localNumToWrite - 2; ++i)
                {
                    BsonDocument toInsert = (BsonDocument)template.DeepClone();
                    toInsert["_id"] = new BsonString(Guid.NewGuid().ToString());
                    toInsert["pk"] = new BsonString(Guid.NewGuid().ToString());

                    documents.Add(ParseInsertOne(toInsert));
                }

                BsonDocument toInsert2 = (BsonDocument)template.DeepClone();
                toInsert2["_id"] = new BsonString("81s01");
                toInsert2["pk"] = new BsonString("10000");

                documents.Add(ParseInsertOne(toInsert2));

                BulkWriteOptions bulkWriteOptions = new BulkWriteOptions();
                bulkWriteOptions.IsOrdered = false;

                Console.WriteLine("Starting Bulk executions ExecuteInsertManyWorkloadAsync IsOrdered = false for worker: " + worker + " localNumToWrite: " + localNumToWrite);

                BulkWriteResult result = null;

                try
                {
                    result = await mongoCollection.BulkWriteAsync(documents, bulkWriteOptions);
                }
                catch(Exception e)
                {
                    Console.WriteLine(e);
                }

                Console.WriteLine($"Result values: {result.InsertedCount}, {result.Upserts}, {result.ToString()}");
            }

            await Task.WhenAll(workerTasks);

            Console.WriteLine($"Inserted {numDocs} items in {(stopwatch.ElapsedMilliseconds - startMilliseconds)} milliSeconds");
            Console.WriteLine();
        }

        private static InsertOneModel<BsonDocument> ParseInsertOne(BsonDocument document)
        {
            return new InsertOneModel<BsonDocument>(document);
        }

        private static async Task<Dictionary<string, MongoClient>> DiscoverAvailableRegionsAsync(MongoClient mongoClient)
        {
            Console.WriteLine("Discovering all available regions...");
            Console.WriteLine();

            KeyValuePair<string, TimeSpan>[] availableRegions = mongoClient.Cluster.Description.Servers
                .Select(x => new KeyValuePair<string, TimeSpan>(Helpers.TryGetRegionFromTags(x.Tags), x.AverageRoundTripTime))
                .ToArray();

            Console.WriteLine($"{mongoClient.Cluster.Description.Servers.Count} regions available:");

            int maxRegionCharacters = availableRegions.Max(x => x.Key.Length);

            foreach (KeyValuePair<string, TimeSpan> availableRegion in availableRegions.OrderBy(x => x.Value))
            {
                Console.WriteLine($"  {availableRegion.Key.PadRight(maxRegionCharacters)} [{availableRegion.Value.TotalMilliseconds} milliseconds round-trip latency]");
            }

            Console.WriteLine();

            Dictionary<string, MongoClient> regionToMongoClient = new Dictionary<string, MongoClient>();

            foreach (KeyValuePair<string, TimeSpan> availableRegion in availableRegions)
            {
                MongoClient regionMongoClient = Helpers.GetMongoClientWithPreferredWriteRegion(mongoClient.Settings, availableRegion.Key);
                string actualWriteRegion = await Program.WaitForConnectionsToAllServersAsync(regionMongoClient, TimeSpan.FromSeconds(30));
                regionToMongoClient[actualWriteRegion] = regionMongoClient;

                if (!string.Equals(actualWriteRegion, availableRegion.Key, StringComparison.Ordinal))
                {
                    Console.WriteLine("Actual write region differs from preferred write region (is multi-master enabled for this account?).");
                }
            }

            Console.WriteLine();

            return regionToMongoClient;
        }

        /// <summary>
        /// Wait for connections to be established to all replica set members
        /// </summary>
        private static async Task<string> WaitForConnectionsToAllServersAsync(MongoClient mongoClient, TimeSpan timeout)
        {
            await mongoClient.ListDatabasesAsync();

            Stopwatch stopwatch = Stopwatch.StartNew();
            while (mongoClient.Cluster.Description.Servers.Any(x => x.State != ServerState.Connected))
            {
                if (stopwatch.ElapsedMilliseconds > timeout.TotalMilliseconds)
                {
                    throw new InvalidOperationException("Timed out waiting for connections to all replica set members.");
                }

                await Task.Delay(1000);
            }

            ServerDescription primaryServerDescription = mongoClient.Cluster.Description.Servers.First(x => x.Type == ServerType.ReplicaSetPrimary);
            string region = Helpers.TryGetRegionFromTags(primaryServerDescription.Tags) ?? string.Empty;

            string verifyPrimaryRegion = Program.GetPreferredWriteRegionFromApplicationName(mongoClient.Settings.ApplicationName);
            Console.WriteLine($"Requested primary region {verifyPrimaryRegion ?? "<default>"}; received primary region {region}.");
            return region;
        }

        /// <summary>
        /// Extract the preferred write region from the application name
        /// </summary>
        private static string GetPreferredWriteRegionFromApplicationName(string applicationName)
        {
            if (applicationName == null)
            {
                return null;
            }

            int lastIndexOfAmpersand = applicationName.LastIndexOf('@');
            string preferredWriteRegion = applicationName.Substring(lastIndexOfAmpersand + 1);
            return preferredWriteRegion;
        }
    }
}
