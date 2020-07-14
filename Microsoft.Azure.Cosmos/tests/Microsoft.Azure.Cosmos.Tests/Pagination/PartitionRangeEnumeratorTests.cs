namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
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
        public async Task TestDrainFullyAsync()
        {
            int numItems = 1000;
            DocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
            IAsyncEnumerable<TryCatch<TPage>> enumerable = this.CreateEnumerable(inMemoryCollection);
            HashSet<string> identifiers = await this.DrainFullyAsync(enumerable);
            Assert.AreEqual(numItems, identifiers.Count);
        }

        [TestMethod]
        public async Task TestResumingFromStateAsync()
        {
            int numItems = 1000;
            DocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);

            IAsyncEnumerator<TryCatch<TPage>> enumerator = this.CreateEnumerator(inMemoryCollection);
            (HashSet<string> firstDrainResults, TState state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

            IAsyncEnumerable<TryCatch<TPage>> enumerable = this.CreateEnumerable(inMemoryCollection, state);
            HashSet<string> secondDrainResults = await this.DrainFullyAsync(enumerable);

            Assert.AreEqual(numItems, firstDrainResults.Count + secondDrainResults.Count);
        }

        [TestMethod]
        public async Task Test429sAsync()
        {
            int numItems = 100;
            DocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(
                numItems,
                new DocumentContainer.FailureConfigs(
                    inject429s: true,
                    injectEmptyPages: false));

            IAsyncEnumerable<TryCatch<TPage>> enumerable = this.CreateEnumerable(inMemoryCollection);

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
        public async Task Test429sWithContinuationsAsync()
        {
            int numItems = 100;
            DocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(
                numItems,
                new DocumentContainer.FailureConfigs(
                    inject429s: true,
                    injectEmptyPages: false));

            IAsyncEnumerator<TryCatch<TPage>> enumerator = this.CreateEnumerator(inMemoryCollection);

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
                    enumerator = this.CreateEnumerator(inMemoryCollection, state);
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
        public async Task TestEmptyPages()
        {
            int numItems = 100;
            DocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(
                numItems,
                new DocumentContainer.FailureConfigs(
                    inject429s: false,
                    injectEmptyPages: true));
            IAsyncEnumerable<TryCatch<TPage>> enumerable = this.CreateEnumerable(inMemoryCollection);
            HashSet<string> identifiers = await this.DrainFullyAsync(enumerable);
            Assert.AreEqual(numItems, identifiers.Count);
        }

        public abstract IReadOnlyList<Record> GetRecordsFromPage(TPage page);

        public abstract IAsyncEnumerable<TryCatch<TPage>> CreateEnumerable(DocumentContainer documentContainer, TState state = null);

        public abstract IAsyncEnumerator<TryCatch<TPage>> CreateEnumerator(DocumentContainer documentContainer, TState state = null);

        public async Task<DocumentContainer> CreateDocumentContainerAsync(
            int numItems,
            DocumentContainer.FailureConfigs failureConfigs = default)
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

            InMemoryCollection inMemoryCollection = new InMemoryCollection(partitionKeyDefinition, failureConfigs);

            if (!this.singlePartition)
            {
                await inMemoryCollection.SplitAsync(partitionKeyRangeId: 0, cancellationToken: default);

                await inMemoryCollection.SplitAsync(partitionKeyRangeId: 1, cancellationToken: default);
                await inMemoryCollection.SplitAsync(partitionKeyRangeId: 2, cancellationToken: default);

                await inMemoryCollection.SplitAsync(partitionKeyRangeId: 3, cancellationToken: default);
                await inMemoryCollection.SplitAsync(partitionKeyRangeId: 4, cancellationToken: default);
                await inMemoryCollection.SplitAsync(partitionKeyRangeId: 5, cancellationToken: default);
                await inMemoryCollection.SplitAsync(partitionKeyRangeId: 6, cancellationToken: default);
            }

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await inMemoryCollection.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            return inMemoryCollection;
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
