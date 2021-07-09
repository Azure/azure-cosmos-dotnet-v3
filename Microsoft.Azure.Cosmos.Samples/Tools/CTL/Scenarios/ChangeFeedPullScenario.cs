//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Net;
    using System.Diagnostics;

    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Gauge;

    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json;
    

    internal class ChangeFeedPullScenario : ICTLScenario
    {
        private InitializationResult initializationResult;

        public async Task InitializeAsync(
            CTLConfig config,
            CosmosClient cosmosClient,
            ILogger logger)
        {
            this.initializationResult = await CreateDatabaseAndContainerAsync(config, cosmosClient);
            if (this.initializationResult.CreatedDatabase)
            {
                logger.LogInformation("Created database for execution");
            }

            if (this.initializationResult.CreatedContainer)
            {
                logger.LogInformation("Created collection for execution");
            }

            if (config.PreCreatedDocuments > 0)
            {
                logger.LogInformation("Pre-populating {0} documents", config.PreCreatedDocuments);
                await Utils.PopulateDocumentsAsync(config, logger, new List<Container>() { cosmosClient.GetContainer(config.Database, config.Collection) });
            }
        }

        public async Task RunAsync(
            CTLConfig config,
            CosmosClient cosmosClient,
            ILogger logger,
            IMetrics metrics,
            string loggingContextIdentifier,
            CancellationToken cancellationToken)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            //Repeat the loop of creating a new Change Feed Iterator and all the steps until the run-time is completed.
            while(stopWatch.Elapsed <= config.RunningTimeDurationAsTimespan)
            {
                int documentTotal = 0;
                string continuation = null;
                Container container = cosmosClient.GetContainer(config.Database, config.Collection);
                FeedIterator<Dictionary<string, string>> changeFeedPull = container.GetChangeFeedIterator<Dictionary<string, string>>
                                                                                (ChangeFeedStartFrom.Beginning(), ChangeFeedMode.Incremental);
                try 
                {
                    while (changeFeedPull.HasMoreResults)
                    {
                        //Call ReadNextAsync on the iterator
                        FeedResponse<Dictionary<string, string>> response = await changeFeedPull.ReadNextAsync();
                        //If there are new documents in the response, count them in a variable
                        documentTotal += response.Count;
                        continuation = response.ContinuationToken;
                        //If there are no new changes, stop/break out of the iterator.
                        if (response.StatusCode == HttpStatusCode.NotModified)
                        {
                            break;
                        }                   
                    }
                    if(config.PreCreatedDocuments == documentTotal) 
                    {
                        logger.LogInformation("Suceess: The number of new documents match the number of pre-created documents");
                    }
                    //Compare the detected changes vs the prepopulated ones and if they don't match, log a warning with a text showing the 2 values and also log the 
                    //value of the last captured Continuation.
                    else
                    {
                        logger.LogError("The prepopulated documents and the new documents don't match.  Preconfigured Docs =" + config.PreCreatedDocuments + ", New Documents = "+ documentTotal);
                        logger.LogError(continuation);
                    }
                }
                catch (Exception ex) 
                {
                    logger.LogError(ex, "Failure while looping through new documents");
                    //If there are any Exceptions in the loop (try/catch), log the errors 
                }
            }
            stopWatch.Stop();
        }

        private static async Task<InitializationResult> CreateDatabaseAndContainerAsync(
            CTLConfig config,
            CosmosClient cosmosClient)
        {
            InitializationResult result = new InitializationResult()
            {
                CreatedDatabase = false,
                CreatedContainer = false
            };

            Database database;
            try
            {
                database = await cosmosClient.GetDatabase(config.Database).ReadAsync();
            }
            catch (CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseAsync(config.Database, config.Throughput);
                result.CreatedDatabase = true;
                database = databaseResponse.Database;
            }

            Container container;
            try
            {
                container = await database.GetContainer(config.Collection).ReadContainerAsync();
            }
            catch (CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await database.CreateContainerAsync(config.Collection, $"/{config.CollectionPartitionKey}");
                result.CreatedContainer = true;
            }

            return result;
        }

        private struct InitializationResult
        {
            public bool CreatedDatabase;
            public bool CreatedContainer;
        }

        private class SimpleItem
        {
            [JsonProperty("id")]
            public string Id { get; set; }
        }

        internal class LeaseSchema
        {
            [JsonProperty("id")]
            public string LeaseId { get; set; }

            [JsonProperty("LeaseToken")]
            public string LeaseToken { get; set; }

            [JsonProperty("FeedRange")]
            public Range FeedRange { get; set; }

        }

        internal class Range
        {
            [JsonProperty("Range")]
            public FeedRange EffectiveRange { get; set; }
        }

        internal class FeedRange
        {
            [JsonProperty("min")]
            public string Min { get; set; }

            [JsonProperty("max")]
            public string Max { get; set; }
        }
    }
}
