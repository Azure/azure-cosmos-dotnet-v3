namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public abstract class InMemoryCollectionPartitionRangeEnumeratorTests
    {
        [TestMethod]
        public async Task TestDrainFullyAsync()
        {
            int numItems = 1000;
            InMemoryCollection inMemoryCollection = this.CreateInMemoryCollection(numItems);
            IAsyncEnumerable<TryCatch<Page>> enumerable = this.CreateEnumerable(inMemoryCollection);
            HashSet<Guid> identifiers = await this.DrainFullyAsync(enumerable);
            Assert.AreEqual(numItems, identifiers.Count);
        }

        [TestMethod]
        public async Task TestResumingFromStateAsync()
        {
            int numItems = 1000;
            InMemoryCollection inMemoryCollection = this.CreateInMemoryCollection(numItems);

            IAsyncEnumerator<TryCatch<Page>> enumerator = this.CreateEnumerator(inMemoryCollection);
            (HashSet<Guid> firstDrainResults, State state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

            IAsyncEnumerable<TryCatch<Page>> enumerable = this.CreateEnumerable(inMemoryCollection, state);
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

            IAsyncEnumerable<TryCatch<Page>> enumerable = this.CreateEnumerable(inMemoryCollection);

            HashSet<Guid> identifiers = new HashSet<Guid>();
            await foreach (TryCatch<Page> tryGetPage in enumerable)
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

            IAsyncEnumerator<TryCatch<Page>> enumerator = this.CreateEnumerator(inMemoryCollection);

            HashSet<Guid> identifiers = new HashSet<Guid>();
            State state = default;

            while (await enumerator.MoveNextAsync())
            {
                TryCatch<Page> tryGetPage = enumerator.Current;
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
            IAsyncEnumerable<TryCatch<Page>> enumerable = this.CreateEnumerable(inMemoryCollection);
            HashSet<Guid> identifiers = await this.DrainFullyAsync(enumerable);
            Assert.AreEqual(numItems, identifiers.Count);
        }

        internal abstract List<InMemoryCollection.Record> GetRecordsFromPage(Page page);

        internal abstract InMemoryCollection CreateInMemoryCollection(int numItems, InMemoryCollection.FailureConfigs failureConfigs = default);

        internal abstract IAsyncEnumerable<TryCatch<Page>> CreateEnumerable(InMemoryCollection inMemoryCollection, State state = null);

        internal abstract IAsyncEnumerator<TryCatch<Page>> CreateEnumerator(InMemoryCollection inMemoryCollection, State state = null);

        internal async Task<HashSet<Guid>> DrainFullyAsync(IAsyncEnumerable<TryCatch<Page>> enumerable)
        {
            HashSet<Guid> identifiers = new HashSet<Guid>();
            await foreach (TryCatch<Page> tryGetPage in enumerable)
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

        internal async Task<(HashSet<Guid>, State)> PartialDrainAsync(
            IAsyncEnumerator<TryCatch<Page>> enumerator,
            int numIterations)
        {
            HashSet<Guid> identifiers = new HashSet<Guid>();
            State state = default;

            // Drain a couple of iterations
            for (int i = 0; i < numIterations; i++)
            {
                await enumerator.MoveNextAsync();

                TryCatch<Page> tryGetPage = enumerator.Current;
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
