﻿namespace TestWorkloadV2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    internal class CosmosDBNoSql : IDriver
    {
        internal class Configuration : CommonConfiguration
        {
            public int ThroughputToProvision { get; set; }
            public bool IsSharedThroughput { get; set; }
            public bool IsAutoScale { get; set; }
            public bool ShouldContainerIndexAllProperties { get; set; }

            public int ItemPropertyCount { get; set; }

            public bool IsGatewayMode { get; set; }
            public bool AllowBulkExecution { get; set; }
            public bool OmitContentInWriteResponse { get; set; }
        }

        private Configuration configuration;
        private DataSource dataSource;
        private CosmosClient client;
        private Container container;
        private PartitionKey[] partitionKeys;
        private ItemRequestOptions itemRequestOptions;
        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };

        public async Task<(CommonConfiguration, DataSource)> InitializeAsync(IConfigurationRoot configurationRoot)
        {
            this.configuration = new Configuration();
            configurationRoot.Bind(this.configuration);

            string connectionString = configurationRoot.GetValue<string>(this.configuration.ConnectionStringRef);

            this.client = this.GetClientInstance(connectionString);
            this.configuration.ConnectionStringForLogging = this.client.Endpoint.ToString();

            this.container = this.client.GetDatabase(this.configuration.DatabaseName).GetContainer(this.configuration.ContainerName);
            if (this.configuration.ShouldRecreateContainerOnStart)
            {
                this.container = await RecreateContainerAsync(this.client, this.configuration.DatabaseName, this.configuration.ContainerName, this.configuration.ShouldContainerIndexAllProperties, this.configuration.ThroughputToProvision, this.configuration.IsSharedThroughput, this.configuration.IsAutoScale);
                await Task.Delay(5000);
            }

            try
            {
                await this.container.ReadContainerAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in reading collection: {0}", ex);
                throw;
            }

            this.dataSource = new DataSource(this.configuration);

            this.partitionKeys = new PartitionKey[this.configuration.PartitionKeyCount];
            for (int pkIndex = 0; pkIndex < this.configuration.PartitionKeyCount; pkIndex++)
            {
                this.partitionKeys[pkIndex] = new PartitionKey(this.dataSource.PartitionKeyStrings[pkIndex]);
            }

            // Find length and keep some bytes for the system generated properties
            (MemoryStream stream, _) = this.GetNextItem();
            int currentLen = (int)stream.Length + 205;
            string padding = this.configuration.ItemSize > currentLen ? new string('x', this.configuration.ItemSize - currentLen) : string.Empty;
            this.dataSource.InitializePaddingAndInitialItemId(padding);

            this.itemRequestOptions = null;
            if (this.configuration.OmitContentInWriteResponse)
            {
                this.itemRequestOptions = new ItemRequestOptions()
                {
                    EnableContentResponseOnWrite = false
                };
            }

            return (this.configuration, this.dataSource);
        }

        public async Task CleanupAsync()
        {
            if (this.configuration.ShouldDeleteContainerOnFinish)
            {
                await CleanupContainerAsync(this.container);
            }

            this.client.Dispose();
        }

        public Task MakeRequestAsync(CancellationToken cancellationToken, out object context)
        {
            (MemoryStream stream, PartitionKey partitionKeyValue) = this.GetNextItem();
            context = stream;

            return this.container.UpsertItemStreamAsync(stream, partitionKeyValue, this.itemRequestOptions, cancellationToken);
        }

        public ResponseAttributes HandleResponse(Task request, object context)
        {
            Task<ResponseMessage> task = (Task<ResponseMessage>)request;
            Stream stream = (Stream)context;
            if (request.IsCompletedSuccessfully)
            {
                stream?.Dispose();

                using (ResponseMessage responseMessage = task.Result)
                {
                    ResponseAttributes responseAttributes;
                    responseAttributes.StatusCode = responseMessage.StatusCode;
                    //responseAttributes.RequestLatency = responseMessage.Diagnostics.GetClientElapsedTime();
                    responseAttributes.RequestCharge = responseMessage.Headers.RequestCharge;
                    return responseAttributes;
                }
            }
            else
            {
                // observe exception
                _ = task.Exception;
            }

            return default;
        }


        private CosmosClient GetClientInstance(string connectionString)
        {
            return new CosmosClient(connectionString, new CosmosClientOptions()
            {
                ConnectionMode = this.configuration.IsGatewayMode ? ConnectionMode.Gateway : ConnectionMode.Direct,
                AllowBulkExecution = this.configuration.AllowBulkExecution,
                MaxRetryAttemptsOnRateLimitedRequests = 0,
                PortReuseMode = PortReuseMode.ReuseUnicastPort
            });
        }

        private static async Task CleanupContainerAsync(Container container)
        {
            if (container != null)
            {
                try
                {
                    await container.DeleteContainerStreamAsync();
                }
                catch (Exception)
                {
                }
            }
        }

        private static async Task<Container> RecreateContainerAsync(
            CosmosClient client,
            string databaseName,
            string containerName,
            bool shouldIndexAllProperties,
            int throughputToProvision,
            bool isSharedThroughput,
            bool isAutoScale)
        {
            ThroughputProperties throughputProperties = isAutoScale
                ? ThroughputProperties.CreateAutoscaleThroughput(throughputToProvision)
                : ThroughputProperties.CreateManualThroughput(throughputToProvision);
            (string desiredThroughputType, int desiredThroughputValue) = GetThroughputTypeAndValue(throughputProperties);

            Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName, isSharedThroughput ? throughputProperties : null);
            ThroughputProperties existingDatabaseThroughput;
            try
            {
                existingDatabaseThroughput = await database.ReadThroughputAsync(requestOptions: null);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // the database does not have throughput provisioned
                existingDatabaseThroughput = null;
            }

            if (isSharedThroughput && existingDatabaseThroughput != null)
            {
                (string existingThroughputType, int existingThroughputValue) = GetThroughputTypeAndValue(existingDatabaseThroughput);
                if (existingThroughputType == desiredThroughputType)
                {
                    if (existingThroughputValue != desiredThroughputValue)
                    {
                        Console.WriteLine($"Setting database {existingThroughputType} throughput to ${desiredThroughputValue}");
                        await database.ReplaceThroughputAsync(throughputProperties);
                    }
                }
                else
                {
                    throw new Exception($"Cannot set desired database throughput; existing {existingThroughputType} throughput is {existingThroughputValue}.");
                }
            }

            Console.WriteLine("Deleting old container if it exists.");
            await CleanupContainerAsync(database.GetContainer(containerName));

            if (!isSharedThroughput)
            {
                Console.WriteLine($"Creating container with {desiredThroughputType} throughput {desiredThroughputValue}...");
            }
            else
            {
                Console.WriteLine("Creating container");
            }

            ContainerBuilder containerBuilder = database.DefineContainer(containerName, "/pk");

            if (!shouldIndexAllProperties)
            {
                containerBuilder.WithIndexingPolicy()
                    .WithIndexingMode(IndexingMode.Consistent)
                    .WithIncludedPaths()
                        .Path("/")
                        .Attach()
                    .WithExcludedPaths()
                        .Path("/other/*")
                        .Attach()
                .Attach();
            }

            return await containerBuilder.CreateAsync(isSharedThroughput ? null : throughputProperties);
        }

        private static (string, int) GetThroughputTypeAndValue(ThroughputProperties throughputProperties)
        {
            string type = throughputProperties.AutoscaleMaxThroughput.HasValue ? "auto-scale" : "manual";
            int value = throughputProperties.AutoscaleMaxThroughput ?? throughputProperties.Throughput.Value;
            return (type, value);
        }

        public (MemoryStream, PartitionKey) GetNextItem()
        {
            (MyDocument myDocument, int currentPKIndex) = this.dataSource.GetNextItemToInsert();
            string value = JsonConvert.SerializeObject(myDocument, JsonSerializerSettings);
            return (new MemoryStream(Encoding.UTF8.GetBytes(value)), this.partitionKeys[currentPKIndex]);
        }
    }
}
