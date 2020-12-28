//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    internal static class TestCommon
    {
        public const int MinimumOfferThroughputToCreateElasticCollectionInTests = 10100;
        public const int CollectionQuotaForDatabaseAccountForQuotaCheckTests = 2;
        public const int NumberOfPartitionsPerCollectionInLocalEmulatorTest = 5;
        public const int CollectionQuotaForDatabaseAccountInTests = 16;
        public const int CollectionPartitionQuotaForDatabaseAccountInTests = 100;
        public const int TimeinMSTakenByTheMxQuotaConfigUpdateToRefreshInTheBackEnd = 240000; // 240 seconds
        public const int Gen3MaxCollectionCount = 16;
        public const int Gen3MaxCollectionSizeInKB = 256 * 1024;
        public const int MaxCollectionSizeInKBWithRuntimeServiceBindingEnabled = 1024 * 1024;
        public const int ReplicationFactor = 3;
        public static readonly int TimeToWaitForOperationCommitInSec = 2;

        private static readonly int serverStalenessIntervalInSeconds;
        private static readonly int masterStalenessIntervalInSeconds;

        // Passing in the a custom serializer instance will cause all the checks for internal types to validated.
        public static readonly CosmosSerializerCore SerializerCore = new CosmosSerializerCore(new CosmosJsonDotNetSerializer());

        static TestCommon()
        {
            TestCommon.serverStalenessIntervalInSeconds = int.Parse(ConfigurationManager.AppSettings["ServerStalenessIntervalInSeconds"], CultureInfo.InvariantCulture);
            TestCommon.masterStalenessIntervalInSeconds = int.Parse(ConfigurationManager.AppSettings["MasterStalenessIntervalInSeconds"], CultureInfo.InvariantCulture);
        }

        internal static MemoryStream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        internal static (string endpoint, string authKey) GetAccountInfo()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];

            return (endpoint, authKey);
        }

        internal static CosmosClientBuilder GetDefaultConfiguration(bool useCustomSeralizer = true)
        {
            (string endpoint, string authKey) accountInfo = TestCommon.GetAccountInfo();
            CosmosClientBuilder clientBuilder = new CosmosClientBuilder(accountEndpoint: accountInfo.endpoint, authKeyOrResourceToken: accountInfo.authKey);
            if (useCustomSeralizer)
            {
                clientBuilder.WithCustomSerializer(new CosmosJsonDotNetSerializer());
            }

            return clientBuilder;
        }

        internal static CosmosClient CreateCosmosClient(Action<CosmosClientBuilder> customizeClientBuilder = null, bool useCustomSeralizer = true)
        {
            CosmosClientBuilder cosmosClientBuilder = GetDefaultConfiguration(useCustomSeralizer);
            if (customizeClientBuilder != null)
            {
                customizeClientBuilder(cosmosClientBuilder);
            }

            CosmosClient client = cosmosClientBuilder.Build();
            Assert.IsNotNull(client.ClientOptions.Serializer);
            return client;
        }

        internal static CosmosClient CreateCosmosClient(CosmosClientOptions clientOptions, string resourceToken = null)
        {
            string authKey = resourceToken ?? ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];

            return new CosmosClient(endpoint, authKey, clientOptions);
        }

        internal static CosmosClient CreateCosmosClient(
            bool useGateway)
        {
            CosmosClientBuilder cosmosClientBuilder = GetDefaultConfiguration();
            if (useGateway)
            {
                cosmosClientBuilder.WithConnectionModeGateway();
            }

            return cosmosClientBuilder.Build();
        }

        internal static DocumentClient CreateClient(bool useGateway, Protocol protocol = Protocol.Tcp,
            int timeoutInSeconds = 60,
            ConsistencyLevel? defaultConsistencyLevel = null,
            AuthorizationTokenType tokenType = AuthorizationTokenType.PrimaryMasterKey,
            bool createForGeoRegion = false,
            bool enableEndpointDiscovery = true,
            bool? enableReadRequestFallback = null,
            List<string> preferredLocations = null,
            RetryOptions retryOptions = null,
            ApiType apiType = ApiType.None,
            EventHandler<ReceivedResponseEventArgs> recievedResponseEventHandler = null,
            bool useMultipleWriteLocations = false)
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];

            // The Public emulator has no other keys
            //switch (tokenType)
            //{
            //    case AuthorizationTokenType.PrimaryMasterKey:
            //        authKey = ConfigurationManager.AppSettings["MasterKey"];
            //        break;

            //    case AuthorizationTokenType.SystemReadOnly:
            //        authKey = ConfigurationManager.AppSettings["ReadOnlySystemKey"];
            //        break;

            //    case AuthorizationTokenType.SystemReadWrite:
            //        authKey = ConfigurationManager.AppSettings["ReadWriteSystemKey"];
            //        break;

            //    case AuthorizationTokenType.SystemAll:
            //        authKey = ConfigurationManager.AppSettings["FullSystemKey"];
            //        break;
            //    case AuthorizationTokenType.PrimaryReadonlyMasterKey:
            //        authKey = ConfigurationManager.AppSettings["primaryReadonlyMasterKey"];
            //        break;
            //    default:
            //        throw new ArgumentException("tokenType");
            //}

            // DIRECT MODE has ReadFeed issues in the Public emulator
            ConnectionPolicy connectionPolicy = null;
            if (useGateway)
            {
                connectionPolicy = new ConnectionPolicy
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    RequestTimeout = TimeSpan.FromSeconds(timeoutInSeconds),
                    EnableEndpointDiscovery = enableEndpointDiscovery,
                    EnableReadRequestsFallback = enableReadRequestFallback,
                    UseMultipleWriteLocations = useMultipleWriteLocations
                };
            }
            else
            {
                connectionPolicy = new ConnectionPolicy
                {
                    ConnectionMode = ConnectionMode.Direct,
                    ConnectionProtocol = protocol,
                    RequestTimeout = TimeSpan.FromSeconds(timeoutInSeconds),
                    EnableEndpointDiscovery = enableEndpointDiscovery,
                    EnableReadRequestsFallback = enableReadRequestFallback,
                    UseMultipleWriteLocations = useMultipleWriteLocations,
                };
            }

            if (retryOptions != null)
            {
                connectionPolicy.RetryOptions = retryOptions;
            }

            if (preferredLocations != null)
            {
                foreach (string preferredLocation in preferredLocations)
                {
                    connectionPolicy.PreferredLocations.Add(preferredLocation);
                }
            }

            Uri uri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);

            DocumentClient client = new DocumentClient(
                uri,
                authKey,
                null,
                connectionPolicy,
                defaultConsistencyLevel,
                new JsonSerializerSettings()
                {
                    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
                },
                apiType,
                recievedResponseEventHandler);

            return client;
        }

        internal static DocumentClient CreateNonSslTestClient(string key, int timeoutInSeconds = 10)
        {
            return new DocumentClient(
                new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                key,
                (HttpMessageHandler)null,
                new ConnectionPolicy
                {
                    ConnectionMode = ConnectionMode.Direct,
                    ConnectionProtocol = Protocol.Tcp,
                    RequestTimeout = TimeSpan.FromSeconds(timeoutInSeconds)
                });
        }

        internal static void AddMasterAuthorizationHeader(this HttpClient client, string verb,
            string resourceId,
            string resourceType,
            INameValueCollection headers,
            string key)
        {
            if (string.IsNullOrEmpty(verb)) throw new ArgumentException("verb");
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("key");
            if (headers == null) throw new ArgumentNullException("headers");

            string xDate = DateTime.UtcNow.ToString("r");

            client.DefaultRequestHeaders.Remove(HttpConstants.HttpHeaders.XDate);
            client.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.XDate, xDate);

            headers.Remove(HttpConstants.HttpHeaders.XDate);
            headers.Add(HttpConstants.HttpHeaders.XDate, xDate);

            client.DefaultRequestHeaders.Remove(HttpConstants.HttpHeaders.Authorization);
            client.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Authorization,
                AuthorizationHelper.GenerateKeyAuthorizationSignature(verb, resourceId, resourceType, headers, key));
        }

        internal static IList<T> ListAll<T>(DocumentClient client,
            string resourceIdOrFullName,
            INameValueCollection headers = null,
            bool readWithRetry = false) where T : Resource, new()
        {
            List<T> result = new List<T>();

            INameValueCollection localHeaders = new StoreRequestNameValueCollection();
            if (headers != null)
            {
                localHeaders.Add(headers);
            }

            string continuationToken = null;
            DocumentFeedResponse<T> pagedResult = null;
            do
            {
                if (!string.IsNullOrEmpty(continuationToken))
                {
                    localHeaders[HttpConstants.HttpHeaders.Continuation] = continuationToken;
                }

                if (readWithRetry)
                {
                    pagedResult = client.ReadFeedWithRetry<T>(resourceIdOrFullName,
                        localHeaders);
                }
                else
                {
                    pagedResult = client.ReadFeed<T>(resourceIdOrFullName,
                        localHeaders);
                }

                if (typeof(T) == typeof(Document))
                {
                    foreach (T entry in pagedResult)
                    {
                        Document document = (Document)(object)entry;
                        result.Add(entry);
                    }
                }
                else
                {
                    result.AddRange(pagedResult);
                }
                continuationToken = pagedResult.ResponseContinuation;
            } while (!string.IsNullOrEmpty(pagedResult.ResponseContinuation));

            return result;
        }

        private static DocumentServiceRequest CreateRequest(
            OperationType operationType,
            string resourceIdOrFullName,
            ResourceType resourceType,
            INameValueCollection headers,
            AuthorizationTokenType authTokenType)
        {
            if (PathsHelper.IsNameBased(resourceIdOrFullName))
            {
                return DocumentServiceRequest.CreateFromName(
                    operationType,
                    resourceIdOrFullName,
                    resourceType,
                    authTokenType,
                    headers);
            }
            else
            {
                return DocumentServiceRequest.Create(
                    operationType,
                    resourceIdOrFullName,
                    resourceType,
                    authTokenType,
                    headers);
            }
        }

        internal static void RouteToTheOnlyPartition(DocumentClient client, DocumentServiceRequest request)
        {
            ClientCollectionCache collectionCache = client.GetCollectionCacheAsync(NoOpTrace.Singleton).Result;
            ContainerProperties collection = collectionCache.ResolveCollectionAsync(request, CancellationToken.None).Result;
            IRoutingMapProvider routingMapProvider = client.GetPartitionKeyRangeCacheAsync().Result;
            IReadOnlyList<PartitionKeyRange> ranges = routingMapProvider.TryGetOverlappingRangesAsync(
                collection.ResourceId,
                Range<string>.GetPointRange(PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey),
                NoOpTrace.Singleton).Result;
            request.RouteTo(new PartitionKeyRangeIdentity(collection.ResourceId, ranges.Single().Id));
        }

        internal static Database CreateOrGetDatabase(DocumentClient client)
        {
            IList<Database> databases = TestCommon.ListAll<Database>(
                client,
                null);

            if (databases.Count == 0)
            {
                return TestCommon.CreateDatabase(client);
            }
            return databases[0];
        }

        internal static Database CreateDatabase(DocumentClient client, string databaseName = null)
        {
            string name = databaseName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = Guid.NewGuid().ToString("N");
            }
            Database database = new Database
            {
                Id = name,
            };
            return client.Create<Database>(null, database);
        }

        internal static User CreateOrGetUser(DocumentClient client)
        {
            return TestCommon.CreateOrGetUser(client, out Database ignored);
        }

        internal static User CreateOrGetUser(DocumentClient client, out Database database)
        {
            database = TestCommon.CreateOrGetDatabase(client);

            IList<User> users = TestCommon.ListAll<User>(
                client,
                database.ResourceId);

            if (users.Count == 0)
            {
                User user = new User
                {
                    Id = Guid.NewGuid().ToString("N")
                };
                return client.Create<User>(database.ResourceId, user);
            }
            return client.Read<User>(users[0].ResourceId);
        }

        internal static User UpsertUser(DocumentClient client, out Database database)
        {
            database = TestCommon.CreateOrGetDatabase(client);

            User user = new User
            {
                Id = Guid.NewGuid().ToString("N")
            };
            return client.Upsert<User>(database.GetIdOrFullName(), user);
        }

        internal static DocumentCollection CreateOrGetDocumentCollection(DocumentClient client)
        {
            return TestCommon.CreateOrGetDocumentCollection(client, out Database ignored);
        }

        internal static DocumentCollection CreateOrGetDocumentCollection(DocumentClient client, out Database database)
        {
            database = TestCommon.CreateOrGetDatabase(client);

            IList<DocumentCollection> documentCollections = TestCommon.ListAll<DocumentCollection>(
                client,
                database.ResourceId);

            if (documentCollections.Count == 0)
            {
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
                DocumentCollection documentCollection1 = new DocumentCollection
                {
                    Id = Guid.NewGuid().ToString("N"),
                    PartitionKey = partitionKeyDefinition
                };

                return TestCommon.CreateCollectionAsync(client, database, documentCollection1,
                    new RequestOptions() { OfferThroughput = 10000 }).Result;
            }

            return client.Read<DocumentCollection>(documentCollections[0].ResourceId);
        }

        internal static DocumentCollection CreateOrGetDocumentCollectionWithMultiHash(DocumentClient client, out Database database)
        {
            database = TestCommon.CreateOrGetDatabase(client);

            IList<DocumentCollection> documentCollections = TestCommon.ListAll<DocumentCollection>(
                client,
                database.ResourceId);

            if (documentCollections.Count == 0)
            {
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk","/key" }), Kind = PartitionKind.MultiHash };
                DocumentCollection documentCollection1 = new DocumentCollection
                {
                    Id = Guid.NewGuid().ToString("N"),
                    PartitionKey = partitionKeyDefinition
                };

                return TestCommon.CreateCollectionAsync(client, database, documentCollection1,
                    new RequestOptions() { OfferThroughput = 10000 }).Result;
            }

            return client.Read<DocumentCollection>(documentCollections[0].ResourceId);
        }

        internal static Document CreateOrGetDocument(DocumentClient client)
        {

            return TestCommon.CreateOrGetDocument(client, out DocumentCollection ignored1, out Database ignored2);
        }

        internal static Document CreateOrGetDocument(DocumentClient client, out DocumentCollection documentCollection, out Database database)
        {
            documentCollection = TestCommon.CreateOrGetDocumentCollection(client, out database);

            IList<Document> documents = TestCommon.ListAll<Document>(
                client,
                documentCollection.ResourceId);

            if (documents.Count == 0)
            {
                Document document1 = new Document
                {
                    Id = Guid.NewGuid().ToString("N")
                };
                Document document = client.Create<Document>(documentCollection.ResourceId, document1);
                TestCommon.WaitForServerReplication();
                return document;
            }
            else
            {
                return client.Read<Document>(documents[0].ResourceId);
            }
        }

        internal static Document UpsertDocument(DocumentClient client, out DocumentCollection documentCollection, out Database database)
        {
            documentCollection = TestCommon.CreateOrGetDocumentCollection(client, out database);

            Document document = new Document
            {
                Id = Guid.NewGuid().ToString("N")
            };

            return client.Upsert<Document>(documentCollection.GetIdOrFullName(), document);
        }

        internal static async Task CreateDataSet(DocumentClient client, string dbName, string collName, int numberOfDocuments, int inputThroughputOffer)
        {
            Random random = new Random();
            Database database = TestCommon.CreateDatabase(client, dbName);
            DocumentCollection coll = await TestCommon.CreateCollectionAsync(client,
                database,
                new DocumentCollection
                {
                    Id = collName,
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Paths = new Collection<string> { "/partitionKey" },
                        Kind = PartitionKind.Hash,
                    }
                },
                new RequestOptions { OfferThroughput = inputThroughputOffer });

            StringBuilder sb = new StringBuilder();
            List<Task<ResourceResponse<Document>>> taskList = new List<Task<ResourceResponse<Document>>>();
            for (int i = 0; i < numberOfDocuments / 100; i++)
            {

                for (int j = 0; j < 100; j++)
                {
                    sb.Append("{\"id\":\"documentId" + ((100 * i) + j));
                    sb.Append("\",\"partitionKey\":" + ((100 * i) + j));
                    for (int k = 1; k < 20; k++)
                    {
                        sb.Append(",\"field_" + k + "\":" + random.Next(100000));
                    }
                    sb.Append("}");
                    string a = sb.ToString();
                    Task<ResourceResponse<Document>> task = client.CreateDocumentAsync(coll.SelfLink, JsonConvert.DeserializeObject(sb.ToString()));
                    taskList.Add(task);
                    sb.Clear();
                }

                while (taskList.Count > 0)
                {
                    Task<ResourceResponse<Document>> firstFinishedTask = await Task.WhenAny(taskList);
                    await firstFinishedTask;
                    taskList.Remove(firstFinishedTask);
                }
            }
        }

        public static void WaitForMasterReplication()
        {
            if (TestCommon.masterStalenessIntervalInSeconds != 0)
            {
                Task.Delay(TestCommon.masterStalenessIntervalInSeconds * 1000);
            }
        }

        public static void WaitForServerReplication()
        {
            if (TestCommon.serverStalenessIntervalInSeconds != 0)
            {
                Task.Delay(TestCommon.serverStalenessIntervalInSeconds * 1000);
            }
        }
        public static DocumentFeedResponse<T> ReadFeedWithRetry<T>(this DocumentClient client, string resourceIdOrFullName, INameValueCollection headers = null) where T : Resource, new()
        {
            return client.ReadFeedWithRetry<T>(resourceIdOrFullName, out INameValueCollection ignored, headers);
        }

        public static DocumentFeedResponse<T> ReadFeedWithRetry<T>(this DocumentClient client, string resourceIdOrFullName, out INameValueCollection responseHeaders, INameValueCollection headers = null) where T : Resource, new()
        {
            int nMaxRetry = 5;

            do
            {
                try
                {
                    return client.ReadFeed<T>(resourceIdOrFullName, out responseHeaders, headers);
                }
                catch (InternalServerErrorException)
                {
                    if (--nMaxRetry > 0)
                    {
                        Task.Delay(5000); //Wait 5 seconds before retry.                     
                    }
                    else
                    {
                        Assert.Fail("Service is not available after 5 retries");
                    }
                }
                catch (ServiceUnavailableException)
                {
                    if (--nMaxRetry > 0)
                    {
                        Task.Delay(5000); //Wait 5 seconds before retry.
                    }
                    else
                    {
                        Assert.Fail("Service is not available after 5 retries");
                    }
                }
                catch (RequestTimeoutException)
                {
                    if (--nMaxRetry > 0)
                    {
                        Task.Delay(5000); //Wait 5 seconds before retry.
                    }
                    else
                    {
                        Assert.Fail("Service is not available after 5 retries");
                    }
                }
                catch (GoneException)
                {
                    if (--nMaxRetry > 0)
                    {
                        Task.Delay(5000); //Wait 5 seconds before retry.
                    }
                    else
                    {
                        Assert.Fail("Service is not available after 5 retries");
                    }
                }
            } while (true);
        }

        //Sync helpers for DocumentStoreClient
        public static T Read<T>(this DocumentClient client, string resourceIdOrFullName, INameValueCollection headers = null) where T : Resource, new()
        {
            return client.Read<T>(resourceIdOrFullName, out INameValueCollection ignored, headers);
        }

        public static T Read<T>(this DocumentClient client, string resourceIdOrFullName, out INameValueCollection responseHeaders, INameValueCollection headers = null) where T : Resource, new()
        {
            try
            {
                DocumentServiceRequest request;
                if (PathsHelper.IsNameBased(resourceIdOrFullName))
                {
                    request = DocumentServiceRequest.CreateFromName(
                        OperationType.Read,
                        resourceIdOrFullName,
                        TestCommon.ToResourceType(typeof(T)),
                        AuthorizationTokenType.PrimaryMasterKey,
                        headers);
                }
                else
                {
                    request = DocumentServiceRequest.Create(
                        OperationType.Read,
                        resourceIdOrFullName,
                        TestCommon.ToResourceType(typeof(T)),
                        AuthorizationTokenType.PrimaryMasterKey,
                        headers);
                }

                if (request.ResourceType.IsPartitioned())
                {
                    request.Headers[HttpConstants.HttpHeaders.PartitionKey] = PartitionKeyInternal.Empty.ToJsonString();
                }

                DocumentServiceResponse response = client.ReadAsync(request, null).Result;
                responseHeaders = response.Headers;
                return response.GetResource<T>();
            }
            catch (AggregateException aggregatedException)
            {
                throw aggregatedException.InnerException;
            }
        }

        public static T Read<T>(this DocumentClient client, T resource, out INameValueCollection responseHeaders, INameValueCollection headers = null) where T : Resource, new()
        {
            try
            {
                DocumentServiceRequest request;
                if (PathsHelper.IsNameBased(resource.ResourceId))
                {
                    request = DocumentServiceRequest.CreateFromName(
                        OperationType.Read,
                        resource.ResourceId,
                        TestCommon.ToResourceType(typeof(T)),
                        AuthorizationTokenType.PrimaryMasterKey,
                        headers);
                }
                else
                {
                    request = DocumentServiceRequest.Create(
                        OperationType.Read,
                        resource.ResourceId,
                        TestCommon.ToResourceType(typeof(T)),
                        AuthorizationTokenType.PrimaryMasterKey,
                        headers);
                }

                if (request.ResourceType.IsPartitioned())
                {
                    request.Headers[HttpConstants.HttpHeaders.PartitionKey] = resource.Id;//PartitionKeyInternal.Empty.ToJsonString();
                }

                DocumentServiceResponse response = client.ReadAsync(request, null).Result;
                responseHeaders = response.Headers;
                return response.GetResource<T>();
            }
            catch (AggregateException aggregatedException)
            {
                throw aggregatedException.InnerException;
            }
        }

        public static T ReadWithRetry<T>(this DocumentClient client, string resourceIdOrFullName, out INameValueCollection responseHeaders, INameValueCollection headers = null) where T : Resource, new()
        {
            int nMaxRetry = 5;

            do
            {
                try
                {
                    return client.Read<T>(resourceIdOrFullName, out responseHeaders, headers);
                }
                catch (ServiceUnavailableException)
                {
                    if (--nMaxRetry > 0)
                    {
                        Task.Delay(5000); //Wait 5 seconds before retry.
                    }
                    else
                    {
                        Assert.Fail("Service is not available after 5 retries");
                    }
                }
                catch (GoneException)
                {
                    if (--nMaxRetry > 0)
                    {
                        Task.Delay(5000); //Wait 5 seconds before retry.
                    }
                    else
                    {
                        Assert.Fail("Service is not available after 5 retries");
                    }
                }
            } while (true);
        }

        public static T ReadWithRetry<T>(this DocumentClient client, T resource, out INameValueCollection responseHeaders, INameValueCollection headers = null) where T : Resource, new()
        {
            int nMaxRetry = 5;

            do
            {
                try
                {
                    return client.Read<T>(resource, out responseHeaders, headers);
                }
                catch (ServiceUnavailableException)
                {
                    if (--nMaxRetry > 0)
                    {
                        Task.Delay(5000); //Wait 5 seconds before retry.
                    }
                    else
                    {
                        Assert.Fail("Service is not available after 5 retries");
                    }
                }
                catch (GoneException)
                {
                    if (--nMaxRetry > 0)
                    {
                        Task.Delay(5000); //Wait 5 seconds before retry.
                    }
                    else
                    {
                        Assert.Fail("Service is not available after 5 retries");
                    }
                }
            } while (true);
        }

        public static T Create<T>(this DocumentClient client, string resourceIdOrFullName, T resource, INameValueCollection headers = null) where T : Resource, new()
        {
            try
            {
                DocumentServiceRequest request;

                if (PathsHelper.IsNameBased(resourceIdOrFullName))
                {
                    request = DocumentServiceRequest.CreateFromName(
                        OperationType.Create,
                        resource,
                        TestCommon.ToResourceType(typeof(T)),
                        headers,
                        resourceIdOrFullName,
                        AuthorizationTokenType.PrimaryMasterKey);
                }
                else
                {
                    request = DocumentServiceRequest.Create(
                        OperationType.Create,
                        resource,
                        TestCommon.ToResourceType(typeof(T)),
                        AuthorizationTokenType.PrimaryMasterKey,
                        headers,
                        resourceIdOrFullName);
                }

                if (request.ResourceType.IsPartitioned())
                {
                    PartitionKey partitionKey = new PartitionKey("test");
                    request.Headers[HttpConstants.HttpHeaders.PartitionKey] = partitionKey.ToString();
                }

                return client.CreateAsync(request, null).Result.GetResource<T>();
            }
            catch (AggregateException aggregatedException)
            {
                throw aggregatedException.InnerException;
            }
        }

        public static T Upsert<T>(this DocumentClient client, string resourceIdOrFullName, T resource, INameValueCollection headers = null) where T : Resource, new()
        {
            try
            {
                DocumentServiceRequest request;

                if (PathsHelper.IsNameBased(resourceIdOrFullName))
                {
                    request = DocumentServiceRequest.CreateFromName(
                        OperationType.Create,
                        resource,
                        TestCommon.ToResourceType(typeof(T)),
                        headers,
                        resourceIdOrFullName,
                        AuthorizationTokenType.PrimaryMasterKey);
                }
                else
                {
                    request = DocumentServiceRequest.Create(
                        OperationType.Create,
                        resource,
                        TestCommon.ToResourceType(typeof(T)),
                        AuthorizationTokenType.PrimaryMasterKey,
                        headers,
                        resourceIdOrFullName);
                }

                if (request.ResourceType.IsPartitioned())
                {
                    PartitionKey partitionKey = new PartitionKey("test");
                    request.Headers[HttpConstants.HttpHeaders.PartitionKey] = partitionKey.ToString();
                }

                return client.UpsertAsync(request, null).Result.GetResource<T>();
            }
            catch (AggregateException aggregatedException)
            {
                throw aggregatedException.InnerException;
            }
        }


        public static T Update<T>(this DocumentClient client, T resource, INameValueCollection requestHeaders = null) where T : Resource, new()
        {
            try
            {
                string link = resource.SelfLink;
                DocumentServiceRequest request;

                if (link != null)
                {
                    request = DocumentServiceRequest.Create(
                        OperationType.Replace,
                        link,
                        resource,
                        TestCommon.ToResourceType(typeof(T)),
                        AuthorizationTokenType.PrimaryMasterKey,
                        requestHeaders);
                }
                else
                {
                    request = DocumentServiceRequest.Create(
                        OperationType.Replace,
                        resource,
                        TestCommon.ToResourceType(typeof(T)),
                        AuthorizationTokenType.PrimaryMasterKey,
                        requestHeaders);
                }

                if (request.ResourceType.IsPartitioned())
                {
                    PartitionKey partitionKey = new PartitionKey("test");
                    request.Headers[HttpConstants.HttpHeaders.PartitionKey] = partitionKey.ToString();
                }

                DocumentServiceResponse response = client.UpdateAsync(request, null).Result;
                return response.GetResource<T>();
            }
            catch (AggregateException aggregatedException)
            {
                throw aggregatedException.InnerException;
            }
        }

        public static T Replace<T>(this DocumentClient client, T resource) where T : Resource, new()
        {
            return client.Update(resource);
        }

        public static DocumentServiceResponse Delete<T>(this DocumentClient client, string resourceIdOrFullName, INameValueCollection headers = null) where T : Resource, new()
        {
            try
            {
                DocumentServiceRequest request;
                if (PathsHelper.IsNameBased(resourceIdOrFullName))
                {
                    request = DocumentServiceRequest.CreateFromName(
                        OperationType.Delete,
                        resourceIdOrFullName,
                        TestCommon.ToResourceType(typeof(T)),
                        AuthorizationTokenType.PrimaryMasterKey,
                        headers);
                }
                else
                {
                    request = DocumentServiceRequest.Create(
                        OperationType.Delete,
                        resourceIdOrFullName,
                        TestCommon.ToResourceType(typeof(T)),
                        AuthorizationTokenType.PrimaryMasterKey,
                        headers);
                }

                if (request.ResourceType.IsPartitioned())
                {
                    PartitionKey partitionKey = new PartitionKey("test");
                    request.Headers[HttpConstants.HttpHeaders.PartitionKey] = partitionKey.ToString();
                }

                return client.DeleteAsync(request, null).Result;
            }
            catch (AggregateException aggregatedException)
            {
                if (aggregatedException.InnerException is NotFoundException)
                {
                    return new DocumentServiceResponse(null, null, HttpStatusCode.NotFound);
                }
                else
                {
                    throw aggregatedException.InnerException;
                }
            }
        }

        public static DocumentFeedResponse<T> ReadFeed<T>(
            this DocumentClient client,
            string resourceIdOrFullName,
            INameValueCollection headers = null) where T : Resource, new()
        {
            return client.ReadFeed<T>(resourceIdOrFullName, out INameValueCollection ignored, headers);
        }

        public static DocumentFeedResponse<T> ReadFeed<T>(
            this DocumentClient client,
            string resourceIdOrFullName,
            out INameValueCollection responseHeaders,
            INameValueCollection headers = null) where T : Resource, new()
        {
            try
            {
                DocumentServiceRequest request;
                if (PathsHelper.IsNameBased(resourceIdOrFullName))
                {
                    request = DocumentServiceRequest.CreateFromName(
                        OperationType.ReadFeed,
                        resourceIdOrFullName,
                        TestCommon.ToResourceType(typeof(T)),
                        AuthorizationTokenType.PrimaryMasterKey,
                        headers);
                }
                else
                {
                    request = DocumentServiceRequest.Create(
                        OperationType.ReadFeed,
                        resourceIdOrFullName,
                        TestCommon.ToResourceType(typeof(T)),
                        AuthorizationTokenType.PrimaryMasterKey,
                        headers);
                }

                if (request.ResourceType.IsPartitioned())
                {
                    ClientCollectionCache collectionCache = client.GetCollectionCacheAsync(NoOpTrace.Singleton).Result;
                    ContainerProperties collection = collectionCache.ResolveCollectionAsync(request, CancellationToken.None).Result;
                    IRoutingMapProvider routingMapProvider = client.GetPartitionKeyRangeCacheAsync().Result;
                    IReadOnlyList<PartitionKeyRange> overlappingRanges = routingMapProvider.TryGetOverlappingRangesAsync(
                        collection.ResourceId,
                        Range<string>.GetPointRange(PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey),
                        NoOpTrace.Singleton).Result;

                    request.RouteTo(new PartitionKeyRangeIdentity(collection.ResourceId, overlappingRanges.Single().Id));
                }

                DocumentServiceResponse result = client.ReadFeedAsync(request, null).Result;
                responseHeaders = result.Headers;
                FeedResource<T> feedResource = result.GetResource<FeedResource<T>>();

                return new DocumentFeedResponse<T>(feedResource,
                    feedResource.Count,
                    result.Headers);
            }
            catch (AggregateException aggregatedException)
            {
                throw aggregatedException.InnerException;
            }
        }

        public static void AssertException(DocumentClientException clientException, params HttpStatusCode[] statusCodes)
        {
            Assert.IsNotNull(clientException.Error, "Exception.Error is null");
            Assert.IsNotNull(clientException.ActivityId, "Exception.ActivityId is null");

            if (statusCodes.Length == 1)
            {
                Assert.AreEqual(clientException.Error.Code, statusCodes[0].ToString(), string.Format(CultureInfo.InvariantCulture, "Error code dont match, details {0}", clientException.ToString()));
            }
            else
            {
                bool matched = false;

                foreach (HttpStatusCode statusCode in statusCodes)
                {
                    if (statusCode.ToString() == clientException.Error.Code)
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    Assert.Fail("Exception code {0}, didnt match any of the expected exception codes {1}, details {2}",
                        clientException.Error.Code,
                        string.Join(",", statusCodes),
                        clientException.Message);
                }
            }
        }

        public static async Task<string> CreateRandomBinaryFileInTmpLocation(long fileSizeInBytes)
        {
            string filePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString() + ".bin");
            await TestCommon.CreateFileWithRandomBytesAsync(filePath, fileSizeInBytes);
            return filePath;
        }

        public static async Task CreateFileWithRandomBytesAsync(string filePath,
            long fileSizeInBytes)
        {
            Logger.LogLine("Creating file at {0} with fileSizeInBytes {1}", filePath, fileSizeInBytes);

            long remaingBytesToWrite = fileSizeInBytes;
            long bufferSize = 1048576;

            Random random = new Random();

            using (FileStream fileStream = File.Open(filePath, FileMode.CreateNew, FileAccess.Write))
            {
                while (remaingBytesToWrite > 0)
                {
                    byte[] buffer = new byte[Math.Min(bufferSize, remaingBytesToWrite)];
                    random.NextBytes(buffer);


                    await fileStream.WriteAsync(buffer, 0, buffer.Length);

                    remaingBytesToWrite -= bufferSize;
                }
            }

        }

        #region Environment Configuration Helpers
        private static ReplicationPolicy GetServerReplicationPolicy()
        {
            return new ReplicationPolicy
            {
                MaxReplicaSetSize = 3,
                MinReplicaSetSize = 2,
                AsyncReplication = true
            };
        }

        private static AccountConsistency GetServerConsistencyPolicy()
        {
            AccountConsistency consistencyPolicy = new AccountConsistency
            {
                DefaultConsistencyLevel = (Cosmos.ConsistencyLevel)ConsistencyLevel.Strong
            };

            return consistencyPolicy;
        }

        #endregion

        public static string GetCollectionOfferDetails(DocumentClient client, string collectionResourceId)
        {
            using (ActivityScope scope = new ActivityScope(Guid.NewGuid()))
            {
                Offer offer = client.CreateOfferQuery().Where(o => o.OfferResourceId == collectionResourceId).AsEnumerable().FirstOrDefault(); ;
                OfferV2 offerV2 = null;
                try
                {
                    offerV2 = (OfferV2)offer;
                }
                catch
                {
                    ;
                }

                if (offerV2 != null)
                {
                    return offerV2.OfferType;
                }

                if (offer != null)
                {
                    return offer.OfferType;
                }

                return null;
            }
        }

        public static void SetIntConfigurationProperty(string propertyName, int value)
        {
            // There is no federation configuration in the Public Emulator
        }

        public static async Task SetIntConfigurationPropertyAsync(string propertyName, int value)
        {
            // There is no federation configuration in the Public Emulator

            await Task.FromResult<bool>(default(bool));
        }

        public static void SetStringConfigurationProperty(string propertyName, string value)
        {
            // There is no federation configuration in the Public Emulator
        }

        public static void SetBooleanConfigurationProperty(string propertyName, bool value)
        {
            // There is no federation configuration in the Public Emulator
        }

        public static void SetDoubleConfigurationProperty(string propertyName, double value)
        {
            // There is no federation configuration in the Public Emulator
        }

        public static void SetFederationWideConfigurationProperty<T>(string propertyName, T value)
        {
            // There is no federation configuration in the Public Emulator
        }

        public static void WaitForConfigRefresh()
        {
            // There is no federation configuration in the Public Emulator
        }

        public static Task WaitForConfigRefreshAsync()
        {
            // There is no federation configuration in the Public Emulator
            return Task.Delay(0);
        }

        public static void WaitForBackendConfigRefresh()
        {
            // There is no federation configuration in the Public Emulator
        }

        public static async Task DeleteAllDatabasesAsync()
        {
            CosmosClient client = TestCommon.CreateCosmosClient(false);
            int numberOfRetry = 3;
            CosmosException finalException = null;
            do
            {
                TimeSpan retryAfter = TimeSpan.Zero;
                try
                {
                    await TestCommon.DeleteAllDatabasesAsyncWorker(client);

                    // Offer read feed not supported in v3 SDK
                    //DoucmentFeedResponse<Offer> offerResponse = client.ReadOffersFeedAsync().Result;
                    //if (offerResponse.Count != 0)
                    //{
                    //    // Number of offers should have been 0 after deleting all the databases
                    //    string error = string.Format("All offers not deleted after DeleteAllDatabases. Number of offer remaining {0}",
                    //        offerResponse.Count);
                    //    Logger.LogLine(error);
                    //    Logger.LogLine("Remaining offers are: ");
                    //    foreach (Offer offer in offerResponse)
                    //    {
                    //        Logger.LogLine("Offer resourceId: {0}, offer resourceLink: {1}", offer.OfferResourceId, offer.ResourceLink);
                    //    }

                    //    //Assert.Fail(error);
                    //}

                    return;
                }
                catch (CosmosException clientException)
                {
                    finalException = clientException;
                    if (clientException.StatusCode == (HttpStatusCode)429)
                    {
                        Logger.LogLine("Received request rate too large. ActivityId: {0}, {1}",
                                       clientException.ActivityId,
                                       clientException);

                        retryAfter = TimeSpan.FromSeconds(1);
                    }
                    else if (clientException.StatusCode == HttpStatusCode.RequestTimeout)
                    {
                        Logger.LogLine("Received timeout exception while cleaning the store. ActivityId: {0}, {1}",
                                       clientException.ActivityId,
                                       clientException);

                        retryAfter = TimeSpan.FromSeconds(1);
                    }
                    else if (clientException.StatusCode == HttpStatusCode.NotFound)
                    {
                        // Previous request (that timed-out) might have been committed to the store.
                        // In such cases, ignore the not-found exception.
                        Logger.LogLine("Received not-found exception while cleaning the store. ActivityId: {0}, {1}",
                                       clientException.ActivityId,
                                       clientException);
                    }
                    else
                    {
                        Logger.LogLine("Unexpected exception. ActivityId: {0}, {1}", clientException.ActivityId, clientException);
                    }
                    if (numberOfRetry == 1) throw;
                }

                if (retryAfter > TimeSpan.Zero)
                {
                    await Task.Delay(retryAfter);
                }

            } while (numberOfRetry-- > 0);
        }

        public static async Task DeleteAllDatabasesAsyncWorker(CosmosClient client)
        {
            IList<Cosmos.Database> databases = new List<Cosmos.Database>();

            FeedIterator<DatabaseProperties> resultSetIterator = client.GetDatabaseQueryIterator<DatabaseProperties>(
                queryDefinition: null,
                continuationToken: null, 
                requestOptions: new QueryRequestOptions() { MaxItemCount = 10 });

            List<Task> deleteTasks = new List<Task>(10); //Delete in chunks of 10
            int totalCount = 0;
            while (resultSetIterator.HasMoreResults)
            {
                foreach (DatabaseProperties database in await resultSetIterator.ReadNextAsync())
                {
                    deleteTasks.Add(TestCommon.DeleteDatabaseAsync(client, client.GetDatabase(database.Id)));
                    totalCount++;
                }

                await Task.WhenAll(deleteTasks);
                deleteTasks.Clear();
            }

            Logger.LogLine("Number of database to delete {0}", totalCount);
        }

        public static async Task DeleteDatabaseAsync(CosmosClient client, Cosmos.Database database)
        {
            await TestCommon.DeleteDatabaseCollectionAsync(client, database);

            await TestCommon.AsyncRetryRateLimiting(() => database.DeleteAsync());
        }

        public static async Task DeleteDatabaseCollectionAsync(CosmosClient client, Cosmos.Database database)
        {
            //Delete them in chunks of 10.
            FeedIterator<ContainerProperties> resultSetIterator = database.GetContainerQueryIterator<ContainerProperties>(requestOptions: new QueryRequestOptions() { MaxItemCount = 10 });
            while (resultSetIterator.HasMoreResults)
            {
                List<Task> deleteCollectionTasks = new List<Task>(10);
                foreach (ContainerProperties container in await resultSetIterator.ReadNextAsync())
                {
                    Logger.LogLine("Deleting Collection with following info Id:{0}, database Id: {1}", container.Id, database.Id);
                    deleteCollectionTasks.Add(TestCommon.AsyncRetryRateLimiting(() => database.GetContainer(container.Id).DeleteContainerAsync()));
                }

                await Task.WhenAll(deleteCollectionTasks);
                deleteCollectionTasks.Clear();
            }
        }

        public static async Task<T> AsyncRetryRateLimiting<T>(Func<Task<T>> work)
        {
            while (true)
            {
                TimeSpan retryAfter = TimeSpan.FromSeconds(1);

                try
                {
                    return await work();
                }
                catch (DocumentClientException e)
                {
                    if ((int)e.StatusCode == (int)StatusCodes.TooManyRequests)
                    {
                        if (e.RetryAfter.TotalMilliseconds > 0)
                        {
                            retryAfter = new[] { e.RetryAfter, TimeSpan.FromSeconds(1) }.Max();
                        }
                    }
                    else
                    {
                        throw;
                    }
                }

                await Task.Delay(retryAfter);
            }
        }

        public static T RetryRateLimiting<T>(Func<T> work)
        {
            while (true)
            {
                TimeSpan retryAfter = TimeSpan.FromSeconds(1);

                try
                {
                    return work();
                }
                catch (Exception e)
                {
                    while (e is AggregateException)
                    {
                        e = e.InnerException;
                    }

                    DocumentClientException clientException = e as DocumentClientException;
                    if (clientException == null)
                    {
                        throw;
                    }

                    if ((int)clientException.StatusCode == (int)StatusCodes.TooManyRequests)
                    {
                        retryAfter = new[] { clientException.RetryAfter, TimeSpan.FromSeconds(1) }.Max();
                    }
                    else
                    {
                        throw;
                    }
                }

                Task.Delay(retryAfter);
            }
        }

        /// <summary>
        /// Timed wait while the backend operation commit happens.
        /// </summary>
        public static void WaitWhileBackendOperationCommits()
        {
            Task.Delay(TimeSpan.FromSeconds(TestCommon.TimeToWaitForOperationCommitInSec)).Wait();
        }

        internal static ResourceType ToResourceType(Type type)
        {
            if (type == typeof(Conflict))
            {
                return ResourceType.Conflict;
            }
            else if (type == typeof(Database) || type == typeof(DatabaseProperties))
            {
                return ResourceType.Database;
            }
            else if (type == typeof(DocumentCollection) || type == typeof(ContainerProperties))
            {
                return ResourceType.Collection;
            }
            else if (type == typeof(Document) || typeof(Document).IsAssignableFrom(type))
            {
                return ResourceType.Document;
            }
            else if (type == typeof(Permission))
            {
                return ResourceType.Permission;
            }
            else if (type == typeof(StoredProcedure) || type == typeof(StoredProcedureProperties))
            {
                return ResourceType.StoredProcedure;
            }
            else if (type == typeof(Trigger) || type == typeof(TriggerProperties))
            {
                return ResourceType.Trigger;
            }
            else if (type == typeof(UserDefinedFunction) || type == typeof(UserDefinedFunctionProperties))
            {
                return ResourceType.UserDefinedFunction;
            }
            else if (type == typeof(User))
            {
                return ResourceType.User;
            }
            else if (type == typeof(Attachment))
            {
                return ResourceType.Attachment;
            }
            else if (type == typeof(Offer) || type == typeof(Offer))
            {
                return ResourceType.Offer;
            }
            else if (type == typeof(Schema))
            {
                return ResourceType.Schema;
            }
            else if (type == typeof(PartitionKeyRange))
            {
                return ResourceType.PartitionKeyRange;
            }
            else
            {
                string errorMessage = string.Format(CultureInfo.CurrentUICulture, RMResources.UnknownResourceType, type.Name);
                throw new ArgumentException(errorMessage);
            }
        }

        public static async Task<DocumentCollection> CreateCollectionAsync(
            DocumentClient client,
            Uri dbUri,
            DocumentCollection col,
            RequestOptions requestOptions = null)
        {
            return await client.CreateDocumentCollectionAsync(
                dbUri,
                col,
                requestOptions);
        }

        public static async Task<DocumentCollection> CreateCollectionAsync(
            DocumentClient client,
            string databaseSelfLink,
            DocumentCollection col,
            RequestOptions requestOptions = null)
        {
            return await client.CreateDocumentCollectionAsync(
                databaseSelfLink,
                col,
                requestOptions);
        }

        public static async Task<DocumentCollection> CreateCollectionAsync(
            DocumentClient client,
            Database db,
            DocumentCollection col,
            RequestOptions requestOptions = null)
        {
            return await client.CreateDocumentCollectionAsync(
                db.SelfLink,
                col,
                requestOptions);
        }

        public static async Task<ResourceResponse<DocumentCollection>> CreateDocumentCollectionResourceAsync(
            DocumentClient client,
            Database db,
            DocumentCollection col,
            RequestOptions requestOptions = null)
        {
            return await client.CreateDocumentCollectionAsync(
                db.SelfLink,
                col,
                requestOptions);
        }

        public static async Task<ResourceResponse<DocumentCollection>> CreateDocumentCollectionResourceAsync(
            DocumentClient client,
            string dbSelflink,
            DocumentCollection col,
            RequestOptions requestOptions = null)
        {
            return await client.CreateDocumentCollectionAsync(
                dbSelflink,
                col,
                requestOptions);
        }

        public static ISessionToken CreateSessionToken(ISessionToken from, long globalLSN)
        {
            // Creates session token with specified GlobalLSN
            if (from is SimpleSessionToken)
            {
                return new SimpleSessionToken(globalLSN);
            }
            else if (from is VectorSessionToken)
            {
                return new VectorSessionToken(from as VectorSessionToken, globalLSN);
            }
            else
            {
                throw new ArgumentException();
            }
        }

        private class DisposableList : IDisposable
        {
            private readonly List<IDisposable> disposableList;

            internal DisposableList()
            {
                this.disposableList = new List<IDisposable>();
            }

            internal void Add(IDisposable disposable)
            {
                this.disposableList.Add(disposable);
            }

            public void Dispose()
            {
                foreach (IDisposable disposable in this.disposableList)
                {
                    disposable.Dispose();
                }

                TestCommon.WaitForConfigRefresh();
            }
        }

        private class RestoreNamingConfigurations : IDisposable
        {
            private readonly Uri parentName;
            private readonly List<KeyValuePair<string, string>> keyValues;

            internal RestoreNamingConfigurations(Uri parentName, List<KeyValuePair<string, string>> keyValues)
            {
                this.parentName = parentName;
                this.keyValues = keyValues;
            }

            public void Dispose()
            {
                TestCommon.WaitForConfigRefresh();
            }
        }
    }
}