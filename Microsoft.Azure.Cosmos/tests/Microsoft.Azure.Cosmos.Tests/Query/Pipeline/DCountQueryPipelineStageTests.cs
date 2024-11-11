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
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.DCount;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PageList = System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<Microsoft.Azure.Cosmos.CosmosElements.CosmosElement>>;

    [TestClass]
    public sealed class DCountQueryPipelineStageTests
    {
        static IReadOnlyList<(PageList, int, DistinctQueryType[])> TestCases()
        {
            List<(PageList, int, DistinctQueryType[])> result = new List<(PageList, int, DistinctQueryType[])>();

            long[] values = new long[] { 42, 1337, 1337, 42 };
            PageList pages = values
                .Select(value => new List<CosmosElement>() { CosmosElement.Parse($"{{\"item\": {value}}}") })
                .ToList();
            result.Add((pages, values.Distinct().Count(), new[] { DistinctQueryType.Unordered }));

            values = new long[] { 0, 1, 1, 2, 3, 5, 8, 13 };
            pages = values
                .Select(value => new List<CosmosElement>() { CosmosElement.Parse($"{{\"item\": {value}}}") })
                .ToList();
            result.Add((pages, values.Distinct().Count(), new[] { DistinctQueryType.Unordered, DistinctQueryType.Ordered }));

            int expectedCount = 50;
            pages = Enumerable
                .Range(0, 10)
                .Select(_ => Enumerable
                    .Range(0, expectedCount)
                    .Select(j => CosmosElement.Parse($"{{ \"val\": {j} }}"))
                    .ToList())
                .ToList();
            result.Add((pages, expectedCount, new[] { DistinctQueryType.Unordered }));

            pages = Enumerable
                .Range(0, expectedCount)
                .Select(i => Enumerable
                    .Repeat(i, 50)
                    .Select(j => CosmosElement.Parse($"{{ \"val\": {j} }}"))
                    .ToList())
                .ToList();
            result.Add((pages, expectedCount, new[] { DistinctQueryType.Unordered, DistinctQueryType.Ordered }));

            return result;
        }


        [TestMethod]
        public async Task SanityCheck()
        {
            IReadOnlyList<(PageList, int, DistinctQueryType[])> testCases = TestCases();

            foreach ((PageList pages, int expectedCount, DistinctQueryType[] distinctQueryTypes) in testCases)
            {
                foreach (string dcountAlias in new[] { null, string.Empty, "$1", "expectedCount" })
                {
                    foreach (DistinctQueryType distinctQueryType in distinctQueryTypes)
                    {
                        await Run(
                            pages: pages,
                            distinctQueryType: distinctQueryType,
                            dcountAlias: dcountAlias,
                            expectedCount: expectedCount);
                    }
                }
            }
        }

        private static async Task Run(
            PageList pages,
            DistinctQueryType distinctQueryType,
            string dcountAlias,
            int expectedCount)
        {
            List<CosmosElement> elementsCompute = await CreateAndDrainAsync(
                pages: pages,
                continuationToken: null,
                distinctQueryType: distinctQueryType,
                dcountAlias: dcountAlias);

            Validate(expectedCount: expectedCount, dcountAlias: dcountAlias, actual: elementsCompute);

            List<CosmosElement> elementsClient = await CreateAndDrainWithoutStateAsync(
                pages: pages,
                continuationToken: null,
                distinctQueryType: distinctQueryType,
                dcountAlias: dcountAlias);

            Assert.IsTrue(elementsClient.SequenceEqual(elementsCompute));

            Validate(expectedCount: expectedCount, dcountAlias: dcountAlias, actual: elementsClient);
        }

        private static void Validate(int expectedCount, string dcountAlias, IReadOnlyList<CosmosElement> actual)
        {
            Assert.AreEqual(expected: 1, actual: actual.Count());

            long actualCount = long.MaxValue;
            if (string.IsNullOrEmpty(dcountAlias))
            {
                if (actual.First() is CosmosNumber result)
                {
                    actualCount = Number64.ToLong(result.Value);
                }
                else
                {
                    Assert.Fail();
                }
            }
            else
            {
                CosmosNumber result = ((CosmosObject)actual[0])[dcountAlias] as CosmosNumber;
                actualCount = Number64.ToLong(result.Value);
            }

            Assert.AreEqual(expected: expectedCount, actual: actualCount);
        }

        private static async Task<List<CosmosElement>> CreateAndDrainAsync(
            PageList pages,
            CosmosElement continuationToken,
            DistinctQueryType distinctQueryType,
            string dcountAlias)
        {
            List<CosmosElement> resultWithoutState = await CreateAndDrainWithoutStateAsync(
                pages: pages,
                continuationToken: continuationToken,
                distinctQueryType: distinctQueryType,
                dcountAlias: dcountAlias);

            List<CosmosElement> resultWithState = await CreateAndDrainWithStateAsync(
                pages: pages,
                continuationToken: continuationToken,
                distinctQueryType: distinctQueryType,
                dcountAlias: dcountAlias);

            Assert.IsTrue(resultWithoutState.SequenceEqual(resultWithState));

            return resultWithoutState;
        }

        private static async Task<List<CosmosElement>> CreateAndDrainWithoutStateAsync(
            PageList pages,
            CosmosElement continuationToken,
            DistinctQueryType distinctQueryType,
            string dcountAlias)
        {
            List<CosmosElement> elements = new List<CosmosElement>();
            IQueryPipelineStage stage = Create(
                pages: pages,
                requestContinuationToken: continuationToken,
                distinctQueryType: distinctQueryType,
                dcountAlias: dcountAlias);

            await foreach (TryCatch<QueryPage> page in new EnumerableStage(stage, NoOpTrace.Singleton))
            {
                page.ThrowIfFailed();
                elements.AddRange(page.Result.Documents);
            }

            return elements;
        }

        private static async Task<List<CosmosElement>> CreateAndDrainWithStateAsync(
            PageList pages,
            CosmosElement continuationToken,
            DistinctQueryType distinctQueryType,
            string dcountAlias)
        {
            List<CosmosElement> elements = new List<CosmosElement>();
            CosmosElement state = continuationToken;

            do
            {
                IQueryPipelineStage stage = Create(
                    pages: pages,
                    requestContinuationToken: state,
                    distinctQueryType: distinctQueryType,
                    dcountAlias: dcountAlias);

                if (!await stage.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default))
                {
                    break;
                }

                TryCatch<QueryPage> tryGetQueryPage = stage.Current;
                tryGetQueryPage.ThrowIfFailed();

                elements.AddRange(tryGetQueryPage.Result.Documents);
                state = tryGetQueryPage.Result.State?.Value;
            } while (state != null);

            return elements;
        }

        private static IQueryPipelineStage Create(
            PageList pages,
            CosmosElement requestContinuationToken,
            DistinctQueryType distinctQueryType,
            string dcountAlias)
        {
            MonadicCreatePipelineStage source = (CosmosElement continuationToken) =>
                TryCatch<IQueryPipelineStage>.FromResult(MockQueryPipelineStage.Create(pages, continuationToken));

            MonadicCreatePipelineStage createDistinctQueryPipelineStage = (CosmosElement continuationToken) =>
                DistinctQueryPipelineStage.MonadicCreate(
                    requestContinuation: continuationToken,
                    distinctQueryType: distinctQueryType,
                    monadicCreatePipelineStage: source);

            TryCatch<IQueryPipelineStage> tryCreateDCountQueryPipelineStage = DCountQueryPipelineStage.MonadicCreate(
                continuationToken: requestContinuationToken,
                info: new DCountInfo { DCountAlias = dcountAlias },
                monadicCreatePipelineStage: createDistinctQueryPipelineStage);
            Assert.IsTrue(tryCreateDCountQueryPipelineStage.Succeeded);

            return tryCreateDCountQueryPipelineStage.Result;
        }
    }
}