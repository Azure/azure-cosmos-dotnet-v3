namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Take;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class TakeQueryPipelineStageTests
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

            foreach (int takeCount in new int[] { 0, 1, values.Length, 2 * values.Length })
            {
                List<CosmosElement> elements = await TakeQueryPipelineStageTests.CreateAndDrainAsync(
                    pages: pages,
                    executionEnvironment: ExecutionEnvironment.Compute,
                    takeCount: takeCount,
                    continuationToken: null);

                Assert.AreEqual(Math.Min(takeCount, values.Length), elements.Count);
            }
        }

        private static async Task<List<CosmosElement>> CreateAndDrainAsync(
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages,
            ExecutionEnvironment executionEnvironment,
            int takeCount,
            CosmosElement continuationToken)
        {
            IQueryPipelineStage source = new MockQueryPipelineStage(pages);

            TryCatch<IQueryPipelineStage> tryCreateSkipQueryPipelineStage = await TakeQueryPipelineStage.TryCreateLimitStageAsync(
                executionEnvironment: executionEnvironment,
                limitCount: takeCount,
                requestContinuationToken: continuationToken,
                tryCreateSourceAsync: (CosmosElement continuationToken) => Task.FromResult(TryCatch<IQueryPipelineStage>.FromResult(source)));
            Assert.IsTrue(tryCreateSkipQueryPipelineStage.Succeeded);

            IQueryPipelineStage takeQueryPipelineStage = tryCreateSkipQueryPipelineStage.Result;

            List<CosmosElement> elements = new List<CosmosElement>();
            await foreach (TryCatch<QueryPage> page in new EnumerableStage(takeQueryPipelineStage))
            {
                page.ThrowIfFailed();

                elements.AddRange(page.Result.Documents);
            }

            return elements;
        }
    }
}
