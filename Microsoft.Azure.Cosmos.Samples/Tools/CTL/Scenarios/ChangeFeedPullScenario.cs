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
                IReadOnlyDictionary<string, IReadOnlyList<Dictionary<string, string>>> insertedDocuments = await Utils.PopulateDocumentsAsync(config, logger, new List<Container>() { cosmosClient.GetContainer(config.Database, config.Collection) });
                this.initializationResult.InsertedDocuments = insertedDocuments[config.Collection].Count;
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

                    if (config.PreCreatedDocuments > 0)
                    {
                        if (this.initializationResult.InsertedDocuments == documentTotal)
                        {
                            logger.LogInformation($"Success: The number of new documents match the number of pre-created documents: {this.initializationResult.InsertedDocuments}");
                        }
                        else
                        {
                            logger.LogError($"The prepopulated documents and the change feed documents don't match.  Preconfigured Docs = {this.initializationResult.InsertedDocuments}, Change feed Documents = {documentTotal}.{Environment.NewLine}{continuation}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failure while looping through change feed documents");
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
            public int InsertedDocuments;
        }
    }
}
