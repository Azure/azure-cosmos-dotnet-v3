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

            public int MinConnectionPoolSize { get; set; }

            public int MaxConnectionPoolSize { get; set; }

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

        public async Task<(CommonConfiguration, DataSource)> InitializeAsync(IConfigurationRoot configurationRoot)
        {
            this.configuration = new Configuration();
            configurationRoot.Bind(this.configuration);

           if(!Enum.TryParse<MongoFlavor>(this.configuration.MongoFlavor, out this.mongoFlavor))
            {
                throw new Exception($"Invalid Mongo flavor {this.configuration.MongoFlavor}");
            }

            MongoClientSettings settings = MongoClientSettings.FromUrl(new MongoUrl(this.configuration.ConnectionString));
            this.configuration.ConnectionStringForLogging = settings.Server.Host;

            settings.MinConnectionPoolSize = this.configuration.MinConnectionPoolSize;
            settings.MaxConnectionPoolSize = this.configuration.MaxConnectionPoolSize;
            settings.SslSettings = new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };

            this.mongoClient = new MongoClient(settings);
            this.database = this.mongoClient.GetDatabase(this.configuration.DatabaseName);
            this.collection = this.database.GetCollection<MyDocument>(this.configuration.ContainerName);

            // todo?
            // BsonSerializer.RegisterSerializer(typeof(ulong), new UInt64Serializer(BsonType.Decimal128));

            this.dataSource = new DataSource(this.configuration);
            (MyDocument tempDoc, _) = this.dataSource.GetNextItem();
            int currentLen = tempDoc.ToBson().Length;
            int systemPropertiesLen = this.mongoFlavor == MongoFlavor.CosmosDBRU ? 100 : 0;
            string padding = this.configuration.ItemSize > currentLen ? new string('x', this.configuration.ItemSize - currentLen - systemPropertiesLen) : string.Empty;
            this.dataSource.InitializePadding(padding);

            this.insertOneOptions = new InsertOneOptions();

            if(this.configuration.ShouldRecreateContainerOnStart)
            {
                await this.database.DropCollectionAsync(this.configuration.ContainerName);

                await this.database.CreateCollectionAsync(this.configuration.ContainerName);
            }

            return (this.configuration, this.dataSource);
        }

        public Task CleanupAsync()
        {
            return Task.CompletedTask;
        }

        public Task MakeRequestAsync(CancellationToken cancellationToken, out object context)
        {
            context = null;
            (MyDocument doc, _) = this.dataSource.GetNextItem();
            return this.collection.InsertOneAsync(doc, this.insertOneOptions, cancellationToken);
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
