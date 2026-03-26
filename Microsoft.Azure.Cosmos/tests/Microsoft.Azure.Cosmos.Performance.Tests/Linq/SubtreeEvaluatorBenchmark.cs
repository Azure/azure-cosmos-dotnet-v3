// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using BenchmarkDotNet.Attributes;

    /// <summary>
    /// Benchmark measuring E2E LINQ-to-SQL query generation performance and memory impact.
    /// Models realistic CosmosDB LINQ query translation (NOT execution) through the full
    /// pipeline: ConstantEvaluator.PartialEval → SubtreeEvaluator → ExpressionToSql.
    /// Validates fix for GitHub Issue #5487: Unbounded JIT/IL growth from Expression.Compile()
    /// </summary>
    [MemoryDiagnoser]
    public class SubtreeEvaluatorBenchmark
    {
        private const int MemoryIterations = 1000;

        private class BenchmarkDocument
        {
            public string Status { get; set; }
            public int Priority { get; set; }
            public string Region { get; set; }
        }

        [Benchmark(Baseline = true)]
        public string SimpleWhereClause()
        {
            // Simulates: .Where(doc => doc.Status == status)
            // The captured variable "status" triggers SubtreeEvaluator → CompileLambda
            string status = "active";
            return SqlTranslator.TranslateExpression(
                CreateWhereBody<BenchmarkDocument>(doc => doc.Status == status));
        }

        [Benchmark]
        public string ComputedConstant()
        {
            // Simulates: .Where(doc => doc.Priority > threshold + offset)
            // The expression "threshold + offset" is a computed constant requiring compilation
            int threshold = 5;
            int offset = 3;
            return SqlTranslator.TranslateExpression(
                CreateWhereBody<BenchmarkDocument>(doc => doc.Priority > threshold + offset));
        }

        [Benchmark]
        public string NestedPropertyAccess()
        {
            // Simulates: .Where(doc => doc.Region == holder.Region)
            // Nested member access on captured anonymous object triggers compilation
            var filter = new { Region = "westus" };
            return SqlTranslator.TranslateExpression(
                CreateWhereBody<BenchmarkDocument>(doc => doc.Region == filter.Region));
        }

        [Benchmark]
        public string MultiplePredicates()
        {
            // Simulates: .Where(doc => doc.Status == status && doc.Priority >= minPriority)
            // Multiple captured variables, each evaluated through SubtreeEvaluator
            string status = "active";
            int minPriority = 3;
            return SqlTranslator.TranslateExpression(
                CreateWhereBody<BenchmarkDocument>(doc => doc.Status == status && doc.Priority >= minPriority));
        }

        /// <summary>
        /// Demonstrates native memory growth: repeated query generation with the old
        /// Compile() path emits DynamicMethod IL that is never reclaimed by the GC.
        /// </summary>
        [Benchmark]
        public long NativeCompileMemoryGrowth()
        {
            long before = GC.GetTotalMemory(forceFullCollection: true);

            for (int i = 0; i < MemoryIterations; i++)
            {
                string status = "active";
                Expression body = CreateWhereBody<BenchmarkDocument>(doc => doc.Status == status);

                // Simulate the old code path: PartialEval with direct Compile()
                HashSet<Expression> candidates = Nominator.Nominate(body, _ => true);
                foreach (Expression candidate in candidates)
                {
                    if (candidate.NodeType != ExpressionType.Constant
                        && candidate.NodeType != ExpressionType.Parameter
                        && candidate.NodeType != ExpressionType.Lambda)
                    {
                        LambdaExpression lambda = Expression.Lambda(candidate);
                        Delegate fn = lambda.Compile();
                        fn.DynamicInvoke(null);
                    }
                }
            }

            long after = GC.GetTotalMemory(forceFullCollection: true);
            return after - before;
        }

        /// <summary>
        /// With the interpretation fix, the same query generation path uses
        /// ExpressionCompileHelper.CompileLambda which avoids DynamicMethod emission.
        /// Memory remains stable across iterations.
        /// </summary>
        [Benchmark]
        public long InterpretedCompileMemoryGrowth()
        {
            long before = GC.GetTotalMemory(forceFullCollection: true);

            for (int i = 0; i < MemoryIterations; i++)
            {
                // Full E2E translation pipeline (uses ExpressionCompileHelper internally)
                string status = "active";
                SqlTranslator.TranslateExpression(
                    CreateWhereBody<BenchmarkDocument>(doc => doc.Status == status));
            }

            long after = GC.GetTotalMemory(forceFullCollection: true);
            return after - before;
        }

        private static Expression CreateWhereBody<T>(Expression<Func<T, bool>> predicate)
        {
            return predicate.Body;
        }
    }
}
