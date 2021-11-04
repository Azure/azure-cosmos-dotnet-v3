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
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SkipEmptyPageQueryPipelineStageTests
    {
        [TestMethod]
        public async Task StackOverflowTest()
        {
            await using IQueryPipelineStage pipeline = CreatePipeline(Enumerable
                .Repeat(EmptyPagePipelineStage.PageType.Empty, 2000)
                .Concat(Enumerable.Repeat(EmptyPagePipelineStage.PageType.Error, 1))
                .ToList());
            bool hasNext = await pipeline.MoveNextAsync(NoOpTrace.Singleton);
            Assert.IsTrue(hasNext);
            TryCatch<QueryPage> result = pipeline.Current;
            Assert.IsFalse(result.Succeeded);
        }

        [TestMethod]
        public async Task BasicTests()
        {
            IReadOnlyList<TestCase> testCases = new List<TestCase>()
            {
                MakeTest(
                    input: Enumerable
                        .Repeat(EmptyPagePipelineStage.PageType.Empty, 2000)
                        .Concat(Enumerable.Repeat(EmptyPagePipelineStage.PageType.NonEmpty, 1)),
                    expected: Enumerable.Repeat(true, 1)),
                MakeTest(
                    input: Enumerable
                        .Repeat(EmptyPagePipelineStage.PageType.Empty, 100)
                        .Concat(Enumerable.Repeat(EmptyPagePipelineStage.PageType.NonEmpty, 5))
                        .Concat(Enumerable.Repeat(EmptyPagePipelineStage.PageType.Empty, 27))
                        .Concat(Enumerable.Repeat(EmptyPagePipelineStage.PageType.NonEmpty, 3))
                        .Concat(Enumerable.Repeat(EmptyPagePipelineStage.PageType.Empty, 32))
                        .Concat(Enumerable.Repeat(EmptyPagePipelineStage.PageType.NonEmpty, 3)),
                    expected: Enumerable.Repeat(true, 11)),
                MakeTest(
                    input: Enumerable
                        .Repeat(EmptyPagePipelineStage.PageType.Empty, 100)
                        .Concat(Enumerable.Repeat(EmptyPagePipelineStage.PageType.NonEmpty, 5))
                        .Concat(Enumerable.Repeat(EmptyPagePipelineStage.PageType.Empty, 27))
                        .Concat(Enumerable.Repeat(EmptyPagePipelineStage.PageType.Error, 3))
                        .Concat(Enumerable.Repeat(EmptyPagePipelineStage.PageType.Empty, 32))
                        .Concat(Enumerable.Repeat(EmptyPagePipelineStage.PageType.NonEmpty, 3)),
                    expected: Enumerable.Repeat(true, 5)
                        .Concat(Enumerable.Repeat(false, 3))
                        .Concat(Enumerable.Repeat(true, 3))),
                MakeTest(
                    input: Enumerable.Repeat(EmptyPagePipelineStage.PageType.NonEmpty, 500),
                    expected: Enumerable.Repeat(true, 500)),
                MakeTest(
                    input: Enumerable.Repeat(EmptyPagePipelineStage.PageType.Error, 500),
                    expected: Enumerable.Repeat(false, 500))
            };

            foreach (TestCase testCase in testCases)
            {
                await using IQueryPipelineStage pipeline = CreatePipeline(testCase.Input);
                for (int index = 0; index < testCase.Expected.Count; ++index)
                {
                    Assert.IsTrue(await pipeline.MoveNextAsync(NoOpTrace.Singleton));

                    if (testCase.Expected[index])
                    {
                        Assert.IsTrue(pipeline.Current.Succeeded);
                        Assert.AreEqual(1, pipeline.Current.Result.Documents.Count);
                        Assert.AreEqual("42", pipeline.Current.Result.Documents[0].ToString());
                    }
                    else
                    {
                        Assert.IsTrue(pipeline.Current.Failed);
                    }
                }
            }
        }

        internal static TestCase MakeTest(IEnumerable<EmptyPagePipelineStage.PageType> input, IEnumerable<bool> expected)
        {
            return new TestCase(input.ToList(), expected.ToList());
        }

        internal readonly struct TestCase
        {
            public TestCase(IReadOnlyList<EmptyPagePipelineStage.PageType> input, IReadOnlyList<bool> expected)
            {
                this.Input = input;
                this.Expected = expected;
            }

            public readonly IReadOnlyList<EmptyPagePipelineStage.PageType> Input { get; }

            public readonly IReadOnlyList<bool> Expected { get; }
        }

        private static IQueryPipelineStage CreatePipeline(IReadOnlyList<EmptyPagePipelineStage.PageType> pages)
        {
            EmptyPagePipelineStage emptyPagePipelineStage = new EmptyPagePipelineStage(pages);
            SkipEmptyPageQueryPipelineStage skipEmptyPageStage = new SkipEmptyPageQueryPipelineStage(
                inputStage: emptyPagePipelineStage,
                cancellationToken: default);

            return new CatchAllQueryPipelineStage(inputStage: skipEmptyPageStage, cancellationToken: default);
        }

        internal class EmptyPagePipelineStage : IQueryPipelineStage
        {
            public enum PageType { Empty, NonEmpty, Error };

            private static readonly TryCatch<QueryPage> Empty = TryCatch<QueryPage>.FromResult(new QueryPage(
                            documents: new List<CosmosElement>(),
                            requestCharge: 42,
                            activityId: Guid.NewGuid().ToString(),
                            responseLengthInBytes: "[]".Length,
                            cosmosQueryExecutionInfo: default,
                            disallowContinuationTokenMessage: default,
                            additionalHeaders: default,
                            state: new QueryState(CosmosString.Create("Empty"))));

            private static readonly TryCatch<QueryPage> NonEmpty = TryCatch<QueryPage>.FromResult(new QueryPage(
                documents: new List<CosmosElement> { CosmosElement.Parse("42") },
                            requestCharge: 100,
                            activityId: Guid.NewGuid().ToString(),
                            responseLengthInBytes: "[42]".Length,
                            cosmosQueryExecutionInfo: default,
                            disallowContinuationTokenMessage: default,
                            additionalHeaders: default,
                            state: new QueryState(CosmosString.Create("NonEmpty"))));

            private readonly IReadOnlyList<PageType> pages;

            private int current;

            public EmptyPagePipelineStage(IReadOnlyList<PageType> pages)
            {
                this.current = -1;
                this.pages = pages;
            }

            public TryCatch<QueryPage> Current { get; private set; }

            public ValueTask DisposeAsync()
            {
                return new ValueTask();
            }

            public ValueTask<bool> MoveNextAsync(ITrace trace)
            {
                ++this.current;
                if (this.current >= this.pages.Count)
                {
                    return new ValueTask<bool>(false);
                }

                switch (this.pages[this.current])
                {
                    case PageType.Empty:
                        this.Current = Empty;
                        break;
                    case PageType.NonEmpty:
                        this.Current = NonEmpty;
                        break;
                    case PageType.Error:
                        throw new CosmosException(
                            message: "Injected failure",
                            statusCode: System.Net.HttpStatusCode.InternalServerError,
                            subStatusCode: 0,
                            activityId: Guid.Empty.ToString(),
                            requestCharge: 0);
                }

                return new ValueTask<bool>(true);
            }

            public void SetCancellationToken(CancellationToken cancellationToken)
            {
            }
        }
    }
}