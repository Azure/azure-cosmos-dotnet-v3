namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Query.Pipeline;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryPartitionRangePageAsyncEnumeratorTests
    {
        [TestMethod]
        public async Task Test429sAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.Test429sAsync(false);
        }

        [TestMethod]
        public async Task Test429sWithContinuationsAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.Test429sWithContinuationsAsync(false, false);
        }

        [TestMethod]
        public async Task TestDrainFullyAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestDrainFullyAsync(false);
        }

        [TestMethod]
        public async Task TestEmptyPages()
        {
            Implementation implementation = new Implementation();
            await implementation.TestEmptyPages(false);
        }

        [TestMethod]
        public async Task TestResumingFromStateAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestResumingFromStateAsync(false, false);
        }

        [TestMethod]
        public async Task TestSplitAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestSplitAsync();
        }

        [TestClass]
        private sealed class Implementation : PartitionRangeEnumeratorTests<QueryPage, QueryState>
        {
            public Implementation()
                : base(singlePartition: true)
            {
            }

            [TestMethod]
            public async Task TestSplitAsync()
            {
                int numItems = 100;
                IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems);
                IAsyncEnumerator<TryCatch<QueryPage>> enumerator = await this.CreateEnumeratorAsync(documentContainer);

                (HashSet<string> parentIdentifiers, QueryState state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

                // Split the partition
                await documentContainer.SplitAsync(new FeedRangePartitionKeyRange("0"), cancellationToken: default);

                // Try To read from the partition that is gone.
                await enumerator.MoveNextAsync();
                Assert.IsTrue(enumerator.Current.Failed);

                // Resume on the children using the parent continuaiton token
                HashSet<string> childIdentifiers = new HashSet<string>();

                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                List<FeedRangeEpk> ranges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton, 
                    cancellationToken: default);
                foreach (FeedRangeEpk range in ranges)
                {
                    IAsyncEnumerable<TryCatch<QueryPage>> enumerable = new PartitionRangePageAsyncEnumerable<QueryPage, QueryState>(
                        feedRangeState: new FeedRangeState<QueryState>(range, state),
                        (feedRangeState) => new QueryPartitionRangePageAsyncEnumerator(
                            queryDataSource: documentContainer,
                            sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                            feedRangeState: feedRangeState,
                            partitionKey: null,
                            containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                            queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                            cancellationToken: default),
                        trace: NoOpTrace.Singleton);
                    HashSet<string> resourceIdentifiers = await this.DrainFullyAsync(enumerable);

                    childIdentifiers.UnionWith(resourceIdentifiers);
                }

                Assert.AreEqual(numItems, parentIdentifiers.Count + childIdentifiers.Count);
            }

            public override IReadOnlyList<Record> GetRecordsFromPage(QueryPage page)
            {
                List<Record> records = new List<Record>(page.Documents.Count);
                foreach (CosmosElement element in page.Documents)
                {
                    CosmosObject document = (CosmosObject)element;
                    ResourceId resourceIdentifier = ResourceId.Parse(((CosmosString)document["_rid"]).Value);
                    long ticks = Number64.ToLong(((CosmosNumber)document["_ts"]).Value);
                    string identifer = ((CosmosString)document["id"]).Value;

                    records.Add(new Record(resourceIdentifier, new DateTime(ticks: ticks, DateTimeKind.Utc), identifer, document));
                }

                return records;
            }

            protected override IAsyncEnumerable<TryCatch<QueryPage>> CreateEnumerable(
                IDocumentContainer documentContainer,
                bool aggressivePrefetch = false,
                QueryState state = null)
            {
                List<FeedRangeEpk> ranges = documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton, 
                    cancellationToken: default).Result;
                Assert.AreEqual(1, ranges.Count);
                return new PartitionRangePageAsyncEnumerable<QueryPage, QueryState>(
                    feedRangeState: new FeedRangeState<QueryState>(ranges[0], state),
                    (feedRangeState) => new QueryPartitionRangePageAsyncEnumerator(
                        queryDataSource: documentContainer,
                        sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                        feedRangeState: feedRangeState,
                        partitionKey: null,
                        containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                        queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                        cancellationToken: default),
                    trace: NoOpTrace.Singleton);
            }

            protected override Task<IAsyncEnumerator<TryCatch<QueryPage>>> CreateEnumeratorAsync(
                IDocumentContainer documentContainer,
                bool aggressivePrefetch = false,
                bool exercisePrefetch = false,
                QueryState state = default,
                CancellationToken cancellationToken = default)
            {
                List<FeedRangeEpk> ranges = documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton, 
                    cancellationToken: default).Result;
                Assert.AreEqual(1, ranges.Count);

                IAsyncEnumerator<TryCatch<QueryPage>> enumerator = new TracingAsyncEnumerator<TryCatch<QueryPage>>(
                    enumerator: new QueryPartitionRangePageAsyncEnumerator(
                        queryDataSource: documentContainer,
                        sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                        feedRangeState: new FeedRangeState<QueryState>(ranges[0], state),
                        partitionKey: null,
                        containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                        queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                        cancellationToken: cancellationToken),
                    trace: NoOpTrace.Singleton);

                return Task.FromResult(enumerator);
            }
        }
    }
}
