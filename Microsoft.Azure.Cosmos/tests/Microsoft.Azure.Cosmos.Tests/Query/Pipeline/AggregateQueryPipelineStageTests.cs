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
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AggregateQueryPipelineStageTests
    {
        [TestMethod]
        public async Task SinglePageAsync()
        {
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages = new List<List<CosmosElement>>()
            {
                new List<CosmosElement>()
                {
                    CosmosElement.Parse("{\"payload\": {\"$1\": {\"item\": 42}}}")
                }
            };

            List<CosmosElement> elements = await AggregateQueryPipelineStageTests.CreateAndDrain(
                pages: pages,
                aggregates: new List<AggregateOperator>() { AggregateOperator.Sum },
                aliasToAggregateType: new Dictionary<string, AggregateOperator?>() { { "$1", AggregateOperator.Sum } },
                orderedAliases: new List<string>() { "$1" },
                hasSelectValue: false,
                continuationToken: null);

            Assert.AreEqual(1, elements.Count);
            Assert.AreEqual(42, Number64.ToLong(((elements[0] as CosmosObject)["$1"] as CosmosNumber).Value));
        }

        [TestMethod]
        public async Task MultiplePagesAsync()
        {
            long[] values = new long[] { 42, 1337 };
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages = values
                .Select(value => new List<CosmosElement>()
                {
                    CosmosElement.Parse($"{{\"payload\": {{\"$1\": {{\"item\": {value}}}}}}}")
                })
                .ToList();

            List<CosmosElement> elements = await AggregateQueryPipelineStageTests.CreateAndDrain(
                pages: pages,
                aggregates: new List<AggregateOperator>() { AggregateOperator.Sum },
                aliasToAggregateType: new Dictionary<string, AggregateOperator?>() { { "$1", AggregateOperator.Sum } },
                orderedAliases: new List<string>() { "$1" },
                hasSelectValue: false,
                continuationToken: null);

            Assert.AreEqual(1, elements.Count);
            Assert.AreEqual(values.Sum(), Number64.ToLong(((elements[0] as CosmosObject)["$1"] as CosmosNumber).Value));
        }

        [TestMethod]
        public async Task UndefinedSinglePageAsync()
        {
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages = new List<List<CosmosElement>>()
            {
                new List<CosmosElement>()
                {
                    CosmosElement.Parse("{\"payload\": {\"$1\": {}}}")
                }
            };

            List<CosmosElement> elements = await AggregateQueryPipelineStageTests.CreateAndDrain(
                pages: pages,
                aggregates: new List<AggregateOperator>() { AggregateOperator.Sum },
                aliasToAggregateType: new Dictionary<string, AggregateOperator?>() { { "$1", AggregateOperator.Sum } },
                orderedAliases: new List<string>() { "$1" },
                hasSelectValue: false,
                continuationToken: null);

            Assert.AreEqual(1, elements.Count);
            Assert.AreEqual(0, (elements[0] as CosmosObject).Keys.Count());
        }

        private static async Task<List<CosmosElement>> CreateAndDrain(
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages,
            IReadOnlyList<AggregateOperator> aggregates,
            IReadOnlyDictionary<string, AggregateOperator?> aliasToAggregateType,
            IReadOnlyList<string> orderedAliases,
            bool hasSelectValue,
            CosmosElement continuationToken)
        {
            IQueryPipelineStage source = new MockQueryPipelineStage(pages);

            TryCatch<IQueryPipelineStage> tryCreateAggregateQueryPipelineStage = AggregateQueryPipelineStage.MonadicCreate(
                aggregates: aggregates,
                aliasToAggregateType: aliasToAggregateType,
                orderedAliases: orderedAliases,
                hasSelectValue: hasSelectValue,
                continuationToken: continuationToken,
                monadicCreatePipelineStage: (CosmosElement continuationToken) => TryCatch<IQueryPipelineStage>.FromResult(source));
            Assert.IsTrue(tryCreateAggregateQueryPipelineStage.Succeeded);

            IQueryPipelineStage aggregateQueryPipelineStage = tryCreateAggregateQueryPipelineStage.Result;

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