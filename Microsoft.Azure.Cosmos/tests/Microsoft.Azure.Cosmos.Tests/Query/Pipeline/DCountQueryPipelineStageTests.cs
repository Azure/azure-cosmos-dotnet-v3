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
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.DCount;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    [TestClass]
    public sealed class DCountQueryPipelineStageTests
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

            int expectedCount = values.Distinct().Count();
            List<CosmosElement> elements = await CreateAndDrainAsync(
                pages: pages,
                executionEnvironment: ExecutionEnvironment.Compute,
                continuationToken: null,
                distinctQueryType: DistinctQueryType.Unordered,
                dcountAlias: null);

            Validate(expectedCount: expectedCount, actual: elements);

            elements = await CreateAndDrainWithoutStateAsync(
                pages: pages,
                executionEnvironment: ExecutionEnvironment.Client,
                continuationToken: null,
                distinctQueryType: DistinctQueryType.Unordered,
                dcountAlias: null);

            Validate(expectedCount: expectedCount, actual: elements);
        }

        private static void Validate(int expectedCount, IEnumerable<CosmosElement> actual)
        {
            Assert.AreEqual(expected: 1, actual: actual.Count());
            Assert.IsTrue(actual.First() is CosmosNumber);
            CosmosNumber result = actual.First() as CosmosNumber;
            Assert.AreEqual(expected: expectedCount, actual: Number64.ToLong(result.Value));
        }

        private static async Task<List<CosmosElement>> CreateAndDrainAsync(
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages,
            ExecutionEnvironment executionEnvironment,
            CosmosElement continuationToken,
            DistinctQueryType distinctQueryType,
            string dcountAlias)
        {
            List<CosmosElement> resultWithoutState = await CreateAndDrainWithoutStateAsync(
                pages: pages,
                executionEnvironment: executionEnvironment,
                continuationToken: continuationToken,
                distinctQueryType: distinctQueryType,
                dcountAlias: dcountAlias);

            List<CosmosElement> resultWithState = await CreateAndDrainWithStateAsync(
                pages: pages,
                executionEnvironment: executionEnvironment,
                continuationToken: continuationToken,
                distinctQueryType: distinctQueryType,
                dcountAlias: dcountAlias);

            Assert.IsTrue(resultWithoutState.SequenceEqual(resultWithState));

            return resultWithoutState;
        }

        private static async Task<List<CosmosElement>> CreateAndDrainWithoutStateAsync(
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages,
            ExecutionEnvironment executionEnvironment,
            CosmosElement continuationToken,
            DistinctQueryType distinctQueryType,
            string dcountAlias)
        {
            List<CosmosElement> elements = new List<CosmosElement>();
            IQueryPipelineStage stage = Create(
                pages: pages,
                executionEnvironment: executionEnvironment,
                requestContinuationToken: continuationToken,
                distinctQueryType: distinctQueryType,
                dcountAlias: dcountAlias);

            await foreach (TryCatch<QueryPage> page in new EnumerableStage(stage))
            {
                page.ThrowIfFailed();
                elements.AddRange(page.Result.Documents);
            }

            return elements;
        }

        private static async Task<List<CosmosElement>> CreateAndDrainWithStateAsync(
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages,
            ExecutionEnvironment executionEnvironment,
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
                    executionEnvironment: executionEnvironment,
                    requestContinuationToken: state,
                    distinctQueryType: distinctQueryType,
                    dcountAlias: dcountAlias);
                
                if(!await stage.MoveNextAsync())
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
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages,
            ExecutionEnvironment executionEnvironment,
            CosmosElement requestContinuationToken,
            DistinctQueryType distinctQueryType,
            string dcountAlias)
        {
            MonadicCreatePipelineStage source = (CosmosElement continuationToken, CancellationToken cancellationToken) =>
                TryCatch<IQueryPipelineStage>.FromResult(MockQueryPipelineStage.Create(pages, continuationToken));

            MonadicCreatePipelineStage createDistinctQueryPipelineStage = (CosmosElement continuationToken, CancellationToken cancellationToken) => 
                DistinctQueryPipelineStage.MonadicCreate(
                    executionEnvironment: executionEnvironment,
                    requestContinuation: continuationToken,
                    distinctQueryType: distinctQueryType,
                    cancellationToken: cancellationToken,
                    monadicCreatePipelineStage: source);

            TryCatch<IQueryPipelineStage> tryCreateDCountQueryPipelineStage = DCountQueryPipelineStage.MonadicCreate(
                executionEnvironment: executionEnvironment,
                continuationToken: requestContinuationToken,
                info: new DCountInfo { DCountAlias = dcountAlias },
                cancellationToken: default,
                monadicCreatePipelineStage: createDistinctQueryPipelineStage);
            Assert.IsTrue(tryCreateDCountQueryPipelineStage.Succeeded);

            return tryCreateDCountQueryPipelineStage.Result;
        }
    }
}
