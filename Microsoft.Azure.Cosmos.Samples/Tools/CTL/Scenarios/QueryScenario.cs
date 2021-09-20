//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using App.Metrics;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;
    using System.Text;
    using App.Metrics.Gauge;

    internal class QueryScenario : ICTLScenario
    {
        private Utils.InitializationResult initializationResult;

        public async Task InitializeAsync(
            CTLConfig config,
            CosmosClient cosmosClient,
            ILogger logger)
        {
            this.initializationResult = await Utils.CreateDatabaseAndContainerAsync(config, cosmosClient);

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
            try
            {
                await Task.WhenAll(
                        QueryScenario.ExecuteQueryAndGatherResultsAsync(
                            config,
                            cosmosClient,
                            logger,
                            metrics,
                            loggingContextIdentifier,
                            cancellationToken,
                            queryText: "select * from c",
                            queryName: "Star",
                            expectedResults: config.PreCreatedDocuments > 0 ? this.initializationResult.InsertedDocuments : 0),
                        QueryScenario.ExecuteQueryAndGatherResultsAsync(
                            config,
                            cosmosClient,
                            logger,
                            metrics,
                            loggingContextIdentifier,
                            cancellationToken,
                            queryText: "select * from c order by c.id",
                            queryName: "OrderBy",
                            expectedResults: config.PreCreatedDocuments > 0 ? this.initializationResult.InsertedDocuments : 0),
                        QueryScenario.ExecuteQueryAndGatherResultsAsync(
                            config,
                            cosmosClient,
                            logger,
                            metrics,
                            loggingContextIdentifier,
                            cancellationToken,
                            queryText: "select count(1) from c",
                            queryName: "Aggregates",
                            expectedResults: 1),
                        QueryScenario.ExecuteQueryWithContinuationAndGatherResultsAsync(
                            config,
                            cosmosClient,
                            logger,
                            metrics,
                            loggingContextIdentifier,
                            cancellationToken,
                            queryText: "select * from c",
                            queryName: "Star",
                            expectedResults: config.PreCreatedDocuments > 0 ? this.initializationResult.InsertedDocuments : 0),
                        QueryScenario.ExecuteQueryWithContinuationAndGatherResultsAsync(
                            config,
                            cosmosClient,
                            logger,
                            metrics,
                            loggingContextIdentifier,
                            cancellationToken,
                            queryText: "select * from c order by c.id",
                            queryName: "OrderBy",
                            expectedResults: config.PreCreatedDocuments > 0 ? this.initializationResult.InsertedDocuments : 0),
                        QueryScenario.ExecuteQueryWithContinuationAndGatherResultsAsync(
                            config,
                            cosmosClient,
                            logger,
                            metrics,
                            loggingContextIdentifier,
                            cancellationToken,
                            queryText: "select count(1) from c",
                            queryName: "Aggregates",
                            expectedResults: 1)
                        );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure during Query scenario");
            }
            finally
            {
                if (this.initializationResult.CreatedContainer)
                {
                    await cosmosClient.GetContainer(config.Database, config.Collection).DeleteContainerStreamAsync();
                }

                if (this.initializationResult.CreatedDatabase)
                {
                    await cosmosClient.GetDatabase(config.Database).DeleteStreamAsync();
                }
            }
        }

        /// <summary>
        /// Executes the query and does paging using continuations.
        /// </summary>
        private async static Task ExecuteQueryWithContinuationAndGatherResultsAsync(CTLConfig config,
            CosmosClient cosmosClient,
            ILogger logger,
            IMetrics metrics,
            string loggingContextIdentifier,
            CancellationToken cancellationToken,
            string queryText,
            string queryName,
            long expectedResults)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();

            GaugeOptions documentGauge = new GaugeOptions
            {
                Name = $"#{queryName} Query with CT received documents",
                MeasurementUnit = Unit.Items,
                Context = loggingContextIdentifier
            };

            Container container = cosmosClient.GetContainer(config.Database, config.Collection);
            while (stopWatch.Elapsed <= config.RunningTimeDurationAsTimespan
                && !cancellationToken.IsCancellationRequested)
            {
                // To really debug what happened on the query, having a list of all continuations would be useful.
                List<string> allContinuations = new List<string>();
                long documentTotal = 0;
                string continuation;
                FeedIterator<Dictionary<string, string>> query = container.GetItemQueryIterator<Dictionary<string, string>>(queryText);
                try
                {
                    while (query.HasMoreResults)
                    {
                        FeedResponse<Dictionary<string, string>> response = await query.ReadNextAsync();
                        documentTotal += response.Count;
                        continuation = response.ContinuationToken;
                        allContinuations.Add(continuation);
                        if (continuation != null)
                        {
                            // Use continuation to paginate on the query instead of draining just the initial query
                            // This validates that we can indeed move forward with the continuation
                            query.Dispose();
                            query = container.GetItemQueryIterator<Dictionary<string, string>>(queryText, continuation);
                        }
                    }

                    query.Dispose();

                    metrics.Measure.Gauge.SetValue(documentGauge, documentTotal);

                    if (expectedResults > 0 && expectedResults != documentTotal)
                    {
                        StringBuilder errorDetail = new StringBuilder();
                        errorDetail.AppendLine($"{queryName} Query with CT expected to read {expectedResults} but got {documentTotal}");
                        foreach (string c in allContinuations)
                        {
                            errorDetail.AppendLine($"Continuation: {c}");
                        }

                        logger.LogError(errorDetail.ToString());
                    }
                }
                catch (CosmosException ce) when (ce.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    //Logging 429s is not relevant
                }
                catch (Exception ex)
                {
                    metrics.Measure.Gauge.SetValue(documentGauge, documentTotal);

                    StringBuilder errorDetail = new StringBuilder();
                    errorDetail.AppendLine($"{queryName} Query with CT failure while looping through query.");
                    foreach (string c in allContinuations)
                    {
                        errorDetail.AppendLine($"Continuation: {c}");
                    }

                    logger.LogError(ex, errorDetail.ToString());
                }
            }

            stopWatch.Stop();
        }

        /// <summary>
        /// Executes the query and drains all results
        /// </summary>
        private async static Task ExecuteQueryAndGatherResultsAsync(CTLConfig config,
            CosmosClient cosmosClient,
            ILogger logger,
            IMetrics metrics,
            string loggingContextIdentifier,
            CancellationToken cancellationToken,
            string queryText,
            string queryName,
            long expectedResults)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();

            GaugeOptions documentGauge = new GaugeOptions
            {
                Name = $"#{queryName} Query received documents",
                MeasurementUnit = Unit.Items,
                Context = loggingContextIdentifier
            };

            Container container = cosmosClient.GetContainer(config.Database, config.Collection);
            while (stopWatch.Elapsed <= config.RunningTimeDurationAsTimespan
                && !cancellationToken.IsCancellationRequested)
            {
                long documentTotal = 0;
                string continuation = null;
                using FeedIterator<Dictionary<string, string>> query = container.GetItemQueryIterator<Dictionary<string, string>>(queryText);
                try
                {
                    while (query.HasMoreResults)
                    {
                        FeedResponse<Dictionary<string, string>> response = await query.ReadNextAsync();
                        documentTotal += response.Count;
                        continuation = response.ContinuationToken;
                    }

                    metrics.Measure.Gauge.SetValue(documentGauge, documentTotal);

                    if (expectedResults > 0 && expectedResults != documentTotal)
                    {
                        StringBuilder errorDetail = new StringBuilder();
                        errorDetail.AppendLine($"{queryName} Query expected to read {expectedResults} but got {documentTotal}");
                        errorDetail.AppendLine($"Last continuation: {continuation}");

                        logger.LogError(errorDetail.ToString());
                    }
                }
                catch (CosmosException ce) when (ce.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    //Logging 429s is not relevant
                }
                catch (Exception ex)
                {
                    metrics.Measure.Gauge.SetValue(documentGauge, documentTotal);

                    StringBuilder errorDetail = new StringBuilder();
                    errorDetail.AppendLine($"{queryName} Query failure while looping through query.");
                    errorDetail.AppendLine($"Last continuation: {continuation}");

                    logger.LogError(ex, errorDetail.ToString());
                }
            }

            stopWatch.Stop();
        }
    }
}
