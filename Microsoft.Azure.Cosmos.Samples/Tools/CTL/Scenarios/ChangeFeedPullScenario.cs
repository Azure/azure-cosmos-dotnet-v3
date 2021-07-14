//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Net;
    using System.Diagnostics;
    using App.Metrics;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;

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
            Stopwatch stopWatch = Stopwatch.StartNew();

            while (stopWatch.Elapsed <= config.RunningTimeDurationAsTimespan)
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
                        FeedResponse<Dictionary<string, string>> response = await changeFeedPull.ReadNextAsync();
                        documentTotal += response.Count;
                        continuation = response.ContinuationToken;
                        if (response.StatusCode == HttpStatusCode.NotModified)
                        {
                            break;
                        }
                    }

                    if (config.PreCreatedDocuments == documentTotal)
                    {
                        logger.LogInformation($"Success: The number of new documents match the number of pre-created documents: {config.PreCreatedDocuments}");
                    }
                    else
                    {
                        logger.LogError($"The prepopulated documents and the new documents don't match.  Preconfigured Docs = {config.PreCreatedDocuments}, New Documents = {documentTotal}");
                        logger.LogError(continuation);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failure while looping through new documents");
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
    }
}
