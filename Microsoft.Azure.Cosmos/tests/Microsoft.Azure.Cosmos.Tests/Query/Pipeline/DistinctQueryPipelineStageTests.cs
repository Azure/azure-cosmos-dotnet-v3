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
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class DistinctQueryPipelineStageTests
    {
        [TestMethod]
        public async Task SanityTests()
        {
            long[] values = new long[] { 42, 1337, 1337, 42 };
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages = values
                .Select(value => new List<CosmosElement>()
                {
                    CosmosElement.Parse($"{{\"item\": {value}}}")
                })
                .ToList();

            List<CosmosElement> elements = await DistinctQueryPipelineStageTests.CreateAndDrainAsync(
                pages: pages,
                executionEnvironment: ExecutionEnvironment.Compute,
                continuationToken: null,
                distinctQueryType: DistinctQueryType.Unordered);

            Assert.AreEqual(values.Distinct().Count(), elements.Count);
        }

        private static async Task<List<CosmosElement>> CreateAndDrainAsync(
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages,
            ExecutionEnvironment executionEnvironment,
            CosmosElement continuationToken,
            DistinctQueryType distinctQueryType)
        {
            IQueryPipelineStage source = new MockQueryPipelineStage(pages);

            TryCatch<IQueryPipelineStage> tryCreateDistinctQueryPipelineStage = DistinctQueryPipelineStage.MonadicCreate(
                executionEnvironment: executionEnvironment,
                requestContinuation: continuationToken,
                distinctQueryType: distinctQueryType,
                cancellationToken: default,
                monadicCreatePipelineStage: (CosmosElement continuationToken, CancellationToken cancellationToken) => TryCatch<IQueryPipelineStage>.FromResult(source));
            Assert.IsTrue(tryCreateDistinctQueryPipelineStage.Succeeded);

            IQueryPipelineStage distinctQueryPipelineStage = tryCreateDistinctQueryPipelineStage.Result;

            List<CosmosElement> elements = new List<CosmosElement>();
            await foreach (TryCatch<QueryPage> page in new EnumerableStage(distinctQueryPipelineStage))
            {
                page.ThrowIfFailed();

                elements.AddRange(page.Result.Documents);
            }

            return elements;
        }
    }
}
