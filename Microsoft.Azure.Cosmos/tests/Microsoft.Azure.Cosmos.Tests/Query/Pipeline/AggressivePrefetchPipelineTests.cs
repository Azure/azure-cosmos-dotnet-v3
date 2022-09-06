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
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AggressivePrefetchPipelineTests
    {
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private const int DocumentCount = 500;

        private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(25);

        private static readonly IReadOnlyList<CosmosObject> Documents = Enumerable
            .Range(1, DocumentCount)
            .Select(x => CosmosObject.Parse($"{{\"pk\" : {x} }}"))
            .ToList();

        [TestMethod]
        [Owner("ndeshpan")]
        public async Task BasicTests()
        {
            AggressivePrefetchTestCase[] testCases = new[]
            {
                MakeTest(
                    query: "SELECT VALUE COUNT(1) FROM c",
                    continuationCount: 3,
                    partitionCount: 3,
                    expectedDocument: CosmosNumber64.Create(DocumentCount)),
                MakeTest(
                    query: "SELECT VALUE MAX(c.pk) FROM c",
                    continuationCount: 3,
                    partitionCount: 3,
                    expectedDocument: CosmosNumber64.Create(DocumentCount)),
                MakeTest(
                    query: "SELECT VALUE MIN(c.pk) FROM c",
                    continuationCount: 3,
                    partitionCount: 3,
                    expectedDocument: CosmosNumber64.Create(1)),
                MakeTest(
                    query: "SELECT VALUE SUM(1) FROM c",
                    continuationCount: 3,
                    partitionCount: 3,
                    expectedDocument: CosmosNumber64.Create(DocumentCount))
            };

            foreach(AggressivePrefetchTestCase testCase in testCases)
            {
                await RunTest(testCase);
            }
        }

        private static AggressivePrefetchTestCase MakeTest(string query, int continuationCount, int partitionCount, CosmosElement expectedDocument)
        {
            return new AggressivePrefetchTestCase(
                query: query, 
                continuationCount: continuationCount,
                partitionCount: partitionCount,
                expectedDocument: expectedDocument);
        }

        private struct AggressivePrefetchTestCase
        {
            public string Query { get; }

            public int ContinuationCount { get; }

            public int PartitionCount { get; }

            public CosmosElement ExpectedDocument { get; }

            public AggressivePrefetchTestCase(
                string query,
                int continuationCount,
                int partitionCount,
                CosmosElement expectedDocument)
            {
                this.Query = query ?? throw new ArgumentNullException(nameof(query));
                this.ContinuationCount = continuationCount;
                this.PartitionCount = partitionCount;
                this.ExpectedDocument = expectedDocument ?? throw new ArgumentNullException(nameof(expectedDocument));
            }
        }

        private static async Task RunTest(AggressivePrefetchTestCase testCase)
        {
            CancellationTokenSource cts = new CancellationTokenSource(Timeout);

            int maxConcurrency = Convert.ToInt32(Math.Pow(2, testCase.PartitionCount));

            using MockDocumentContainer monadicDocumentContainer = new MockDocumentContainer(
                partitionKeyDefinition: FullPipelineTests.partitionKeyDefinition,
                continuationCount: testCase.ContinuationCount,
                maxConcurrency: maxConcurrency,
                cancellationToken: cts.Token);

            IDocumentContainer documentContainer = await FullPipelineTests.CreateDocumentContainerAsync(
                Documents,
                monadicDocumentContainer,
                testCase.PartitionCount);

            IReadOnlyList<FeedRangeEpk> feedRanges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cts.Token);
            Assert.AreEqual(maxConcurrency, feedRanges.Count);

            Task<List<CosmosElement>> resultTask = FullPipelineTests.DrainWithoutStateAsync(
                testCase.Query,
                documentContainer,
                pageSize: 10);

            for (int i = 0; i < testCase.ContinuationCount; i++)
            {
                while (monadicDocumentContainer.CountWaiters < maxConcurrency && !cts.IsCancellationRequested)
                {
                    await Task.Delay(PollingInterval);
                }

                monadicDocumentContainer.Release(maxConcurrency);
            }

            IReadOnlyList<CosmosElement> actualDocuments = await resultTask;
            actualDocuments.Should().HaveCount(1);
            actualDocuments.First().Should().Be(testCase.ExpectedDocument);
        }

        private sealed class MockDocumentContainer : InMemoryContainer, IDisposable
        {
            private readonly SemaphoreSlim semaphore;

            private readonly CancellationToken cancellationToken;

            private readonly int maxConcurrency;

            private readonly int continuationCount;

            private bool disposedValue;

            public int CountWaiters => this.maxConcurrency - this.semaphore.CurrentCount;

            public MockDocumentContainer(
                PartitionKeyDefinition partitionKeyDefinition,
                int continuationCount,
                int maxConcurrency,
                CancellationToken cancellationToken)
                : base(partitionKeyDefinition)
            {
                this.continuationCount = continuationCount;
                this.maxConcurrency = maxConcurrency;
                this.semaphore = new SemaphoreSlim(0, maxConcurrency);
                this.cancellationToken = cancellationToken;
            }

            public override async Task<TryCatch<QueryPage>> MonadicQueryAsync(
                SqlQuerySpec sqlQuerySpec,
                FeedRangeState<QueryState> feedRangeState,
                QueryPaginationOptions queryPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                await this.semaphore.WaitAsync(this.cancellationToken);

                int count = ParseQueryState(feedRangeState.State);

                QueryState continuationToken = count < this.continuationCount ? CreateQueryState(++count) : default;
                QueryPage page = new QueryPage(
                        documents: new List<CosmosElement> { },
                        requestCharge: 3.0,
                        activityId: "E7980B1F-436E-44DF-B7A5-655C56D38648",
                        responseLengthInBytes: 48,
                        cosmosQueryExecutionInfo: new Lazy<CosmosQueryExecutionInfo>(() => new CosmosQueryExecutionInfo(false, false)),
                        disallowContinuationTokenMessage: null,
                        additionalHeaders: null,
                        state: continuationToken);

                return continuationToken != default ?
                    TryCatch<QueryPage>.FromResult(page) :
                    await base.MonadicQueryAsync(
                        sqlQuerySpec,
                        new FeedRangeState<QueryState>(feedRangeState.FeedRange, default),
                        queryPaginationOptions,
                        trace,
                        cancellationToken);
            }

            public void Release(int count)
            {
                this.semaphore.Release(count);
            }

            private static int ParseQueryState(QueryState state)
            {
                if (state == default) return 0;

                CosmosObject parsedContinuationToken = CosmosObject.Parse(((CosmosString)state.Value).Value);
                int continuationCount = (int)Number64.ToLong(((CosmosNumber64)parsedContinuationToken["continuationCount"]).Value);
                return continuationCount;
            }

            private static QueryState CreateQueryState(int count)
            {
                return new QueryState(
                    CosmosString.Create(
                        CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "continuationCount", CosmosNumber64.Create(++count) },
                            })
                        .ToString()));
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
    }
}