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

    [TestClass]
    public class FixedPageQueryIteratorTests
    {
        private readonly IReadOnlyList<FixedPageQueryIteratorTestCase> TestCases = new List<FixedPageQueryIteratorTestCase>
        {
            MakeTest(
                new List<TestFeedResponse>
                {
                    new TestFeedResponse(Enumerable.Repeat(1, 10), "2", 200, "1"),
                },
                new List<(int FixedPageSize, IReadOnlyList<TestFeedResponse> ExpectedResponses)>
                {
                    (5,
                    new List<TestFeedResponse>
                    { 
                        new TestFeedResponse(
                            Enumerable.Repeat(1, 5),
                            new FixedPageQueryIterator<int>.ContinuationState(token: null, skip: 5).ToString(),
                            200,
                            "1\r\n")
                    })
                }),
            MakeTest(
                new List<TestFeedResponse>
                {
                    new TestFeedResponse(Enumerable.Repeat(1, 5), "2", 200, "1"),
                    new TestFeedResponse(Enumerable.Repeat(2, 5), null, 200, "2"),
                },
                new List<(int FixedPageSize, IReadOnlyList<TestFeedResponse> ExpectedResponses)>
                {
                    (10,
                    new List<TestFeedResponse>
                    {
                        new TestFeedResponse(
                            Enumerable.Repeat(1, 5).Concat(Enumerable.Repeat(2, 5)),
                            null,
                            400,
                            "1\r\n2\r\n")
                    })
                }),
            MakeTest(
                new List<TestFeedResponse>
                {
                    new TestFeedResponse(Enumerable.Repeat(1, 5), "2", 200, "1"),
                    new TestFeedResponse(Enumerable.Repeat(2, 3), "3", 100, "2"),
                    new TestFeedResponse(Enumerable.Repeat(3, 3), null, 150, "3"),
                },
                new List<(int FixedPageSize, IReadOnlyList<TestFeedResponse> ExpectedResponses)>
                {
                    (10,
                    new List<TestFeedResponse>
                    {
                        new TestFeedResponse(
                            Enumerable
                                .Repeat(1, 5)
                                .Concat(Enumerable.Repeat(2, 3))
                                .Concat(Enumerable.Repeat(3, 2)),
                            new FixedPageQueryIterator<int>.ContinuationState(token: "3", skip: 2).ToString(),
                            450,
                            "1\r\n2\r\n3\r\n")
                    })
                }),
            MakeTest(
                new List<TestFeedResponse>
                {
                    new TestFeedResponse(Enumerable.Repeat(1, 5), "2", 200, "1"),
                    new TestFeedResponse(Enumerable.Repeat(2, 3), null, 100, "2")
                },
                new List<(int FixedPageSize, IReadOnlyList<TestFeedResponse> ExpectedResponses)>
                {
                    (10,
                    new List<TestFeedResponse>
                    {
                        new TestFeedResponse(
                            Enumerable
                                .Repeat(1, 5)
                                .Concat(Enumerable.Repeat(2, 3)),
                            null,
                            300,
                            "1\r\n2\r\n")
                    })
                })
        };

        [TestMethod]
        [Owner("ndeshpan")]
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
                mockContainer.Setup(c => c
                    .GetItemQueryIterator<int>(queryDefinition, null, queryRequestOptions))
                    .Returns(testIterator);

                foreach ((int fixedPageSize, IReadOnlyList<TestFeedResponse> expectedResponses) in testCase.Expected)
                {
                    FeedIterator<int> iterator = FixedPageQueryIterator<int>.Create(
                        mockContainer.Object,
                        queryDefinition,
                        queryRequestOptions,
                        null,
                        fixedPageSize);

                    foreach (TestFeedResponse expectedResponse in expectedResponses)
                    {
                        Assert.IsTrue(iterator.HasMoreResults);
                        FeedResponse<int> response = await iterator.ReadNextAsync();
                        ValidateResponse(expected: expectedResponse, actual: response);
                    }

                    // TODO: fix up test cases above and uncomment this
                    // Assert.IsFalse(iterator.HasMoreResults);
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
            Assert.AreEqual(expected.RequestCharge, actual.RequestCharge);
            CollectionAssert.AreEqual(expected.Resource.ToList(), actual.Resource.ToList());
        }

        private static FixedPageQueryIteratorTestCase MakeTest(
            IReadOnlyList<TestFeedResponse> responses,
            IReadOnlyList<(int FixedPageSize, IReadOnlyList<TestFeedResponse> ExpectedResponses)> expected)
        {
            return new FixedPageQueryIteratorTestCase(responses, expected);
        }

        private readonly struct FixedPageQueryIteratorTestCase
        {
            public FixedPageQueryIteratorTestCase(
                IReadOnlyList<TestFeedResponse> responses,
                IReadOnlyList<(int FixedPageSize, IReadOnlyList<TestFeedResponse> ExpectedResponses)> expected)
            {
                this.Responses = responses ?? throw new ArgumentNullException(nameof(responses));
                this.Expected = expected ?? throw new ArgumentNullException(nameof(expected));
            }

            public IReadOnlyList<TestFeedResponse> Responses { get; }

            public IReadOnlyList<(int FixedPageSize, IReadOnlyList<TestFeedResponse> ExpectedResponses)> Expected { get; }
        }

        private class TestFeedResponse : FeedResponse<int>
        {
            private readonly double requestCharge;

            public TestFeedResponse(
                IEnumerable<int> page,
                string continuationToken,
                double requestCharge,
                string indexMetrics)
            {
                this.Page = page.ToList() ?? throw new ArgumentNullException(nameof(page));
                this.ContinuationToken = continuationToken;
                this.requestCharge = requestCharge;
                this.IndexMetrics = indexMetrics;
                this.StatusCode = HttpStatusCode.OK;
            }

            public IReadOnlyList<int> Page { get; }

            public override string ContinuationToken { get; }

            public override int Count => this.Page.Count;

            public override string IndexMetrics { get; }

            public override Headers Headers { get; }

            public override double RequestCharge => this.requestCharge;

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