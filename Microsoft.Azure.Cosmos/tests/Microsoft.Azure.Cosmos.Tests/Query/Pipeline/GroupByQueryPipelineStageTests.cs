//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.GroupBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class GroupByQueryPipelineStageTests
    {
        [TestMethod]
        public async Task SinglePageAsync()
        {
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages = new List<List<CosmosElement>>()
            {
                new List<CosmosElement>()
                {
                    CosmosElement.Parse("{\"groupByItems\": [{\"item\" : \"John\"}], \"payload\" : {\"name\": \"John\", \"count\": {\"item\": 42}}}")
                }
            };

            List<CosmosElement> elements = await GroupByQueryPipelineStageTests.CreateAndDrainAsync(
                pages: pages,
                continuationToken: null,
                groupByAliasToAggregateType: new Dictionary<string, AggregateOperator?>() { { "name", null }, { "count", AggregateOperator.Sum } },
                orderedAliases: new List<string>() { "name", "count" },
                hasSelectValue: false);

            Assert.AreEqual(1, elements.Count);
            Assert.AreEqual(42, Number64.ToLong(((elements[0] as CosmosObject)["count"] as CosmosNumber).Value));
            Assert.AreEqual("John", ((elements[0] as CosmosObject)["name"] as CosmosString).Value.ToString());
        }

        private static async Task<List<CosmosElement>> CreateAndDrainAsync(
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages,
            CosmosElement continuationToken,
            IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
            IReadOnlyList<string> orderedAliases,
            bool hasSelectValue)
        {
            IQueryPipelineStage source = new MockQueryPipelineStage(pages);

            TryCatch<IQueryPipelineStage> tryCreateGroupByStage = GroupByQueryPipelineStage.MonadicCreate(
                requestContinuation: continuationToken,
                monadicCreatePipelineStage: (CosmosElement continuationToken) => TryCatch<IQueryPipelineStage>.FromResult(source),
                aggregates: new AggregateOperator[] { },
                groupByAliasToAggregateType: groupByAliasToAggregateType,
                orderedAliases: orderedAliases,
                hasSelectValue: hasSelectValue,
                pageSize: int.MaxValue);
            Assert.IsTrue(tryCreateGroupByStage.Succeeded);

            IQueryPipelineStage groupByQueryPipelineStage = tryCreateGroupByStage.Result;

            List<CosmosElement> elements = new List<CosmosElement>();
            await foreach (TryCatch<QueryPage> page in new EnumerableStage(groupByQueryPipelineStage, NoOpTrace.Singleton))
            {
                page.ThrowIfFailed();

                elements.AddRange(page.Result.Documents);
            }

            return elements;
        }
    }
}