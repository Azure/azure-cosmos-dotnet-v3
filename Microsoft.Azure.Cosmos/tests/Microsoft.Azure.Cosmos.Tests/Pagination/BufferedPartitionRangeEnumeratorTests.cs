namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;
    using System.Runtime.CompilerServices;
    using System;

    [TestClass]
    public sealed class BufferedPartitionPartitionRangeEnumeratorTests
    {
        [TestMethod]
        public async Task Test429sAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.Test429sAsync();
        }

        [TestMethod]
        public async Task Test429sWithContinuationsAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.Test429sWithContinuationsAsync();
        }

        [TestMethod]
        public async Task TestDrainFullyAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestDrainFullyAsync();
        }

        [TestMethod]
        public async Task TestEmptyPages()
        {
            Implementation implementation = new Implementation();
            await implementation.TestEmptyPages();
        }

        [TestMethod]
        public async Task TestResumingFromStateAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestResumingFromStateAsync();
        }

        [TestMethod]
        public async Task TestSplitAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestSplitAsync();
        }

        [TestMethod]
        public async Task TestBufferPageAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestBufferPageAsync();
        }

        [TestMethod]
        public async Task TestMoveNextAndBufferPageAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestMoveNextAndBufferPageAsync();
        }

        [TestClass]
        private sealed class Implementation : PartitionRangeEnumeratorTests<DocumentContainerPage, DocumentContainerState>
        {
            private static readonly int iterations = 1;

            public Implementation()
                : base(singlePartition: true)
            {
            }

            [TestMethod]
            public async Task TestSplitAsync()
            {
                int numItems = 100;
                IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
                IAsyncEnumerator<TryCatch<DocumentContainerPage>> enumerator = this.CreateEnumerator(inMemoryCollection);

                (HashSet<string> parentIdentifiers, DocumentContainerState state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

                // Split the partition
                await inMemoryCollection.SplitAsync(new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"), cancellationToken: default);

                // Try To read from the partition that is gone.
                await enumerator.MoveNextAsync();
                Assert.IsTrue(enumerator.Current.Failed);

                // Resume on the children using the parent continuaiton token
                HashSet<string> childIdentifiers = new HashSet<string>();
                foreach (int partitionKeyRangeId in new int[] { 1, 2 })
                {
                    PartitionRangePageAsyncEnumerable<DocumentContainerPage, DocumentContainerState> enumerable = new PartitionRangePageAsyncEnumerable<DocumentContainerPage, DocumentContainerState>(
                        range: new FeedRangePartitionKeyRange(partitionKeyRangeId: partitionKeyRangeId.ToString()),
                        state: state,
                        (range, state) => new DocumentContainerPartitionRangeEnumerator(
                                inMemoryCollection,
                                feedRange: range,
                                pageSize: 10,
                                cancellationToken: default,
                                state: state));
                    HashSet<string> resourceIdentifiers = await this.DrainFullyAsync(enumerable);

                    childIdentifiers.UnionWith(resourceIdentifiers);
                }

                Assert.AreEqual(numItems, parentIdentifiers.Count + childIdentifiers.Count);
            }

            [TestMethod]
            public async Task TestBufferPageAsync()
            {
                int numItems = 100;
                IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
                BufferedPartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState> enumerator = new BufferedPartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState>(
                    new DocumentContainerPartitionRangeEnumerator(
                        inMemoryCollection,
                        feedRange: new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                        pageSize: 10,
                        cancellationToken: default),
                    cancellationToken: default);

                int count = 0;

                for (int i = 0; i < 10; i++)
                {
                    // This call is idempotent;
                    await enumerator.PrefetchAsync(default);
                }

                Random random = new Random();
                while (await enumerator.MoveNextAsync())
                {
                    count += enumerator.Current.Result.Records.Count;
                    if (random.Next() % 2 == 0)
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            // This call is idempotent;
                            await enumerator.PrefetchAsync(default);
                        }
                    }
                }

                Assert.AreEqual(numItems, count);
            }

            [TestMethod]
            public async Task TestMoveNextAndBufferPageAsync()
            {
                int numItems = 100;
                IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);

                Random random = new Random();
                for (int iteration = 0; iteration < iterations; iteration++)
                {
                    BufferedPartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState> enumerator = new BufferedPartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState>(
                    new DocumentContainerPartitionRangeEnumerator(
                        inMemoryCollection,
                        feedRange: new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                        pageSize: 10,
                        cancellationToken: default),
                    cancellationToken: default);

                    if ((random.Next() % 2) == 0)
                    {
                        await enumerator.PrefetchAsync(default);
                    }

                    int count = 0;
                    while (await enumerator.MoveNextAsync())
                    {
                        count += enumerator.Current.Result.Records.Count;
                        
                        if ((random.Next() % 2) == 0)
                        {
                            await enumerator.PrefetchAsync(default);
                        }
                    }

                    Assert.AreEqual(numItems, count);
                }
            }

            public override IReadOnlyList<Record> GetRecordsFromPage(DocumentContainerPage page)
            {
                return page.Records;
            }

            public override IAsyncEnumerable<TryCatch<DocumentContainerPage>> CreateEnumerable(
                IDocumentContainer documentContainer,
                DocumentContainerState state = null) => new PartitionRangePageAsyncEnumerable<DocumentContainerPage, DocumentContainerState>(
                    range: new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                    state: state,
                    (range, state) => new BufferedPartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState>(
                        new DocumentContainerPartitionRangeEnumerator(
                            documentContainer,
                            feedRange: range,
                            pageSize: 10,
                            cancellationToken: default,
                            state: state),
                        cancellationToken: default));

            public override IAsyncEnumerator<TryCatch<DocumentContainerPage>> CreateEnumerator(
                IDocumentContainer inMemoryCollection,
                DocumentContainerState state = null) => new BufferedPartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState>(
                    new DocumentContainerPartitionRangeEnumerator(
                        inMemoryCollection,
                        feedRange: new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                        pageSize: 10,
                        cancellationToken: default,
                        state: state),
                    cancellationToken: default);

            private async Task BufferMoreInBackground(BufferedPartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState> enumerator)
            {
                while (true)
                {
                    await enumerator.PrefetchAsync(default);
                    await Task.Delay(10);
                }
            }
        }
    }
}
