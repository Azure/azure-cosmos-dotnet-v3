//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    public class FixedPageQueryIteratorTests
    {
        private readonly IReadOnlyList<FixedPageQueryIteratorTestCase> TestCases = new List<FixedPageQueryIteratorTestCase>
        {

        };

        public async Task BasicTestsAsync()
        {
            QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c");
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                ConsistencyLevel = ConsistencyLevel.Strong,
                MaxConcurrency = 32
            };

            foreach (FixedPageQueryIteratorTestCase testCase in this.TestCases)
            {
                TestFeedIterator testIterator = new TestFeedIterator(testCase.Responses);
                Mock<Container> mockContainer = new Mock<Container>();
                mockContainer.Setup(c =>c
                    .GetItemQueryIterator<int>(queryDefinition, null, queryRequestOptions))
                    .Returns(testIterator);

                foreach ((int fixedPageSize, IReadOnlyList<int> testFeedIteratorIndexes) in testCase.Expected)
                {
                    FeedIterator<int> iterator = FixedPageQueryIterator<int>.Create(
                        mockContainer.Object,
                        queryDefinition,
                        queryRequestOptions,
                        null,
                        fixedPageSize);

                    foreach (int expectedIndex in testFeedIteratorIndexes)
                    {
                        Assert.IsTrue(iterator.HasMoreResults);
                        FeedResponse<int> response = await iterator.ReadNextAsync();
                        Assert.AreEqual(expectedIndex, testIterator.Index);
                        ValidateResponse(expected: testCase.Responses[expectedIndex], actual: response);
                    }
                }
            }
        }

        private static void ValidateResponse(FeedResponse<int> expected, FeedResponse<int> actual)
        {
            Assert.AreEqual(expected.ActivityId, actual.ActivityId);
            Assert.AreEqual(expected.ContinuationToken, actual.ContinuationToken);
            Assert.AreEqual(expected.Count, actual.Count);
            Assert.AreEqual(expected.Diagnostics, actual.Diagnostics);
            Assert.AreEqual(expected.ETag, actual.ETag);
            Assert.AreEqual(expected.IndexMetrics, actual.IndexMetrics);
            Assert.AreEqual(expected.StatusCode, actual.StatusCode);
            CollectionAssert.AreEqual(expected.Resource.ToList(), actual.Resource.ToList());
        }

        private FixedPageQueryIteratorTestCase MakeTest(
            IReadOnlyList<TestFeedResponse> responses,
            IReadOnlyList<(int FixedPageSize, IReadOnlyList<int> TestFeedIteratorIndexes)> expected)
        {
            return new FixedPageQueryIteratorTestCase(responses, expected);
        }

        private readonly struct FixedPageQueryIteratorTestCase
        {
            public FixedPageQueryIteratorTestCase(
                IReadOnlyList<TestFeedResponse> responses,
                IReadOnlyList<(int FixedPageSize, IReadOnlyList<int> TestFeedIteratorIndexes)> expected)
            {
                this.Responses = responses ?? throw new ArgumentNullException(nameof(responses));
                this.Expected = expected ?? throw new ArgumentNullException(nameof(expected));
            }

            public IReadOnlyList<TestFeedResponse> Responses { get; }

            public IReadOnlyList<(int FixedPageSize, IReadOnlyList<int> TestFeedIteratorIndexes)> Expected { get; }
        }

        private class TestFeedResponse : FeedResponse<int>
        {
            public TestFeedResponse(
                IReadOnlyList<int> page,
                string continuationToken,
                string indexMetrics,
                HttpStatusCode statusCode,
                CosmosDiagnostics diagnostics)
            {
                this.Page = page ?? throw new ArgumentNullException(nameof(page));
                this.ContinuationToken = continuationToken ?? throw new ArgumentNullException(nameof(continuationToken));
                this.IndexMetrics = indexMetrics ?? throw new ArgumentNullException(nameof(indexMetrics));
                this.StatusCode = statusCode;
                this.Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            }

            public IReadOnlyList<int> Page { get; }

            public override string ContinuationToken { get; }

            public override int Count => this.Page.Count;

            public override string IndexMetrics { get; }

            public override Headers Headers { get; }

            public override IEnumerable<int> Resource => this.Page;

            public override HttpStatusCode StatusCode { get; }

            public override CosmosDiagnostics Diagnostics { get; }

            public override IEnumerator<int> GetEnumerator()
            {
                return this.Page.GetEnumerator();
            }
        }

        private class TestFeedIterator : FeedIterator<int>
        {
            public TestFeedIterator(IReadOnlyList<TestFeedResponse> responses)
            {
                this.Responses = responses ?? throw new ArgumentNullException(nameof(responses));
            }

            public IReadOnlyList<TestFeedResponse> Responses { get; }

            public int Index { get; set; }

            public override bool HasMoreResults => this.Index < this.Responses.Count;

            public override Task<FeedResponse<int>> ReadNextAsync(CancellationToken cancellationToken = default)
            {
                if (!this.HasMoreResults)
                {
                    throw new InvalidOperationException("No more results");
                }

                return Task.FromResult<FeedResponse<int>>(this.Responses[this.Index++]);
            }
        }
    }
}