//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class HybridSearchCrossPartitionQueryPipelineStageTests
    {
        [TestMethod]
        public void ComputeRanksUsesStandardCompetitionRanking()
        {
            AssertRanks(
                scores: new[] { 10.0, 10.0, 8.0, 7.0, 6.0 },
                expectedRanks: new[] { 1, 1, 3, 4, 5 });
            AssertRanks(
                scores: new[] { 10.0, 9.0, 9.0, 9.0, 4.0 },
                expectedRanks: new[] { 1, 2, 2, 2, 5 });
            AssertRanks(
                scores: new[] { 10.0, 9.0, 8.0, 7.0, 7.0 },
                expectedRanks: new[] { 1, 2, 3, 4, 4 });
            AssertRanks(
                scores: new[] { 5.0, 5.0, 5.0, 5.0, 5.0 },
                expectedRanks: new[] { 1, 1, 1, 1, 1 });
        }

        [TestMethod]
        public void CompetitionRanksDetermineRrfOrder()
        {
            IReadOnlyList<List<HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple>> componentScores =
                new List<List<HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple>>
                {
                    new List<HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple>
                    {
                        new HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple(10, 0),
                        new HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple(5, 1),
                        new HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple(5, 2),
                        new HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple(5, 3),
                        new HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple(0, 4),
                    },
                    new List<HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple>
                    {
                        new HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple(10, 4),
                        new HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple(9, 3),
                        new HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple(8, 2),
                        new HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple(7, 1),
                        new HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple(6, 0),
                    },
                };

            int[,] ranks = HybridSearchCrossPartitionQueryPipelineStage.ComputeRanks(componentScores);

            CollectionAssert.AreEqual(new[] { 1, 2, 2, 2, 5 }, GetComponentRanks(ranks, componentIndex: 0));
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1 }, GetComponentRanks(ranks, componentIndex: 1));

            int[] fusedOrder = Enumerable.Range(0, ranks.GetLength(1))
                .OrderByDescending(documentIndex =>
                    (2.0 / (60 + ranks[0, documentIndex])) +
                    (1.0 / (60 + ranks[1, documentIndex])))
                .ToArray();

            CollectionAssert.AreEqual(new[] { 3, 0, 2, 1, 4 }, fusedOrder);
        }

        private static void AssertRanks(double[] scores, int[] expectedRanks)
        {
            IReadOnlyList<List<HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple>> componentScores =
                new List<List<HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple>>
                {
                    scores.Select((score, index) =>
                        new HybridSearchCrossPartitionQueryPipelineStage.ScoreTuple(score, index)).ToList(),
                };

            int[,] ranks = HybridSearchCrossPartitionQueryPipelineStage.ComputeRanks(componentScores);

            CollectionAssert.AreEqual(expectedRanks, GetComponentRanks(ranks, componentIndex: 0));
        }

        private static int[] GetComponentRanks(int[,] ranks, int componentIndex)
        {
            return Enumerable.Range(0, ranks.GetLength(1))
                .Select(documentIndex => ranks[componentIndex, documentIndex])
                .ToArray();
        }
    }
}
