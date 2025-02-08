namespace TestWorkloadV2
{
    using System.Collections.Generic;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using MongoDB.Bson.Serialization;
    using MongoDB.Bson;
    using MongoDB.Driver;
    using System.Net;
    using System;

    internal class Mongo : IDriver
    {
        internal class Configuration : CommonConfiguration
        {
            public string MongoFlavor { get; set; }

            // Applies only to CosmosDBRU flavor
            public int? ThroughputToProvision { get; set; }

            // Applies only to CosmosDBRU flavor
            public bool? IsSharedThroughput { get; set; }

            // Applies only to CosmosDBRU flavor
            public bool? IsAutoScale { get; set; }
        }

        internal class GetLastRequestStatisticsCommand : Command<Dictionary<string, object>>
        {
            public override RenderedCommand<Dictionary<string, object>> Render(IBsonSerializerRegistry serializerRegistry)
            {
                return new RenderedCommand<Dictionary<string, object>>(new BsonDocument("getLastRequestStatistics", 1), serializerRegistry.GetSerializer<Dictionary<string, object>>());
            }
        }

        enum MongoFlavor
        {
            CosmosDBRU,
            CosmosDBvCore,
            Native
        }

        private Configuration configuration;
        private MongoFlavor mongoFlavor;
        private MongoClient mongoClient;
        private IMongoDatabase database;
        private IMongoCollection<MyDocument> collection;
        private DataSource dataSource;
        private InsertOneOptions insertOneOptions;
        private int isExceptionPrinted;
        private Random random;

        public async Task<(CommonConfiguration, DataSource)> InitializeAsync(IConfigurationRoot configurationRoot)
        {
            this.configuration = new Configuration();
            configurationRoot.Bind(this.configuration);
            string connectionString = configurationRoot.GetValue<string>(this.configuration.ConnectionStringRef);

            MongoClientSettings settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
            this.configuration.ConnectionStringForLogging = settings.Server.Host;


            this.configuration.SetConnectionPoolLimits();

            settings.MinConnectionPoolSize = this.configuration.MinConnectionPoolSize.Value;
            settings.MaxConnectionPoolSize = this.configuration.MaxConnectionPoolSize.Value;
            settings.SslSettings = new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };

            this.mongoClient = new MongoClient(settings);
            this.database = this.mongoClient.GetDatabase(this.configuration.DatabaseName);
            this.collection = this.database.GetCollection<MyDocument>(this.configuration.ContainerName);


            if (!string.IsNullOrEmpty(this.configuration.MongoFlavor))
            {
                if (!Enum.TryParse<MongoFlavor>(this.configuration.MongoFlavor, out this.mongoFlavor))
                {
                    throw new Exception($"Invalid Mongo flavor {this.configuration.MongoFlavor}");
                }
            }
            else
            {
                this.mongoFlavor = settings.Server.Host.Contains("mongocluster.cosmos.azure.com") ? MongoFlavor.CosmosDBvCore 
                    :  settings.Server.Host.Contains("mongo.cosmos") ? MongoFlavor.CosmosDBRU 
                    : MongoFlavor.Native;
                this.configuration.MongoFlavor = this.mongoFlavor.ToString();
            }

            // todo?
            // BsonSerializer.RegisterSerializer(typeof(ulong), new UInt64Serializer(BsonType.Decimal128));

            if (this.configuration.ShouldRecreateContainerOnStart)
            {
                await this.database.DropCollectionAsync(this.configuration.ContainerName);

                await this.database.CreateCollectionAsync(this.configuration.ContainerName);
            }

            this.dataSource = await DataSource.CreateAsync(this.configuration,
            paddingGenerator: (DataSource d) =>
            {
                (MyDocument tempDoc, _) = d.GetNextItemToInsert();
                int currentLen = tempDoc.ToBson().Length;
                int systemPropertiesLen = this.mongoFlavor == MongoFlavor.CosmosDBRU ? 100 : 0;
                string padding = this.configuration.ItemSize > currentLen ? new string('x', this.configuration.ItemSize - currentLen - systemPropertiesLen) : string.Empty;
                return Task.FromResult(padding);
            },
            initialItemIdFinder: async () =>
            {
                int workerIndex = this.configuration.WorkerIndex ?? 0;
                long lastId = this.configuration.ShouldRecreateContainerOnStart
                    ? workerIndex * DataSource.WorkerIdMultiplier
                    : await this.BinarySearchExistingIdAsync(workerIndex * DataSource.WorkerIdMultiplier, (workerIndex + 1) * DataSource.WorkerIdMultiplier);
                return lastId;
            });

            this.insertOneOptions = new InsertOneOptions();
            this.random = new Random(Configuration.RandomSeed);

            return (this.configuration, this.dataSource);
        }

        private async Task<long> BinarySearchExistingIdAsync(long start, long end)
        {
            if(start == end) 
            { 
                return start; 
            }

            long mid = (start + end) / 2;
            string midId = DataSource.GetId(mid);
            IAsyncCursor<MyDocument> docsFound = await this.collection.FindAsync<MyDocument>(doc => doc.Id == midId);
            if(!docsFound.Any())
            {
                end = mid;
                return await this.BinarySearchExistingIdAsync(start, end);
            }
            else
            {
                start = mid + 1;
                return await this.BinarySearchExistingIdAsync(start, end);
            }
        }

        public Task CleanupAsync()
        {
            return Task.CompletedTask;
        }

        public Task MakeRequestAsync(CancellationToken cancellationToken, out object context)
        {
            context = null;

            if (this.configuration.RequestType == RequestType.Create)
            {
                (MyDocument doc, _) = this.dataSource.GetNextItemToInsert();
                return this.collection.InsertOneAsync(doc, this.insertOneOptions, cancellationToken);
            }
            else if (this.configuration.RequestType == RequestType.PointRead)
            {
                long randomId = this.random.NextInt64(this.dataSource.InitialItemId);
                string id = DataSource.GetId(randomId);
                return this.collection.FindAsync((MyDocument doc) => doc.Id == id);
            }

            throw new NotImplementedException(this.configuration.RequestType.ToString());
        }

        public ResponseAttributes HandleResponse(Task request, object context)
        {
            ResponseAttributes responseAttributes = default;
            if (request.IsCompletedSuccessfully)
            {
                responseAttributes.StatusCode = HttpStatusCode.OK;
                //if (this.mongoFlavor == MongoFlavor.CosmosDBRU)
                //{
                //    Dictionary<string, object> stats = this.database.RunCommand(new GetLastRequestStatisticsCommand());
                //    responseAttributes.RequestCharge = (double)stats["RequestCharge"];
                //    responseAttributes.RequestLatency = TimeSpan.FromMilliseconds((int)stats["RequestDurationInMilliseconds"]);
                //}
            }
            else
            {
                if(Interlocked.CompareExchange(ref this.isExceptionPrinted, 1, 0) == 0)
                {
                    Console.WriteLine(request.Exception.ToString());
                }

                responseAttributes.StatusCode = HttpStatusCode.InternalServerError;
            }

            return responseAttributes;
        }

    }
}
