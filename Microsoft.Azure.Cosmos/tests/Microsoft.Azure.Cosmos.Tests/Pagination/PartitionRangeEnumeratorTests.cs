namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal abstract class PartitionRangeEnumeratorTests<TPage, TState>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly bool singlePartition;
        protected PartitionRangeEnumeratorTests(bool singlePartition)
        {
            this.singlePartition = singlePartition;
        }

        [TestMethod]
        public async Task TestMoveNextAsyncThrowsTaskCanceledException()
        {
            int numItems = 100;
            IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
            CancellationTokenSource cts = new CancellationTokenSource();
            IAsyncEnumerator<TryCatch<TPage>> enumerator = await this.CreateEnumeratorAsync(
                inMemoryCollection,
                aggressivePrefetch: false,
                exercisePrefetch: false,
                state: null,
                cancellationToken: cts.Token);

            cts.Cancel();
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
        }

        [TestMethod]
        public async Task TestDrainFullyAsync(bool aggressivePrefetch)
        {
            int numItems = 1000;
            IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
            IAsyncEnumerable<TryCatch<TPage>> enumerable = this.CreateEnumerable(inMemoryCollection, aggressivePrefetch);
            HashSet<string> identifiers = await this.DrainFullyAsync(enumerable);
            Assert.AreEqual(numItems, identifiers.Count);
        }

        [TestMethod]
        public async Task TestResumingFromStateAsync(bool aggressivePrefetch, bool exercisePrefetch)
        {
            int numItems = 1000;
            IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);

            IAsyncEnumerator<TryCatch<TPage>> enumerator = await this.CreateEnumeratorAsync(inMemoryCollection, aggressivePrefetch, exercisePrefetch);
            (HashSet<string> firstDrainResults, TState state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

            IAsyncEnumerable<TryCatch<TPage>> enumerable = this.CreateEnumerable(inMemoryCollection, aggressivePrefetch, state);
            HashSet<string> secondDrainResults = await this.DrainFullyAsync(enumerable);

            Assert.AreEqual(numItems, firstDrainResults.Count + secondDrainResults.Count);
        }

        [TestMethod]
        public async Task Test429sAsync(bool aggressivePrefetch)
        {
            int numItems = 100;
            IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(
                numItems,
                new FlakyDocumentContainer.FailureConfigs(
                    inject429s: true,
                    injectEmptyPages: false));

            IAsyncEnumerable<TryCatch<TPage>> enumerable = this.CreateEnumerable(inMemoryCollection, aggressivePrefetch);

            HashSet<string> identifiers = new HashSet<string>();
            await foreach (TryCatch<TPage> tryGetPage in enumerable)
            {
                if (tryGetPage.Failed)
                {
                    Exception exception = tryGetPage.Exception;
                    while (exception.InnerException != null)
                    {
                        exception = exception.InnerException;
                    }

                    if (!((exception is CosmosException cosmosException) && (cosmosException.StatusCode == (System.Net.HttpStatusCode)429)))
                    {
                        throw tryGetPage.Exception;
                    }
                }
                else
                {
                    IReadOnlyList<Record> records = this.GetRecordsFromPage(tryGetPage.Result);
                    foreach (Record record in records)
                    {
                        identifiers.Add(record.Identifier);
                    }
                }
            }

            Assert.AreEqual(numItems, identifiers.Count);
        }

        [TestMethod]
        public async Task Test429sWithContinuationsAsync(bool aggressivePrefetch, bool exercisePrefetch)
        {
            int numItems = 100;
            IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(
                numItems,
                new FlakyDocumentContainer.FailureConfigs(
                    inject429s: true,
                    injectEmptyPages: false));

            IAsyncEnumerator<TryCatch<TPage>> enumerator = await this.CreateEnumeratorAsync(
                inMemoryCollection,
                aggressivePrefetch,
                exercisePrefetch);

            HashSet<string> identifiers = new HashSet<string>();
            TState state = default;

            while (await enumerator.MoveNextAsync())
            {
                TryCatch<TPage> tryGetPage = enumerator.Current;
                if (tryGetPage.Failed)
                {
                    Exception exception = tryGetPage.Exception;
                    while (exception.InnerException != null)
                    {
                        exception = exception.InnerException;
                    }

                    if (!((exception is CosmosException cosmosException) && (cosmosException.StatusCode == (System.Net.HttpStatusCode)429)))
                    {
                        throw tryGetPage.Exception;
                    }

                    // Create a new enumerator from that state to simulate when the user want's to start resume later from a continuation token.
                    enumerator = await this.CreateEnumeratorAsync(inMemoryCollection, false, false, state);
                }
                else
                {
                    IReadOnlyList<Record> records = this.GetRecordsFromPage(tryGetPage.Result);
                    foreach (Record record in records)
                    {
                        identifiers.Add(record.Identifier);
                    }

                    state = tryGetPage.Result.State;
                }
            }

            Assert.AreEqual(numItems, identifiers.Count);
        }

        [TestMethod]
        public async Task TestEmptyPages(bool aggressivePrefetch)
        {
            int numItems = 100;
            IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(
                numItems,
                new FlakyDocumentContainer.FailureConfigs(
                    inject429s: false,
                    injectEmptyPages: true));
            IAsyncEnumerable<TryCatch<TPage>> enumerable = this.CreateEnumerable(inMemoryCollection, aggressivePrefetch);
            HashSet<string> identifiers = await this.DrainFullyAsync(enumerable);
            Assert.AreEqual(numItems, identifiers.Count);
        }

        public abstract IReadOnlyList<Record> GetRecordsFromPage(TPage page);

        protected abstract IAsyncEnumerable<TryCatch<TPage>> CreateEnumerable(
            IDocumentContainer documentContainer,
            bool aggressivePrefetch = false,
            TState state = null);

        protected abstract Task<IAsyncEnumerator<TryCatch<TPage>>> CreateEnumeratorAsync(
                IDocumentContainer inMemoryCollection,
                bool aggressivePrefetch = false,
                bool exercisePrefetch = false,
                TState state = null,
                CancellationToken cancellationToken = default);

        public async Task<IDocumentContainer> CreateDocumentContainerAsync(
            int numItems,
            FlakyDocumentContainer.FailureConfigs failureConfigs = default)
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Paths = new System.Collections.ObjectModel.Collection<string>()
                    {
                        "/pk"
                    },
                Kind = PartitionKind.Hash,
                Version = PartitionKeyDefinitionVersion.V2,
            };

            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            if (failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }

            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            if (!this.singlePartition)
            {
                for (int i = 0; i < 3; i++)
                {
                    IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(
                        trace: NoOpTrace.Singleton,
                        cancellationToken: default);
                    foreach (FeedRangeInternal range in ranges)
                    {
                        await documentContainer.SplitAsync(range, cancellationToken: default);
                    }

                    await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                }
            }

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            return documentContainer;
        }

        public async Task<HashSet<string>> DrainFullyAsync(IAsyncEnumerable<TryCatch<TPage>> enumerable)
        {
            HashSet<string> identifiers = new HashSet<string>();
            await foreach (TryCatch<TPage> tryGetPage in enumerable)
            {
                tryGetPage.ThrowIfFailed();

                IReadOnlyList<Record> records = this.GetRecordsFromPage(tryGetPage.Result);

                foreach (Record record in records)
                {
                    identifiers.Add(record.Identifier);
                }
            }

            return identifiers;
        }

        public async Task<(HashSet<string>, TState)> PartialDrainAsync(
            IAsyncEnumerator<TryCatch<TPage>> enumerator,
            int numIterations)
        {
            HashSet<string> identifiers = new HashSet<string>();
            TState state = default;

            // Drain a couple of iterations
            for (int i = 0; i < numIterations; i++)
            {
                await enumerator.MoveNextAsync();

                TryCatch<TPage> tryGetPage = enumerator.Current;
                tryGetPage.ThrowIfFailed();

                IReadOnlyList<Record> records = this.GetRecordsFromPage(tryGetPage.Result);

                foreach (Record record in records)
                {
                    identifiers.Add(record.Identifier);
                }

                state = tryGetPage.Result.State;
            }

            return (identifiers, state);
        }
    }
}