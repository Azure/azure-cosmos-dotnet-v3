//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Extensions.Logging;

    internal static class Utils
    {
        private static readonly int DefaultDocumentFieldCount = 5;
        private static readonly int DefaultDataFieldSize = 20;
        private static readonly string DataFieldValue = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, DefaultDataFieldSize);

        public static Task ForEachAsync<TSource>(
            this IEnumerable<TSource> source,
            Func<TSource, Task> worker,
            int maxParallelTaskCount = 0,
            CancellationToken cancellationToken = default)
        {
            if (maxParallelTaskCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxParallelTaskCount));
            }

            return Task.WhenAll(
                Partitioner.Create(source)
                           .GetPartitions(maxParallelTaskCount)
                           .Select(partition => Task.Run(
                               async () =>
                               {
                                   using (partition)
                                   {
                                       while (partition.MoveNext())
                                       {
                                           cancellationToken.ThrowIfCancellationRequested();
                                           await worker(partition.Current).ConfigureAwait(false);
                                       }
                                   }
                               })));
        }

        public static async Task<IReadOnlyDictionary<string, IReadOnlyList<Dictionary<string, string>>>> PopulateDocumentsAsync(
            CTLConfig config,
            ILogger logger,
            IEnumerable<Container> containers)
        {
            Dictionary<string, IReadOnlyList<Dictionary<string, string>>> createdDocuments = new Dictionary<string, IReadOnlyList<Dictionary<string, string>>>();
            foreach (Container container in containers)
            {
                long successes = 0;
                long failures = 0;
                ConcurrentBag<Dictionary<string, string>> createdDocumentsInContainer = new ConcurrentBag<Dictionary<string, string>>();
                IEnumerable<Dictionary<string, string>> documentsToCreate = GenerateDocuments(config.PreCreatedDocuments, config.CollectionPartitionKey);
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
                            Utils.LogError(logger, $"Pre-populating {container.Id}", task.Exception);

                            Interlocked.Increment(ref failures);
                        }
                    }), config.Concurrency);

                if (successes > 0)
                {
                    logger.LogInformation("Completed pre-populating {0} documents in container {1}.", successes, container.Id);
                }

                if (failures > 0)
                {
                    logger.LogWarning("Failed pre-populating {0} documents in container {1}.", failures, container.Id);
                }

                createdDocuments.Add(container.Id, createdDocumentsInContainer.ToList());
            }

            return createdDocuments;
        }

        public static IEnumerable<Dictionary<string, string>> GenerateDocuments(
            long documentsToCreate,
            string partitionKeyPropertyName)
        {
            List<Dictionary<string, string>> createdDocuments = new List<Dictionary<string, string>>();
            for (long i = 0; i < documentsToCreate; i++)
            {
                createdDocuments.Add(GenerateDocument(partitionKeyPropertyName));
            }

            return createdDocuments;
        }

        public static Dictionary<string, string> GenerateDocument(string partitionKeyPropertyName)
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

        public static async Task<InitializationResult> CreateDatabaseAndContainerAsync(
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

        public static void LogDiagnsotics(
            ILogger logger,
            string operationName,
            TimeSpan timerContextLatency,
            CTLConfig config,
            CosmosDiagnostics cosmosDiagnostics)
        {

            if (timerContextLatency > config.DiagnosticsThresholdDurationAsTimespan)
            {
                logger.LogInformation($"{operationName}; LatencyInMs:{timerContextLatency.TotalMilliseconds}; request took more than latency threshold {config.DiagnosticsThresholdDuration}, diagnostics: {cosmosDiagnostics}");
            }

            CosmosTraceDiagnostics traceDiagnostics = (CosmosTraceDiagnostics)cosmosDiagnostics;
            if (traceDiagnostics.IsGoneExceptionHit())
            {
                logger.LogInformation($"{operationName}; LatencyInMs:{timerContextLatency.TotalMilliseconds}; request contains 410(GoneExceptions), diagnostics:{cosmosDiagnostics}");
                return;
            }
        }

        public static void LogError(
            ILogger logger,
            string context,
            Exception ex,
            string extraDetails = null)
        {
            if (ex is AggregateException aggregateException)
            {
                AggregateException innerExceptions = aggregateException.Flatten();
                ex = innerExceptions.InnerExceptions.FirstOrDefault();
            }
            
            if (ex is CosmosException cosmosException && cosmosException.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // Not consider throttles as errors
            }
            else
            {
                logger.LogError(ex, $"[ERROR][{context}]{extraDetails ?? string.Empty}");
            }
        }

        public static void LogError(
            ILogger logger,
            string context,
            string error)
        {
            logger.LogError($"[ERROR][{context}]{error}");
        }

        public struct InitializationResult
        {
            public bool CreatedDatabase;
            public bool CreatedContainer;
            public long InsertedDocuments;
        }
    }
}
