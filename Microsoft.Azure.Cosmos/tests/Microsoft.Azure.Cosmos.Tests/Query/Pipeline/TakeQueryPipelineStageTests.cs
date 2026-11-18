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
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Take;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PageList = System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<Microsoft.Azure.Cosmos.CosmosElements.CosmosElement>>;

    [TestClass]
    public sealed class TakeQueryPipelineStageTests
    {
        [TestMethod]
        public async Task SanityTests()
        {
            long[] values = new long[] { 42, 1337 };
            PageList pages = values
                .Select(value => new List<CosmosElement>()
                {
                    CosmosElement.Parse($"{{\"item\": {value}}}")
                })
                .ToList();

            foreach (int takeCount in new int[] { 0, 1, values.Length, 2 * values.Length })
            {
                (List<CosmosElement> elements, _) = await TakeQueryPipelineStageTests.CreateAndDrainAsync(
                    pages: pages,
                    takeCount: takeCount,
                    continuationToken: null);

                Assert.AreEqual(Math.Min(takeCount, values.Length), elements.Count);
            }
        }

        static IReadOnlyList<(PageList, int, long)> TestCases()
        {
            // List<(pages, takeCount, pageIndex)>
            List<(PageList, int, long)> result = new List<(PageList, int, long)>();

            long[] values = new long[] { 0, 1, 13, 42, 1337, 1337, 42, 1, 2, 3 };
            PageList pages = Enumerable
                .Range(0, 10)
                .Select(_ =>
                    values
                    .Select(value => CosmosElement.Parse($"{{\"item\": {value}}}"))
                    .ToList())
                .ToList();

            foreach (int takeCount in new[] { 1, 3, 6, 10, 15, 20, 23, 32, 41, 56, 81, 97, 100 })
            {
                long pageIndex = takeCount < values.Length ? 1 : Convert.ToInt64(Math.Ceiling((decimal)takeCount / values.Length));
                result.Add((pages, takeCount, pageIndex));
            }

            return result;
        }


        [TestMethod]
        public async Task BasicTests()
        {
            foreach ((PageList pages, int takeCount, long expectedPageIndex) in TestCases())
            {
                (List<CosmosElement> elements, long pageIndex) = await TakeQueryPipelineStageTests.CreateAndDrainAsync(
                    pages: pages,
                    takeCount: takeCount,
                    continuationToken: null);

                Assert.AreEqual(expectedPageIndex, pageIndex);
                Assert.AreEqual(Math.Min(takeCount, pages.Select(x => x.Count).Sum()), elements.Count);
            }
        }

        private static async Task<(List<CosmosElement>, long)> CreateAndDrainAsync(
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages,
            int takeCount,
            CosmosElement continuationToken)
        {
            MockQueryPipelineStage source = new MockQueryPipelineStage(pages);

            TryCatch<IQueryPipelineStage> tryCreateSkipQueryPipelineStage = TakeQueryPipelineStage.MonadicCreateLimitStage(
                limitCount: takeCount,
                requestContinuationToken: continuationToken,
                monadicCreatePipelineStage: (CosmosElement continuationToken) => TryCatch<IQueryPipelineStage>.FromResult(source));
            Assert.IsTrue(tryCreateSkipQueryPipelineStage.Succeeded);

            IQueryPipelineStage takeQueryPipelineStage = tryCreateSkipQueryPipelineStage.Result;

            List<CosmosElement> elements = new List<CosmosElement>();
            await foreach (TryCatch<QueryPage> page in new EnumerableStage(takeQueryPipelineStage, NoOpTrace.Singleton))
            {
                page.ThrowIfFailed();

                elements.AddRange(page.Result.Documents);
            }

            return (elements, source.PageIndex);
        }
    }
}