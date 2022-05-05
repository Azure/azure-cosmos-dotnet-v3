namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Pagination;
    using Cosmos.Query.Core.Monads;
    using Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Cosmos.Query.Core.Pipeline.Pagination;
    using Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tests.Query.Pipeline;
    using Pagination;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OrderByQueryPartitionRangePageAsyncEnumeratorTests
    {
        [TestMethod]
        public async Task TestMoveNextAsyncThrowsTaskCanceledException()
        {
            Implementation implementation = new Implementation();
            await implementation.TestMoveNextAsyncThrowsTaskCanceledException();
        }

        [TestClass]
        private sealed class Implementation : PartitionRangeEnumeratorTests<OrderByQueryPage, QueryState>
        {
            public Implementation()
                : base(singlePartition: true)
            {
            }

            public override IReadOnlyList<Record> GetRecordsFromPage(OrderByQueryPage page)
            {
                throw new NotImplementedException();
            }

            protected override IAsyncEnumerable<TryCatch<OrderByQueryPage>> CreateEnumerable(
                IDocumentContainer documentContainer,
                bool aggressivePrefetch = false,
                QueryState state = null)
            {
                throw new NotImplementedException();
            }

            protected override IAsyncEnumerator<TryCatch<OrderByQueryPage>> CreateEnumerator(
                IDocumentContainer documentContainer,
                bool aggressivePrefetch = false,
                QueryState state = null,
                CancellationToken cancellationToken = default)
            {
                List<FeedRangeEpk> ranges = documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: cancellationToken).Result;
                Assert.AreEqual(1, ranges.Count);
                return new TracingAsyncEnumerator<TryCatch<OrderByQueryPage>>(
                    new OrderByQueryPartitionRangePageAsyncEnumerator(
                        queryDataSource: documentContainer,
                        sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                        feedRangeState: new FeedRangeState<QueryState>(ranges[0], state),
                        partitionKey: null,
                        queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                        filter: "filter",
                        cancellationToken: cancellationToken),
                    NoOpTrace.Singleton);
            }
        }
    }
}