//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using System.Threading;
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.RecordIO;
    using System.Linq;

    [TestClass]
    public class InMemoryCollectionPartitionRangeEnumeratorTests
    {
        [TestMethod]
        public async Task TestDrainFullyAsync()
        {
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection();
            IAsyncEnumerable<TryCatch<Page>> enumerable = new PageEnumeratorToEnumerableAdaptor(() =>
            {
                return new InMemoryCollectionPartitionRangeEnumerator(
                    inMemoryCollection,
                    partitionKeyRangeId: 0,
                    pageSize: 10);
            });

            List<InMemoryCollection.Record> records = new List<InMemoryCollection.Record>();
            await foreach (TryCatch<Page> tryGetPage in enumerable)
            {
                tryGetPage.ThrowIfFailed();

                if (!(tryGetPage.Result is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage page))
                {
                    throw new InvalidCastException();
                }

                records.AddRange(page.Records);
            }

            Assert.AreEqual(100, new HashSet<long>(records.Select(record => record.ResourceIdentifier)).Count);
        }

        [TestMethod]
        public async Task TestResumingFromState()
        {
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection();
            InMemoryCollectionPartitionRangeEnumerator enumerator = new InMemoryCollectionPartitionRangeEnumerator(
                    inMemoryCollection,
                    partitionKeyRangeId: 0,
                    pageSize: 10);

            List<InMemoryCollection.Record> records = new List<InMemoryCollection.Record>();
            State state = default;

            // Drain a couple of iterations
            for(int i = 0; i < 3; i++)
            {
                await enumerator.MoveNextAsync();

                TryCatch<Page> tryGetPage = enumerator.Current;
                tryGetPage.ThrowIfFailed();

                if (!(tryGetPage.Result is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage page))
                {
                    throw new InvalidCastException();
                }

                records.AddRange(page.Records);
                state = enumerator.State;
            }

            // Resume from state
            enumerator = new InMemoryCollectionPartitionRangeEnumerator(
                inMemoryCollection,
                partitionKeyRangeId: 0,
                pageSize: 10,
                state: state);

            IAsyncEnumerable<TryCatch<Page>> enumerable = new PageEnumeratorToEnumerableAdaptor(() => enumerator);
            await foreach (TryCatch<Page> tryGetPage in enumerable)
            {
                tryGetPage.ThrowIfFailed();

                if (!(tryGetPage.Result is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage page))
                {
                    throw new InvalidCastException();
                }

                records.AddRange(page.Records);
            }

            Assert.AreEqual(100, new HashSet<long>(records.Select(record => record.ResourceIdentifier)).Count);
        }

        [TestMethod]
        public async Task TestSplitAsync()
        {
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection();
            InMemoryCollectionPartitionRangeEnumerator enumerator = new InMemoryCollectionPartitionRangeEnumerator(
                    inMemoryCollection,
                    partitionKeyRangeId: 0,
                    pageSize: 10);

            List<InMemoryCollection.Record> records = new List<InMemoryCollection.Record>();
            State state = default;

            // Drain a couple of iterations
            for (int i = 0; i < 3; i++)
            {
                await enumerator.MoveNextAsync();

                TryCatch<Page> tryGetPage = enumerator.Current;
                tryGetPage.ThrowIfFailed();

                if (!(tryGetPage.Result is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage page))
                {
                    throw new InvalidCastException();
                }

                records.AddRange(page.Records);
                state = enumerator.State;
            }

            inMemoryCollection.Split(partitionKeyRangeId: 0);

            // Try To read from the partition that is gone.
            await enumerator.MoveNextAsync();
            Assert.IsTrue(enumerator.Current.Failed);

            // Resume on the children using the parent continuaiton token
            InMemoryCollectionPartitionRangeEnumerator enumerator1 = new InMemoryCollectionPartitionRangeEnumerator(
                inMemoryCollection,
                partitionKeyRangeId: 1,
                pageSize: 10,
                state: state);

            InMemoryCollectionPartitionRangeEnumerator enumerator2 = new InMemoryCollectionPartitionRangeEnumerator(
                inMemoryCollection,
                partitionKeyRangeId: 2,
                pageSize: 10,
                state: state);

            IAsyncEnumerable<TryCatch<Page>> enumerable;
            enumerable = new PageEnumeratorToEnumerableAdaptor(() => enumerator1);
            await foreach (TryCatch<Page> tryGetPage in enumerable)
            {
                tryGetPage.ThrowIfFailed();

                if (!(tryGetPage.Result is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage page))
                {
                    throw new InvalidCastException();
                }

                records.AddRange(page.Records);
            }

            enumerable = new PageEnumeratorToEnumerableAdaptor(() => enumerator2);
            await foreach (TryCatch<Page> tryGetPage in enumerable)
            {
                tryGetPage.ThrowIfFailed();

                if (!(tryGetPage.Result is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage page))
                {
                    throw new InvalidCastException();
                }

                records.AddRange(page.Records);
            }

            Assert.AreEqual(100, new HashSet<long>(records.Select(record => record.ResourceIdentifier)).Count);
        }

        private static InMemoryCollection CreateInMemoryCollection()
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

            InMemoryCollection inMemoryCollection = new InMemoryCollection(partitionKeyDefinition);

            int numItemsToInsert = 100;
            for (int i = 0; i < numItemsToInsert; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                inMemoryCollection.CreateItem(item);
            }

            return inMemoryCollection;
        }

        private sealed class PageEnumeratorToEnumerableAdaptor : IAsyncEnumerable<TryCatch<Page>>
        {
            private readonly Func<IAsyncEnumerator<TryCatch<Page>>> factory;

            public PageEnumeratorToEnumerableAdaptor(Func<IAsyncEnumerator<TryCatch<Page>>> factory)
            {
                this.factory = factory;
            }

            public IAsyncEnumerator<TryCatch<Page>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return this.factory();
            }
        }
    }
}
