//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AggressivePrefetchPipelineTests
    {
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

        private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(25);

        [TestMethod]
        [Owner("ndeshpan")]
        public async Task BasicTests()
        {

        }

        private AggressivePrefetchTestCase MakeTest(string query, IReadOnlyList<CosmosObject> documents, MonadicQueryDelegate queryDelegate, CosmosElement expectedDocument)
        {
            return new AggressivePrefetchTestCase(query, documents, queryDelegate, expectedDocument);
        }

        private struct AggressivePrefetchTestCase
        {
            public string Query { get; }

            public IReadOnlyList<CosmosObject> Documents { get; }

            public int CountPartitions { get; }

            public MonadicQueryDelegate QueryDelegate { get; }

            public CosmosElement ExpectedDocument { get; }

            public AggressivePrefetchTestCase(
                string query,
                IReadOnlyList<CosmosObject> documents,
                int countPartitions,
                MonadicQueryDelegate queryDelegate,
                CosmosElement expectedDocument)
            {
                this.Query = query ?? throw new ArgumentNullException(nameof(query));
                this.Documents = documents ?? throw new ArgumentNullException(nameof(documents));
                this.CountPartitions = countPartitions;
                this.QueryDelegate = queryDelegate ?? throw new ArgumentNullException(nameof(queryDelegate));
                this.ExpectedDocument = expectedDocument ?? throw new ArgumentNullException(nameof(expectedDocument));
            }
        }

        private static async Task RunTest(AggressivePrefetchTestCase testCase)
        {
            CancellationTokenSource cts = new CancellationTokenSource(Timeout);

            MockQueryDataSource queryDataSource = new MockQueryDataSource(
                testCase.QueryDelegate,
                testCase.CountPartitions,
                cts.Token);

            IMonadicDocumentContainer monadicDocumentContainer = new MockDocumentContainer(
                FullPipelineTests.partitionKeyDefinition,
                queryDataSource);

            IDocumentContainer documentContainer = await FullPipelineTests.CreateDocumentContainerAsync(
                testCase.Documents,
                monadicDocumentContainer,
                testCase.CountPartitions);

            Task<List<CosmosElement>> resultTask = FullPipelineTests.ExecuteQueryAsync(
                testCase.Query,
                testCase.Documents,
                documentContainer);

            while (queryDataSource.CountWaiters < testCase.CountPartitions && !cts.IsCancellationRequested)
            {
                await Task.Delay(PollingInterval);
            }

            queryDataSource.Release(testCase.CountPartitions);

            IReadOnlyList<CosmosElement> actualDocuments = await resultTask;
            actualDocuments.Should().HaveCount(1);
            actualDocuments.First().Should().Be(testCase.ExpectedDocument);
        }

        private delegate Task<TryCatch<QueryPage>> MonadicQueryDelegate(
            SqlQuerySpec sqlQuerySpec,
            FeedRangeState<QueryState> feedRangeState,
            QueryPaginationOptions queryPaginationOptions,
            ITrace trace,
            CancellationToken cancellationToken);

        private sealed class MockQueryDataSource : IMonadicQueryDataSource, IDisposable
        {
            private readonly SemaphoreSlim semaphore;

            private readonly CancellationToken cancellationToken;

            private readonly int maxConcurrency;

            private readonly int continuationCount;

            private bool disposedValue;

            public MonadicQueryDelegate QueryDelegate { get; }

            public int CountWaiters => this.maxConcurrency - this.semaphore.CurrentCount;

            public MockQueryDataSource(
                MonadicQueryDelegate queryDelegate,
                int continuationCount,
                int maxConcurrency,
                CancellationToken cancellationToken)
            {
                this.continuationCount = continuationCount;
                this.maxConcurrency = maxConcurrency;
                this.semaphore = new SemaphoreSlim(0, maxConcurrency);
                this.cancellationToken = cancellationToken;
                this.QueryDelegate = queryDelegate ?? throw new ArgumentNullException(nameof(queryDelegate));
            }

            public void Release(int count)
            {
                this.semaphore.Release(count);
            }

            public async Task<TryCatch<QueryPage>> MonadicQueryAsync(
                SqlQuerySpec sqlQuerySpec,
                FeedRangeState<QueryState> feedRangeState,
                QueryPaginationOptions queryPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                await this.semaphore.WaitAsync(this.cancellationToken);

                Number64 continuationToken = feedRangeState.State.Value as Number64;

                return await this.QueryDelegate(sqlQuerySpec, feedRangeState, queryPaginationOptions, trace, cancellationToken);
            }

            private void Dispose(bool disposing)
            {
                if (!this.disposedValue)
                {
                    if (disposing)
                    {
                        this.semaphore.Dispose();
                    }

                    this.disposedValue = true;
                }
            }

            public void Dispose()
            {
                // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
                this.Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        private sealed class MockDocumentContainer : InMemoryContainer
        {
            private readonly IMonadicQueryDataSource queryDataSource;

            public MockDocumentContainer(PartitionKeyDefinition partitionKeyDefinition, IMonadicQueryDataSource queryDataSource)
                : base(partitionKeyDefinition)
            {
                this.queryDataSource = queryDataSource ?? throw new ArgumentNullException(nameof(queryDataSource));
            }

            public override Task<TryCatch<QueryPage>> MonadicQueryAsync(
                SqlQuerySpec sqlQuerySpec,
                FeedRangeState<QueryState> feedRangeState,
                QueryPaginationOptions queryPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return this.queryDataSource.MonadicQueryAsync(sqlQuerySpec, feedRangeState, queryPaginationOptions, trace, cancellationToken);
            }
        }
    }
}