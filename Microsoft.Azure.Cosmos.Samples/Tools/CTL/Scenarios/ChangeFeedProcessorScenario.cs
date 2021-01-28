//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Gauge;
    using App.Metrics.Timer;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class ChangeFeedProcessorScenario : ICTLScenario
    {
        private static readonly int DefaultDocumentFieldCount = 5;
        private static readonly int DefaultDataFieldSize = 20;
        private static readonly string DataFieldValue = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, DefaultDataFieldSize);

        private readonly Random random = new Random();

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

            if (config.Operations > 0)
            {
                logger.LogInformation("Pre-populating {0} documents", config.Operations);
                await PopulateDocumentsAsync(config, cosmosClient, logger);
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
            logger.LogInformation("Initializing counters and metrics.");
            CounterOptions documentCounter = new CounterOptions { Name = "#Documents received", Context = loggingContextIdentifier };
            GaugeOptions leaseGauge = new GaugeOptions { Name = "#Leases created", Context = loggingContextIdentifier };

            Container leaseContainer = await cosmosClient.GetDatabase(config.Database).CreateContainerAsync(Guid.NewGuid().ToString(), "/id");

            try
            {
                ChangeFeedProcessor changeFeedProcessor = cosmosClient.GetContainer(config.Database, config.Collection)
                    .GetChangeFeedProcessorBuilder<SimpleItem>("ctlProcessor", 
                    (IReadOnlyCollection<SimpleItem> docs, CancellationToken token) =>
                        {
                            metrics.Measure.Counter.Increment(documentCounter, docs.Count);
                            return Task.CompletedTask;
                        })
                    .WithLeaseContainer(leaseContainer)
                    .WithInstanceName(Guid.NewGuid().ToString())
                    .WithStartTime(DateTime.MinValue.ToUniversalTime())
                    .Build();

                await changeFeedProcessor.StartAsync();
                logger.LogInformation("Started change feed processor");

                await Task.Delay(config.RunningTimeDurationAsTimespan, cancellationToken);

                logger.LogInformation("Stopping change feed processor");
                await changeFeedProcessor.StopAsync();

                // List leases
                using FeedIterator<LeaseSchema> leaseIterator = leaseContainer.GetItemQueryIterator<LeaseSchema>();
                int leaseTotal = 0;
                List<FeedRange> ranges = new List<FeedRange>();
                while (leaseIterator.HasMoreResults)
                {
                    FeedResponse<LeaseSchema> response = await leaseIterator.ReadNextAsync();
                    foreach (LeaseSchema lease in response)
                    {
                        if (lease.LeaseToken != null)
                        {
                            logger.LogInformation($"Lease for range {lease.LeaseToken}");
                            ranges.Add(lease.FeedRange.EffectiveRange);
                            leaseTotal++;
                        }
                    }
                }

                string previousMin = "";
                foreach(FeedRange sortedRange in ranges.OrderBy(range => range.Min))
                {
                    if (previousMin != sortedRange.Min)
                    {
                        logger.LogError($"Expected a sorted range with Min <{previousMin}> but encountered range <{sortedRange.Min}>:<{sortedRange.Max}>");
                    }

                    previousMin = sortedRange.Max;
                }

                metrics.Measure.Gauge.SetValue(leaseGauge, leaseTotal);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure during Change Feed Processor initialization");
            }
            finally
            {
                await leaseContainer.DeleteContainerAsync();
                if (this.initializationResult.CreatedDatabase)
                {
                    await cosmosClient.GetDatabase(config.Database).DeleteAsync();
                }

                if (this.initializationResult.CreatedContainer)
                {
                    await cosmosClient.GetContainer(config.Database, config.Collection).DeleteContainerAsync();
                }
            }
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

        private static async Task PopulateDocumentsAsync(
            CTLConfig config,
            CosmosClient cosmosClient,
            ILogger logger)
        {
            long successes = 0;
            long failures = 0;
            Container container = cosmosClient.GetContainer(config.Database, config.Collection);
            ConcurrentBag<Dictionary<string, string>> createdDocumentsInContainer = new ConcurrentBag<Dictionary<string, string>>();
            IEnumerable<Dictionary<string, string>> documentsToCreate = GenerateDocuments(config.Operations, config.CollectionPartitionKey);
            await Utils.ForEachAsync(documentsToCreate, (Dictionary<string, string> doc)
                => container.CreateItemAsync(doc).ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        createdDocumentsInContainer.Add(doc);
                        Interlocked.Increment(ref successes);
                    }
                    else
                    {
                        AggregateException innerExceptions = task.Exception.Flatten();
                        if (innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is CosmosException) is CosmosException cosmosException)
                        {
                            logger.LogError(cosmosException, "Failure pre-populating container {0}", container.Id);
                        }

                        Interlocked.Increment(ref failures);
                    }
                }), 100);

            if (successes > 0)
            {
                logger.LogInformation("Completed pre-populating {0} documents in container {1}.", successes, container.Id);
            }

            if (failures > 0)
            {
                logger.LogWarning("Failed pre-populating {0} documents in container {1}.", failures, container.Id);
            }
        }

        private static IEnumerable<Dictionary<string, string>> GenerateDocuments(
            long documentsToCreate,
            string partitionKeyPropertyName)
        {
            List<Dictionary<string, string>> createdDocuments = new List<Dictionary<string, string>>((int)documentsToCreate);
            for (long i = 0; i < documentsToCreate; i++)
            {
                createdDocuments.Add(GenerateDocument(partitionKeyPropertyName));
            }

            return createdDocuments;
        }

        private static Dictionary<string, string> GenerateDocument(string partitionKeyPropertyName)
        {
            Dictionary<string, string> document = new Dictionary<string, string>();
            string newGuid = Guid.NewGuid().ToString();
            document["id"] = newGuid;
            document[partitionKeyPropertyName] = newGuid;
            for (int j = 0; j < DefaultDocumentFieldCount; j++)
            {
                document["dataField" + j] = DataFieldValue;
            }

            return document;
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
