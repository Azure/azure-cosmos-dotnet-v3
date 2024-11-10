namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Skip;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class SkipQueryPipelineStageTests
    {
        [TestMethod]
        public async Task SanityTests()
        {
            long[] values = new long[] { 42, 1337 };
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages = values
                .Select(value => new List<CosmosElement>()
                {
                    CosmosElement.Parse($"{{\"item\": {value}}}")
                })
                .ToList();

            foreach (int offsetCount in new int[] { 0, 1, values.Length, 2 * values.Length })
            {
                List<CosmosElement> elements = await SkipQueryPipelineStageTests.CreateAndDrainAsync(
                    pages: pages,
                    offsetCount: offsetCount,
                    continuationToken: null);

                Assert.AreEqual(Math.Max(values.Length - offsetCount, 0), elements.Count);
            }
        }

        private static async Task<List<CosmosElement>> CreateAndDrainAsync(
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages,
            int offsetCount,
            CosmosElement continuationToken)
        {
            IQueryPipelineStage source = new MockQueryPipelineStage(pages);

            TryCatch<IQueryPipelineStage> tryCreateSkipQueryPipelineStage = SkipQueryPipelineStage.MonadicCreate(
                offsetCount: offsetCount,
                continuationToken: continuationToken,
                monadicCreatePipelineStage: (CosmosElement continuationToken) => TryCatch<IQueryPipelineStage>.FromResult(source));
            Assert.IsTrue(tryCreateSkipQueryPipelineStage.Succeeded);

            IQueryPipelineStage aggregateQueryPipelineStage = tryCreateSkipQueryPipelineStage.Result;

            List<CosmosElement> elements = new List<CosmosElement>();
            await foreach (TryCatch<QueryPage> page in new EnumerableStage(aggregateQueryPipelineStage, NoOpTrace.Singleton))
            {
                page.ThrowIfFailed();

                elements.AddRange(page.Result.Documents);
            }

            return elements;
        }
    }
}