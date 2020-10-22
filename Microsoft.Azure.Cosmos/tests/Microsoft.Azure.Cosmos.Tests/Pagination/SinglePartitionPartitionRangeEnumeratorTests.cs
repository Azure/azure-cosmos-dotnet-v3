//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;

    [TestClass]
    public sealed class SinglePartitionPartitionRangeEnumeratorTests
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

        [TestClass]
        private sealed class Implementation : PartitionRangeEnumeratorTests<ReadFeedPage, ReadFeedState>
        {
            public Implementation()
                : base(singlePartition: true)
            {
            }

            [TestMethod]
            public async Task TestSplitAsync()
            {
                int numItems = 100;
                IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
                IReadOnlyList<FeedRangeInternal> ranges = await inMemoryCollection.GetFeedRangesAsync(cancellationToken: default);
                Assert.AreEqual(1, ranges.Count);

                ReadFeedPartitionRangeEnumerator enumerator = new ReadFeedPartitionRangeEnumerator(
                    inMemoryCollection,
                    feedRange: ranges[0],
                    pageSize: 10,
                    queryRequestOptions: default,
                    cancellationToken: default);

                (HashSet<string> parentIdentifiers, ReadFeedState state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

                // Split the partition
                await inMemoryCollection.SplitAsync(ranges[0], cancellationToken: default);

                // Try To read from the partition that is gone.
                await enumerator.MoveNextAsync();
                Assert.IsTrue(enumerator.Current.Failed);

                // Resume on the children using the parent continuaiton token
                HashSet<string> childIdentifiers = new HashSet<string>();

                IReadOnlyList<FeedRangeInternal> childRanges = await inMemoryCollection.GetFeedRangesAsync(cancellationToken: default);
                foreach (FeedRangeInternal childRange in childRanges)
                {
                    PartitionRangePageAsyncEnumerable<ReadFeedPage, ReadFeedState> enumerable = new PartitionRangePageAsyncEnumerable<ReadFeedPage, ReadFeedState>(
                        range: childRange,
                        state: state,
                        (range, state) => new ReadFeedPartitionRangeEnumerator(
                                inMemoryCollection,
                                feedRange: childRange,
                                pageSize: 10,
                                state: state,
                                queryRequestOptions: default,
                                cancellationToken: default));
                    HashSet<string> resourceIdentifiers = await this.DrainFullyAsync(enumerable);

                    childIdentifiers.UnionWith(resourceIdentifiers);
                }

                Assert.AreEqual(numItems, parentIdentifiers.Count + childIdentifiers.Count);
            }

            public override IReadOnlyList<Record> GetRecordsFromPage(ReadFeedPage page)
            {
                return page.GetRecords();
            }

            public override IAsyncEnumerable<TryCatch<ReadFeedPage>> CreateEnumerable(
                IDocumentContainer documentContainer,
                ReadFeedState state = null) => new PartitionRangePageAsyncEnumerable<ReadFeedPage, ReadFeedState>(
                    range: new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                    state: state,
                    (range, state) => new ReadFeedPartitionRangeEnumerator(
                        documentContainer,
                        feedRange: range,
                        pageSize: 10,
                        state: state,
                        queryRequestOptions: default,
                        cancellationToken: default));

            public override IAsyncEnumerator<TryCatch<ReadFeedPage>> CreateEnumerator(
                IDocumentContainer inMemoryCollection,
                ReadFeedState state = null) => new ReadFeedPartitionRangeEnumerator(
                    inMemoryCollection,
                    feedRange: new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                    pageSize: 10,
                    state: state,
                    queryRequestOptions: default,
                    cancellationToken: default);
        }
    }
}
