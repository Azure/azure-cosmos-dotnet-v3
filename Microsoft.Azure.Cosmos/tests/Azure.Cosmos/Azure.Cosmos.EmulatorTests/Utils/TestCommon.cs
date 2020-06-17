//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
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
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos;
    using Azure.Cosmos.Serialization;
    using Microsoft.Azure.Cosmos.Routing;
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
        public static Lazy<CosmosSerializer> Serializer = new Lazy<CosmosSerializer>(() =>
        {
            // Adding converters to support V2 types in existing tests
            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions();
            jsonSerializerOptions.Converters.Add(new TextJsonJTokenConverter());
            jsonSerializerOptions.Converters.Add(new TextJsonDocumentConverter());
            jsonSerializerOptions.Converters.Add(new TextJsonCosmosElementConverter());
            jsonSerializerOptions.Converters.Add(new TextJsonCosmosElementListConverter());
            jsonSerializerOptions.Converters.Add(new TextJsonJObjectConverter());
            jsonSerializerOptions.Converters.Add(new TextJsonJTokenListConverter());
            CosmosTextJsonSerializer.InitializeRESTConverters(jsonSerializerOptions);
            CosmosTextJsonSerializer.InitializeDataContractConverters(jsonSerializerOptions);
            TestCommon.AddSpatialConverters(jsonSerializerOptions);
            return new CosmosTextJsonSerializer(jsonSerializerOptions);
        });

        static TestCommon()
        {
            TestCommon.serverStalenessIntervalInSeconds = int.Parse(ConfigurationManager.AppSettings["ServerStalenessIntervalInSeconds"], CultureInfo.InvariantCulture);
            TestCommon.masterStalenessIntervalInSeconds = int.Parse(ConfigurationManager.AppSettings["MasterStalenessIntervalInSeconds"], CultureInfo.InvariantCulture);
        }

        internal static (string endpoint, string authKey) GetAccountInfo()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];

            return (endpoint, authKey);
        }

        internal static CosmosClientBuilder GetDefaultConfiguration()
        {
            (string endpoint, string authKey) accountInfo = TestCommon.GetAccountInfo();

            return new CosmosClientBuilder(accountEndpoint: accountInfo.endpoint, authKeyOrResourceToken: accountInfo.authKey);
        }

        internal static CosmosClient CreateCosmosClient(bool gatewayMode)
        {
            if (!gatewayMode)
            {
                return TestCommon.CreateCosmosClient();
            }

            return TestCommon.CreateCosmosClient((builder) => builder.WithConnectionModeGateway());
        }

        internal static CosmosClient CreateCosmosClient(CosmosClientOptions options, string resourceToken = null)
        {
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            return new CosmosClient(endpoint, resourceToken ?? authKey, options);
        }

        internal static CosmosClient CreateCosmosClient(Action<CosmosClientBuilder> customizeClientBuilder = null)
        {
            CosmosClientBuilder cosmosClientBuilder = GetDefaultConfiguration();
            cosmosClientBuilder.WithCustomSerializer(TestCommon.Serializer.Value);
            if (customizeClientBuilder != null)
            {
                customizeClientBuilder(cosmosClientBuilder);
            }

            return cosmosClientBuilder.Build();
        }

        internal static async Task<Response> GetFirstResponse(this IAsyncEnumerable<Response> asyncEnumerable)
        {
            await foreach(Response response in asyncEnumerable)
            {
                return response;
            }

            return null;
        }

        public static async Task DeleteAllDatabasesAsync()
        {
            CosmosClient client = TestCommon.CreateCosmosClient();
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
            IList<Cosmos.CosmosDatabase> databases = new List<Cosmos.CosmosDatabase>();

            AsyncPageable<CosmosDatabaseProperties> resultSetIterator = client.GetDatabaseQueryResultsAsync<CosmosDatabaseProperties>(
                queryDefinition: null,
                continuationToken: null,
                requestOptions: new QueryRequestOptions() { MaxItemCount = 10 });

            List<Task> deleteTasks = new List<Task>(10); //Delete in chunks of 10
            int totalCount = 0;
            await foreach (CosmosDatabaseProperties database in resultSetIterator)
            {
                deleteTasks.Add(TestCommon.DeleteDatabaseAsync(client, client.GetDatabase(database.Id)));
                totalCount++;
            }

            await Task.WhenAll(deleteTasks);
            deleteTasks.Clear();

            Logger.LogLine("Number of database to delete {0}", totalCount);
        }

        public static async Task DeleteDatabaseAsync(CosmosClient client, Cosmos.CosmosDatabase database)
        {
            await TestCommon.DeleteDatabaseCollectionAsync(client, database);

            await TestCommon.AsyncRetryRateLimiting(() => database.DeleteAsync());
        }

        public static async Task DeleteDatabaseCollectionAsync(CosmosClient client, Cosmos.CosmosDatabase database)
        {
            //Delete them in chunks of 10.
            AsyncPageable<CosmosContainerProperties> resultSetIterator = database.GetContainerQueryResultsAsync<CosmosContainerProperties>(requestOptions: new QueryRequestOptions() { MaxItemCount = 10 });
            List<Task> deleteCollectionTasks = new List<Task>(10);
            await foreach (CosmosContainerProperties container in resultSetIterator)
            {
                Logger.LogLine("Deleting Collection with following info Id:{0}, database Id: {1}", container.Id, database.Id);
                deleteCollectionTasks.Add(TestCommon.AsyncRetryRateLimiting(() => database.GetContainer(container.Id).DeleteContainerAsync()));
            }

            await Task.WhenAll(deleteCollectionTasks);
            deleteCollectionTasks.Clear();
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

        public static void AddSpatialConverters(JsonSerializerOptions jsonSerializerOptions)
        {
            jsonSerializerOptions.Converters.Add(new TextJsonCrsConverterFactory());
            jsonSerializerOptions.Converters.Add(new TextJsonGeometryConverterFactory());
            jsonSerializerOptions.Converters.Add(new TextJsonGeometryParamsJsonConverter());
            jsonSerializerOptions.Converters.Add(new TextJsonBoundingBoxConverter());
            jsonSerializerOptions.Converters.Add(new TextJsonGeometryValidationResultConverter());
            jsonSerializerOptions.Converters.Add(new TextJsonLinearRingConverter());
            jsonSerializerOptions.Converters.Add(new TextJsonLineStringCoordinatesConverter());
            jsonSerializerOptions.Converters.Add(new TextJsonPolygonCoordinatesConverter());
            jsonSerializerOptions.Converters.Add(new TextJsonPositionConverter());
        }
    }
}