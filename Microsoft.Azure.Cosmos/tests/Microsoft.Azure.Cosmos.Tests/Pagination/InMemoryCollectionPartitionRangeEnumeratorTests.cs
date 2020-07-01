namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal abstract class InMemoryCollectionPartitionRangeEnumeratorTests<TPage, TState>
        where TPage : Page<TState>
        where TState : State
    {
        [TestMethod]
        public async Task TestDrainFullyAsync()
        {
            int numItems = 1000;
            InMemoryCollection inMemoryCollection = this.CreateInMemoryCollection(numItems);
            IAsyncEnumerable<TryCatch<TPage>> enumerable = this.CreateEnumerable(inMemoryCollection);
            HashSet<Guid> identifiers = await this.DrainFullyAsync(enumerable);
            Assert.AreEqual(numItems, identifiers.Count);
        }

        [TestMethod]
        public async Task TestResumingFromStateAsync()
        {
            int numItems = 1000;
            InMemoryCollection inMemoryCollection = this.CreateInMemoryCollection(numItems);

            IAsyncEnumerator<TryCatch<TPage>> enumerator = this.CreateEnumerator(inMemoryCollection);
            (HashSet<Guid> firstDrainResults, TState state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

            IAsyncEnumerable<TryCatch<TPage>> enumerable = this.CreateEnumerable(inMemoryCollection, state);
            HashSet<Guid> secondDrainResults = await this.DrainFullyAsync(enumerable);

            Assert.AreEqual(numItems, firstDrainResults.Count + secondDrainResults.Count);
        }

        [TestMethod]
        public async Task Test429sAsync()
        {
            int numItems = 100;
            InMemoryCollection inMemoryCollection = this.CreateInMemoryCollection(
                numItems,
                new InMemoryCollection.FailureConfigs(
                    inject429s: true,
                    injectEmptyPages: false));

            IAsyncEnumerable<TryCatch<TPage>> enumerable = this.CreateEnumerable(inMemoryCollection);

            HashSet<Guid> identifiers = new HashSet<Guid>();
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
                    List<InMemoryCollection.Record> records = this.GetRecordsFromPage(tryGetPage.Result);
                    foreach (InMemoryCollection.Record record in records)
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
            InMemoryCollection inMemoryCollection = this.CreateInMemoryCollection(
                numItems,
                new InMemoryCollection.FailureConfigs(
                    inject429s: true,
                    injectEmptyPages: false));

            IAsyncEnumerator<TryCatch<TPage>> enumerator = this.CreateEnumerator(inMemoryCollection);

            HashSet<Guid> identifiers = new HashSet<Guid>();
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
                    List<InMemoryCollection.Record> records = this.GetRecordsFromPage(tryGetPage.Result);
                    foreach (InMemoryCollection.Record record in records)
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
            InMemoryCollection inMemoryCollection = this.CreateInMemoryCollection(
                numItems,
                new InMemoryCollection.FailureConfigs(
                    inject429s: false,
                    injectEmptyPages: true));
            IAsyncEnumerable<TryCatch<TPage>> enumerable = this.CreateEnumerable(inMemoryCollection);
            HashSet<Guid> identifiers = await this.DrainFullyAsync(enumerable);
            Assert.AreEqual(numItems, identifiers.Count);
        }

        internal abstract List<InMemoryCollection.Record> GetRecordsFromPage(TPage page);

        internal abstract InMemoryCollection CreateInMemoryCollection(int numItems, InMemoryCollection.FailureConfigs failureConfigs = default);

        internal abstract IAsyncEnumerable<TryCatch<TPage>> CreateEnumerable(InMemoryCollection inMemoryCollection, TState state = null);

        internal abstract IAsyncEnumerator<TryCatch<TPage>> CreateEnumerator(InMemoryCollection inMemoryCollection, TState state = null);

        internal async Task<HashSet<Guid>> DrainFullyAsync(IAsyncEnumerable<TryCatch<TPage>> enumerable)
        {
            HashSet<Guid> identifiers = new HashSet<Guid>();
            await foreach (TryCatch<TPage> tryGetPage in enumerable)
            {
                tryGetPage.ThrowIfFailed();

                List<InMemoryCollection.Record> records = this.GetRecordsFromPage(tryGetPage.Result);

                foreach (InMemoryCollection.Record record in records)
                {
                    identifiers.Add(record.Identifier);
                }
            }

            return identifiers;
        }

        internal async Task<(HashSet<Guid>, TState)> PartialDrainAsync(
            IAsyncEnumerator<TryCatch<TPage>> enumerator,
            int numIterations)
        {
            HashSet<Guid> identifiers = new HashSet<Guid>();
            TState state = default;

            // Drain a couple of iterations
            for (int i = 0; i < numIterations; i++)
            {
                await enumerator.MoveNextAsync();

                TryCatch<TPage> tryGetPage = enumerator.Current;
                tryGetPage.ThrowIfFailed();

                List<InMemoryCollection.Record> records = this.GetRecordsFromPage(tryGetPage.Result);

                foreach (InMemoryCollection.Record record in records)
                {
                    identifiers.Add(record.Identifier);
                }

                state = tryGetPage.Result.State;
            }

            return (identifiers, state);
        }
    }
}
