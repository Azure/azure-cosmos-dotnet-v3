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
    using App.Metrics.Histogram;
    using App.Metrics.Timer;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;

    internal class ReadWriteQueryScenario : ICTLScenario
    {
        private static readonly string DefaultPartitionKey = "pk";
        private static readonly string DefaultPartitionKeyPath = $"/{DefaultPartitionKey}";
        private static readonly int DefaultDocumentFieldCount = 5;
        private static readonly int DefaultDataFieldSize = 20;

        private static readonly string DataFieldValue = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, DefaultDataFieldSize);

        private readonly Random random = new Random();

        public async Task RunAsync(
            CTLConfig config, 
            CosmosClient cosmosClient,
            ILogger logger, 
            IMetrics metrics,
            CancellationToken cancellationToken)
        {
            if (!TryParseReadWriteQueryPercentages(config.ReadWriteQueryPercentage, out ReadWriteQueryPercentage readWriteQueryPercentage))
            {
                logger.LogError("Cannot correctly parse {0} = {1}", nameof(config.ReadWriteQueryPercentage), config.ReadWriteQueryPercentage);
                return;
            }

            InitializationResult initializationResult = await CreateDatabaseAndContainersAsync(config, cosmosClient);
            if (initializationResult.CreatedDatabase)
            {
                logger.LogInformation("Created database for execution");
            }

            if (initializationResult.CreatedContainers.Count > 0)
            {
                logger.LogInformation("Created {0} collections for execution", initializationResult.CreatedContainers.Count);
            }

            try
            {
                await this.ExecuteOperationsAsync(config, logger, metrics, initializationResult, readWriteQueryPercentage, cancellationToken);
            }
            catch (Exception unhandledException)
            {
                logger.LogError(unhandledException, "Unhandled exception executing {0}", nameof(ReadWriteQueryScenario));
            }
            finally
            {
                if (initializationResult.CreatedDatabase)
                {
                    await cosmosClient.GetDatabase(config.Database).DeleteAsync();
                }
                else
                {
                    foreach (string createdCollection in initializationResult.CreatedContainers)
                    {
                        await cosmosClient.GetContainer(config.Database, createdCollection).DeleteContainerAsync();
                    }
                }
            }
        }

        private async Task ExecuteOperationsAsync(
            CTLConfig config,
            ILogger logger,
            IMetrics metrics,
            InitializationResult initializationResult,
            ReadWriteQueryPercentage readWriteQueryPercentage,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Pre-populating {0} documents", config.Operations);
            IReadOnlyDictionary<string, IReadOnlyList<Dictionary<string, string>>> createdDocuments = await PopulateDocumentsAsync(config, logger, initializationResult.Containers);

            logger.LogInformation("Initializing counters and metrics.");
            CounterOptions readSuccessMeter = new CounterOptions { Name = "#Read Successful Operations" };
            CounterOptions readFailureMeter = new CounterOptions { Name = "#Read Unsuccessful Operations" };
            CounterOptions writeSuccessMeter = new CounterOptions { Name = "#Write Successful Operations" };
            CounterOptions writeFailureMeter = new CounterOptions { Name = "#Write Unsuccessful Operations" };
            CounterOptions querySuccessMeter = new CounterOptions { Name = "#Query Successful Operations" };
            CounterOptions queryFailureMeter = new CounterOptions { Name = "#Query Unsuccessful Operations" };

            TimerOptions readLatencyTimer = new TimerOptions
            {
                Name = "Read latency",
                MeasurementUnit = Unit.Requests,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds,
                Reservoir = () => new App.Metrics.ReservoirSampling.Uniform.DefaultAlgorithmRReservoir()
            };

            TimerOptions writeLatencyTimer = new TimerOptions
            {
                Name = "Write latency",
                MeasurementUnit = Unit.Requests,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds,
                Reservoir = () => new App.Metrics.ReservoirSampling.Uniform.DefaultAlgorithmRReservoir()
            };

            TimerOptions queryLatencyTimer = new TimerOptions
            {
                Name = "Query latency",
                MeasurementUnit = Unit.Requests,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds,
                Reservoir = () => new App.Metrics.ReservoirSampling.Uniform.DefaultAlgorithmRReservoir()
            };

            SemaphoreSlim concurrencyControlSemaphore = new SemaphoreSlim(config.Concurrency);
            Stopwatch stopwatch = Stopwatch.StartNew();
            int writeRange = readWriteQueryPercentage.ReadPercentage + readWriteQueryPercentage.WritePercentage;
            long diagnosticsThresholdDuration = (long)config.DiagnosticsThresholdDurationAsTimespan.TotalMilliseconds;
            List<Task> operations = new List<Task>((int)config.Operations);
            for (long i = 0; ShouldContinue(stopwatch, i, config); i++)
            {
                long index = (long)i % 100;
                if (index < readWriteQueryPercentage.ReadPercentage)
                {
                    operations.Add(CTLOperationHandler<ItemResponse<Dictionary<string, string>>>.PerformOperationAsync(
                        semaphoreSlim: concurrencyControlSemaphore,
                        diagnosticsLoggingThreshold: diagnosticsThresholdDuration,
                        createTimerContext: () => metrics.Measure.Timer.Time(readLatencyTimer),
                        resultProducer: new SingleExecutionResultProducer<ItemResponse<Dictionary<string, string>>>(() => this.CreateReadOperation(
                            operation: i,
                            containers: initializationResult.Containers,
                            createdDocumentsPerContainer: createdDocuments)),
                        onSuccess: () => metrics.Measure.Counter.Increment(readSuccessMeter),
                        onFailure: (Exception ex) =>
                        {
                            metrics.Measure.Counter.Increment(readFailureMeter);
                            logger.LogError(ex, "Failure during read operation");
                        },
                        logDiagnostics: (ItemResponse<Dictionary<string, string>> response) => logger.LogInformation("Read request took more than latency threshold {0}, diagnostics: {1}", config.DiagnosticsThresholdDuration, response.Diagnostics.ToString()),
                        cancellationToken: cancellationToken));
                }
                else if (index < writeRange)
                {
                    operations.Add(CTLOperationHandler<ItemResponse<Dictionary<string, string>>>.PerformOperationAsync(
                        semaphoreSlim: concurrencyControlSemaphore,
                        diagnosticsLoggingThreshold: diagnosticsThresholdDuration,
                        createTimerContext: () => metrics.Measure.Timer.Time(writeLatencyTimer),
                        resultProducer: new SingleExecutionResultProducer<ItemResponse<Dictionary<string, string>>>(() => this.CreateWriteOperation(
                            operation: i,
                            containers: initializationResult.Containers,
                            isContentResponseOnWriteEnabled: config.IsContentResponseOnWriteEnabled)),
                        onSuccess: () => metrics.Measure.Counter.Increment(writeSuccessMeter),
                        onFailure: (Exception ex) =>
                        {
                            metrics.Measure.Counter.Increment(writeFailureMeter);
                            logger.LogError(ex, "Failure during write operation");
                        },
                        logDiagnostics: (ItemResponse<Dictionary<string, string>> response) => logger.LogInformation("Write request took more than latency threshold {0}, diagnostics: {1}", config.DiagnosticsThresholdDuration, response.Diagnostics.ToString()),
                        cancellationToken: cancellationToken));

                }
                else
                {
                    operations.Add(CTLOperationHandler<FeedResponse<Dictionary<string, string>>>.PerformOperationAsync(
                        semaphoreSlim: concurrencyControlSemaphore,
                        diagnosticsLoggingThreshold: diagnosticsThresholdDuration,
                        createTimerContext: () => metrics.Measure.Timer.Time(queryLatencyTimer),
                        resultProducer: new IteratorResultProducer<Dictionary<string, string>>(this.CreateQueryOperation(
                            operation: i,
                            containers: initializationResult.Containers)),
                        onSuccess: () => metrics.Measure.Counter.Increment(querySuccessMeter),
                        onFailure: (Exception ex) =>
                        {
                            metrics.Measure.Counter.Increment(queryFailureMeter);
                            logger.LogError(ex, "Failure during query operation");
                        },
                        logDiagnostics: (FeedResponse<Dictionary<string, string>> response) => logger.LogInformation("Query request took more than latency threshold {0}, diagnostics: {1}", config.DiagnosticsThresholdDuration, response.Diagnostics.ToString()),
                        cancellationToken: cancellationToken));
                }
            }

            await Task.WhenAll(operations);
            stopwatch.Stop();
            logger.LogInformation("[{0}] operations performed in [{1}] seconds.",
                config.Operations, stopwatch.Elapsed.TotalSeconds);
        }

        private Task<ItemResponse<Dictionary<string, string>>> CreateReadOperation(
            long operation,
            IReadOnlyList<Container> containers,
            IReadOnlyDictionary<string, IReadOnlyList<Dictionary<string, string>>> createdDocumentsPerContainer)
        {
            Container container = containers[(int)operation % containers.Count];
            IReadOnlyList<Dictionary<string, string>> documents = createdDocumentsPerContainer[container.Id];
            Dictionary<string, string> document = documents[this.random.Next(documents.Count)];
            return container.ReadItemAsync<Dictionary<string, string>>(document["id"], new PartitionKey(document[DefaultPartitionKey]));
        }

        private Task<ItemResponse<Dictionary<string, string>>> CreateWriteOperation(
            long operation,
            IReadOnlyList<Container> containers,
            bool isContentResponseOnWriteEnabled)
        {
            Container container = containers[(int)operation % containers.Count];
            Dictionary<string, string> document = GenerateDocument();
            ItemRequestOptions itemRequestOptions = new ItemRequestOptions
            {
                EnableContentResponseOnWrite = isContentResponseOnWriteEnabled
            };

            return container.CreateItemAsync<Dictionary<string, string>>(document, requestOptions: itemRequestOptions);
        }

        private FeedIterator<Dictionary<string, string>> CreateQueryOperation(
                long operation,
                IReadOnlyList<Container> containers)
        {
            Container container = containers[(int)operation % containers.Count];
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions() { MaxItemCount = 10 };
            return container.GetItemQueryIterator<Dictionary<string, string>>(
                queryText: "Select top 100 * from c order by c._ts", 
                requestOptions: queryRequestOptions);
        }

        private static bool ShouldContinue(
            Stopwatch stopwatch,
            long iterationCount,
            CTLConfig config)
        {
            TimeSpan maxDurationTime = config.RunningTimeDurationAsTimespan;
            long maxNumberOfOperations = config.Operations;

            if (maxDurationTime == null)
            {
                return iterationCount < maxNumberOfOperations;
            }

            if (maxDurationTime.TotalMilliseconds < stopwatch.ElapsedMilliseconds)
            {
                return false;
            }

            if (maxNumberOfOperations < 0)
            {
                return true;
            }

            return iterationCount < maxNumberOfOperations;
        }

        private static async Task<IReadOnlyDictionary<string, IReadOnlyList<Dictionary<string, string>>>> PopulateDocumentsAsync(
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
                IEnumerable<Dictionary<string, string>> documentsToCreate = GenerateDocuments(config.Operations);
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
                    logger.LogInformation("Completed pre-populating {0} documents in container {1].", successes, container.Id);
                }

                if (failures > 0)
                {
                    logger.LogWarning("Failed pre-populating {0} documents in container {1].", failures, container.Id);
                }

                createdDocuments.Add(container.Id, createdDocumentsInContainer.ToList());
            }

            return createdDocuments;
        }

        private static IEnumerable<Dictionary<string, string>> GenerateDocuments(long documentsToCreate)
        {
            List<Dictionary<string, string>> createdDocuments = new List<Dictionary<string, string>>((int)documentsToCreate);
            for (long i = 0; i < documentsToCreate; i++)
            {
                createdDocuments.Add(GenerateDocument());
            }

            return createdDocuments;
        }

        private static Dictionary<string, string> GenerateDocument()
        {
            Dictionary<string, string> document = new Dictionary<string, string>();
            string newGuid = Guid.NewGuid().ToString();
            document["id"] = newGuid;
            document[DefaultPartitionKey] = newGuid;
            for (int j = 0; j < DefaultDocumentFieldCount; j++)
            {
                document["dataField" + j] = DataFieldValue;
            }

            return document;
        }

        /// <summary>
        /// Create the database and the required number of collections.
        /// </summary>
        private static async Task<InitializationResult> CreateDatabaseAndContainersAsync(
            CTLConfig config,
            CosmosClient cosmosClient)
        {
            List<string> createdContainers = new List<string>();
            List<Container> containers = new List<Container>();
            InitializationResult result = new InitializationResult()
            {
                CreatedDatabase = false
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

            int collectionCount = config.CollectionCount;
            if (collectionCount <= 0)
            {
                collectionCount = 1;
            }

            for (int i = 1; i <= collectionCount; i++)
            {
                string containerName = $"{config.Collection}_{i}";
                Container container;
                try
                {
                    container = await database.GetContainer(containerName).ReadContainerAsync();
                }
                catch (CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    container = await database.CreateContainerAsync(containerName, ReadWriteQueryScenario.DefaultPartitionKeyPath);
                    createdContainers.Add(containerName);
                }

                containers.Add(container);
            }

            result.CreatedContainers = createdContainers;
            result.Containers = containers;
            return result;
        }

        private static bool TryParseReadWriteQueryPercentages(
            string configuration,
            out ReadWriteQueryPercentage readWriteQueryPercentage)
        {
            readWriteQueryPercentage = default;
            string[] readWriteQueryPctList = configuration.Split(",");
            if (readWriteQueryPctList.Length != 3)
            {
                return false;
            }

            if (!int.TryParse(readWriteQueryPctList[0], out int readPercentage)
                    || !int.TryParse(readWriteQueryPctList[1], out int writePercentage)
                    || !int.TryParse(readWriteQueryPctList[2], out int queryPercentage))
            {
                return false;
            }

            if ((readPercentage + writePercentage + queryPercentage) != 100)
            {
                return false;
            }

            readWriteQueryPercentage = new ReadWriteQueryPercentage()
            {
                ReadPercentage = readPercentage,
                WritePercentage = writePercentage,
                QueryPercentage = queryPercentage
            };

            return true;
        }

        private struct ReadWriteQueryPercentage
        {
            public int ReadPercentage;
            public int WritePercentage;
            public int QueryPercentage;
        }

        private struct InitializationResult
        {
            public bool CreatedDatabase;
            public IReadOnlyList<string> CreatedContainers;
            public IReadOnlyList<Container> Containers;
        }
    }
}
