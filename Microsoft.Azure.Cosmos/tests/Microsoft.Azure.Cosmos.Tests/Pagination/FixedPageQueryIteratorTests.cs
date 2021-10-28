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
    using Newtonsoft.Json;

    [TestClass]
    public class FixedPageQueryIteratorTests
    {
        private readonly IReadOnlyList<FixedPageQueryIteratorTestCase> TestCases = new List<FixedPageQueryIteratorTestCase>
        {
            MakeTest(
                responses: new List<TestFeedResponse>
                {
                    new TestFeedResponse(Enumerable.Repeat(1, 10), "1", 200, "a"),
                },
                expected: new List<(int FixedPageSize, IReadOnlyList<TestFeedResponse> ExpectedResponses)>
                {
                    (FixedPageSize: 5,
                    ExpectedResponses: new List<TestFeedResponse>
                    { 
                        new TestFeedResponse(
                            Enumerable.Repeat(1, 5),
                            new FixedPageQueryIterator<int>.ContinuationState(token: null, skip: 5).ToString(),
                            200,
                            "a\r\n"),
                        new TestFeedResponse(
                            Enumerable.Repeat(1, 5),
                            null,
                            0,
                            "a\r\n")
                    })
                }),
            MakeTest(
                responses: new List<TestFeedResponse>
                {
                    new TestFeedResponse(Enumerable.Repeat(1, 5), "1", 200, "a"),
                    new TestFeedResponse(Enumerable.Repeat(2, 5), null, 200, "b"),
                },
                expected: new List<(int FixedPageSize, IReadOnlyList<TestFeedResponse> ExpectedResponses)>
                {
                    (FixedPageSize: 10,
                    ExpectedResponses: new List<TestFeedResponse>
                    {
                        new TestFeedResponse(
                            Enumerable.Repeat(1, 5).Concat(Enumerable.Repeat(2, 5)),
                            null,
                            400,
                            "a\r\nb\r\n")
                    })
                }),
            MakeTest(
                responses: new List<TestFeedResponse>
                {
                    new TestFeedResponse(Enumerable.Repeat(1, 5), "1", 200, "a"),
                    new TestFeedResponse(Enumerable.Repeat(2, 3), "2", 100, "b"),
                    new TestFeedResponse(Enumerable.Repeat(3, 3), null, 150, "c"),
                },
                expected: new List<(int FixedPageSize, IReadOnlyList<TestFeedResponse> ExpectedResponses)>
                {
                    (FixedPageSize: 10,
                    ExpectedResponses: new List<TestFeedResponse>
                    {
                        new TestFeedResponse(
                            Enumerable
                                .Repeat(1, 5)
                                .Concat(Enumerable.Repeat(2, 3))
                                .Concat(Enumerable.Repeat(3, 2)),
                            new FixedPageQueryIterator<int>.ContinuationState(token: "2", skip: 2).ToString(),
                            450,
                            "a\r\nb\r\nc\r\n"),
                        new TestFeedResponse(Enumerable.Repeat(3, 1), null, 0, "c\r\n")
                    })
                }),
            MakeTest(
                responses: new List<TestFeedResponse>
                {
                    new TestFeedResponse(Enumerable.Repeat(1, 5), "1", 200, "a"),
                    new TestFeedResponse(Enumerable.Repeat(2, 3), null, 100, "b")
                },
                expected: new List<(int FixedPageSize, IReadOnlyList<TestFeedResponse> ExpectedResponses)>
                {
                    (FixedPageSize: 10,
                    ExpectedResponses: new List<TestFeedResponse>
                    {
                        new TestFeedResponse(
                            Enumerable
                                .Repeat(1, 5)
                                .Concat(Enumerable.Repeat(2, 3)),
                            null,
                            300,
                            "a\r\nb\r\n")
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
                Mock<Container> mockContainer = new Mock<Container>(MockBehavior.Strict);
                mockContainer.Setup(c => c
                    .GetItemQueryIterator<int>(queryDefinition, null, queryRequestOptions))
                    .Returns(testIterator);

                foreach ((int fixedPageSize, IReadOnlyList<TestFeedResponse> expectedResponses) in testCase.Expected)
                {
                    using FeedIterator<int> iterator = FixedPageQueryIterator<int>.Create(
                        mockContainer.Object,
                        queryDefinition,
                        queryRequestOptions,
                        null,
                        fixedPageSize);

                    string continuationToken = null;
                    string underlyingContinuationToken = null;
                    foreach (TestFeedResponse expectedResponse in expectedResponses)
                    {
                        Assert.IsTrue(iterator.HasMoreResults);
                        FeedResponse<int> response = await iterator.ReadNextAsync();
                        ValidateResponse(expected: expectedResponse, actual: response);

                        {
                            int underlyingPageIndex = 0;
                            Assert.IsTrue(underlyingContinuationToken == null || int.TryParse(underlyingContinuationToken, out underlyingPageIndex));

                            TestFeedIterator continuedIterator = new TestFeedIterator(testCase.Responses, underlyingPageIndex);
                            Mock<Container> continuedContainer = new Mock<Container>(MockBehavior.Strict);
                            continuedContainer.Setup(c => c
                                .GetItemQueryIterator<int>(queryDefinition, underlyingContinuationToken, queryRequestOptions))
                                .Returns(continuedIterator);

                            using FeedIterator<int> continuedFixedPageIterator = FixedPageQueryIterator<int>.Create(
                                continuedContainer.Object,
                                queryDefinition,
                                queryRequestOptions,
                                continuationToken,
                                fixedPageSize);

                            Assert.IsTrue(continuedFixedPageIterator.HasMoreResults);
                            FeedResponse<int> continuedResponse = await continuedFixedPageIterator.ReadNextAsync();
                            ValidateResponse(expected: expectedResponse, actual: continuedResponse, chargeOverride: continuedIterator.RequestCharge);
                        }

                        continuationToken = response.ContinuationToken;
                        underlyingContinuationToken = continuationToken != null ?
                            JsonConvert
                                .DeserializeObject<FixedPageQueryIterator<int>.ContinuationState>(continuationToken)
                                .ContinuationToken :
                            null;
                    }

                    Assert.IsFalse(iterator.HasMoreResults);
                }
            }
        }

        private static void ValidateResponse(FeedResponse<int> expected, FeedResponse<int> actual, double? chargeOverride = null)
        {
            Assert.AreEqual(expected.ActivityId, actual.ActivityId);
            Assert.AreEqual(expected.ContinuationToken, actual.ContinuationToken);
            Assert.AreEqual(expected.Count, actual.Count);
            Assert.AreEqual(expected.Diagnostics, actual.Diagnostics);
            Assert.AreEqual(expected.ETag, actual.ETag);
            Assert.AreEqual(expected.IndexMetrics, actual.IndexMetrics);
            Assert.AreEqual(expected.StatusCode, actual.StatusCode);
            CollectionAssert.AreEqual(expected.Resource.ToList(), actual.Resource.ToList());

            if (chargeOverride.HasValue)
            {
                Assert.AreEqual(chargeOverride, actual.RequestCharge);
            }
            else
            {
                Assert.AreEqual(expected.RequestCharge, actual.RequestCharge);
            }
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
                : this(responses: responses, continuationToken: 0)
            {
            }

            public TestFeedIterator(IReadOnlyList<TestFeedResponse> responses, int continuationToken)
            {
                this.Responses = responses ?? throw new ArgumentNullException(nameof(responses));
                this.Index = continuationToken;
                this.RequestCharge = 0;
            }

            public IReadOnlyList<TestFeedResponse> Responses { get; }

            public int Index { get; private set; }

            public override bool HasMoreResults => this.Index < this.Responses.Count;

            public double RequestCharge { get; private set; }

            public override Task<FeedResponse<int>> ReadNextAsync(CancellationToken cancellationToken = default)
            {
                if (!this.HasMoreResults)
                {
                    throw new InvalidOperationException("No more results");
                }

                this.RequestCharge += this.Responses[this.Index].RequestCharge;
                return Task.FromResult<FeedResponse<int>>(this.Responses[this.Index++]);
            }
        }
    }
}