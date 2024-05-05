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
            public int ThroughputToProvision { get; set; }

            // Applies only to CosmosDBRU flavor
            public bool IsSharedThroughput { get; set; }

            // Applies only to CosmosDBRU flavor
            public bool IsAutoScale { get; set; }
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

            this.dataSource = new DataSource(this.configuration);

            // setup padding
            (MyDocument tempDoc, _) = this.dataSource.GetNextItem();
            int currentLen = tempDoc.ToBson().Length;
            int systemPropertiesLen = this.mongoFlavor == MongoFlavor.CosmosDBRU ? 100 : 0;
            string padding = this.configuration.ItemSize > currentLen ? new string('x', this.configuration.ItemSize - currentLen - systemPropertiesLen) : string.Empty;

            int lastId = -1;
            if (this.configuration.ShouldRecreateContainerOnStart)
            {
                await this.database.DropCollectionAsync(this.configuration.ContainerName);

                await this.database.CreateCollectionAsync(this.configuration.ContainerName);
            }
            else
            {
                MyDocument lastDoc = await this.collection.Find<MyDocument>(Builders<MyDocument>.Filter.Empty).SortByDescending(d => d.Id).Limit(1).FirstOrDefaultAsync();
                if (lastDoc != null)
                {
                    int.TryParse(lastDoc.Id, out lastId);
                }
            }

            this.dataSource.InitializePaddingAndInitialItemId(padding, lastId + 1);

            this.insertOneOptions = new InsertOneOptions();
            this.random = new Random(Configuration.RandomSeed);



            return (this.configuration, this.dataSource);
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
                (MyDocument doc, _) = this.dataSource.GetNextItem();
                return this.collection.InsertOneAsync(doc, this.insertOneOptions, cancellationToken);
            }
            else if (this.configuration.RequestType == RequestType.PointRead)
            {
                int randomId = this.random.Next(this.dataSource.InitialItemId);
                string id = this.dataSource.GetId(randomId);
                return this.collection.FindAsync((MyDocument doc) => doc.Id == id);
            }
            else
            { 
                throw new NotSupportedException(); 
            }
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
