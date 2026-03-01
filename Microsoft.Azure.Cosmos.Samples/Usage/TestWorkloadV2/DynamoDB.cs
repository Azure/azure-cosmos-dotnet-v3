namespace TestWorkloadV2
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.DynamoDBv2;
    using Amazon.DynamoDBv2.Model;
    using Amazon.Runtime;
    using Microsoft.Extensions.Configuration;

    internal class DynamoDB : IDriver
    {
        internal class Configuration : CommonConfiguration
        {
            public string RegionEndpoint { get; set; }
            public string AccessKeyId { get; set; }
            public string SecretAccessKey { get; set; }
            public int? ReadCapacityUnits { get; set; }
            public int? WriteCapacityUnits { get; set; }
        }

        private Configuration configuration;
        private AmazonDynamoDBClient dynamoClient;
        private DataSource dataSource;
        private Random random;
        private int isExceptionPrinted;

        public async Task<(CommonConfiguration, DataSource)> InitializeAsync(IConfigurationRoot configurationRoot)
        {
            this.configuration = new Configuration();
            configurationRoot.Bind(this.configuration);

            AmazonDynamoDBConfig clientConfig = new AmazonDynamoDBConfig();

            this.configuration.SetConnectionPoolLimits();
            if (this.configuration.MaxConnectionPoolSize.HasValue)
            {
                clientConfig.MaxConnectionsPerServer = this.configuration.MaxConnectionPoolSize.Value;
            }

            AWSCredentials credentials = new BasicAWSCredentials(this.configuration.AccessKeyId, this.configuration.SecretAccessKey);
            string connectionString = configurationRoot.GetValue<string>(this.configuration.ConnectionStringRef);
            if (!string.IsNullOrEmpty(connectionString))
            {
                clientConfig.ServiceURL = connectionString;
                this.dynamoClient = new AmazonDynamoDBClient(credentials, clientConfig);
            }
            else
            {
                clientConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(this.configuration.RegionEndpoint);
                this.dynamoClient = new AmazonDynamoDBClient(credentials, clientConfig);
            }

            this.configuration.ConnectionStringForLogging = clientConfig.ServiceURL ?? clientConfig.RegionEndpoint?.DisplayName;

            if (this.configuration.ShouldRecreateContainerOnStart)
            {
                try
                {
                    await this.dynamoClient.DeleteTableAsync(this.configuration.ContainerName);
                    
                    // Wait for table to be deleted
                    bool tableDeleted = false;
                    for (int i = 0; i < 30; i++)
                    {
                        await Task.Delay(1000);
                        try
                        {
                            await this.dynamoClient.DescribeTableAsync(this.configuration.ContainerName);
                        }
                        catch (ResourceNotFoundException)
                        {
                            tableDeleted = true;
                            break;
                        }
                    }

                    if (!tableDeleted)
                    {
                        throw new Exception($"Table {this.configuration.ContainerName} was not deleted in time");
                    }
                }
                catch (ResourceNotFoundException)
                {
                    // Table doesn't exist, continue
                }

                CreateTableRequest createTableRequest = new CreateTableRequest
                {
                    TableName = this.configuration.ContainerName,
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new AttributeDefinition { AttributeName = "pk", AttributeType = "S" },
                        new AttributeDefinition { AttributeName = "id", AttributeType = "S" }
                    },
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "pk", KeyType = "HASH" },
                        new KeySchemaElement { AttributeName = "id", KeyType = "RANGE" }
                    },
                    ProvisionedThroughput = new ProvisionedThroughput
                    {
                        ReadCapacityUnits = this.configuration.ReadCapacityUnits,
                        WriteCapacityUnits = this.configuration.WriteCapacityUnits
                    }
                };

                await this.dynamoClient.CreateTableAsync(createTableRequest);

                // Wait for table to be active
                bool tableActive = false;
                for (int i = 0; i < 60; i++)
                {
                    await Task.Delay(1000);
                    DescribeTableResponse describeResponse = await this.dynamoClient.DescribeTableAsync(this.configuration.ContainerName);
                    if (describeResponse.Table.TableStatus == TableStatus.ACTIVE)
                    {
                        tableActive = true;
                        break;
                    }
                }

                if (!tableActive)
                {
                    throw new Exception($"Table {this.configuration.ContainerName} did not become active in time");
                }
            }

            this.dataSource = await DataSource.CreateAsync(this.configuration,
                paddingGenerator: (DataSource d) =>
                {
                    (MyDocument doc, _) = d.GetNextItemToInsert();
                    int currentLen = doc.Id.Length + doc.PK.Length + (doc.Other?.Length ?? 0);
                    string padding = this.configuration.ItemSize > currentLen ? new string('x', this.configuration.ItemSize - currentLen) : string.Empty;
                    return Task.FromResult(padding);
                },
                initialItemIdFinder: null);

            this.random = new Random(CommonConfiguration.RandomSeed);

            return (this.configuration, this.dataSource);
        }

        public Task CleanupAsync()
        {
            this.dynamoClient?.Dispose();
            return Task.CompletedTask;
        }

        public Task MakeRequestAsync(CancellationToken cancellationToken, out object context)
        {
            context = null;

            if (this.configuration.RequestType == RequestType.Create)
            {
                (MyDocument doc, _) = this.dataSource.GetNextItemToInsert();

                Dictionary<string, AttributeValue> item = new Dictionary<string, AttributeValue>
                {
                    { "id", new AttributeValue { S = doc.Id } },
                    { "pk", new AttributeValue { S = doc.PK } },
                    { "other", new AttributeValue { S = doc.Other ?? string.Empty } }
                };

                if (doc.Arr != null && doc.Arr.Count > 0)
                {
                    item["arr"] = new AttributeValue { SS = doc.Arr };
                }

                PutItemRequest request = new PutItemRequest
                {
                    TableName = this.configuration.ContainerName,
                    Item = item,
                    ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
                };

                return this.dynamoClient.PutItemAsync(request, cancellationToken);
            }
            else if (this.configuration.RequestType == RequestType.PointRead)
            {
                long randomId = this.random.NextInt64(this.dataSource.InitialItemId);
                string id = DataSource.GetId(randomId);
                int pkIndex = (int)(randomId % this.configuration.PartitionKeyCount);
                string pk = this.dataSource.PartitionKeyStrings[pkIndex];

                GetItemRequest request = new GetItemRequest
                {
                    TableName = this.configuration.ContainerName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "pk", new AttributeValue { S = pk } },
                        { "id", new AttributeValue { S = id } }
                    },
                    ConsistentRead = false
                };

                return this.dynamoClient.GetItemAsync(request, cancellationToken);
            }

            throw new NotImplementedException(this.configuration.RequestType.ToString());
        }

        public ResponseAttributes HandleResponse(Task request, object context)
        {
            ResponseAttributes responseAttributes = default;
            
            if (request.IsCompletedSuccessfully)
            {
                responseAttributes.StatusCode = HttpStatusCode.OK;
                
                // Extract capacity units consumed if available
                if (request is Task<PutItemResponse> putTask)
                {
                    PutItemResponse response = putTask.Result;
                    responseAttributes.RequestCharge = response.ConsumedCapacity?.CapacityUnits ?? 0;
                }
                else if (request is Task<GetItemResponse> getTask)
                {
                    GetItemResponse response = getTask.Result;
                    responseAttributes.RequestCharge = response.ConsumedCapacity?.CapacityUnits ?? 0;
                }
            }
            else
            {
                if (Interlocked.CompareExchange(ref this.isExceptionPrinted, 1, 0) == 0)
                {
                    Console.WriteLine(request.Exception.ToString());
                }

                responseAttributes.StatusCode = HttpStatusCode.InternalServerError;
            }

            return responseAttributes;
        }
    }
}
