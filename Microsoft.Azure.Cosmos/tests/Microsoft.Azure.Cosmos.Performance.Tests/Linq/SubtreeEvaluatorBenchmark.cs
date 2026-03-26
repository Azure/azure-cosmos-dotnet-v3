// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq.Expressions;
    using BenchmarkDotNet.Attributes;

    /// <summary>
    /// Benchmark comparing Expression.Compile() vs Compile(preferInterpretation: true)
    /// in the context of CosmosDB LINQ-to-SQL query generation.
    /// Validates fix for GitHub Issue #5487: Unbounded JIT/IL growth from Expression.Compile()
    /// </summary>
    [MemoryDiagnoser]
    public class SubtreeEvaluatorBenchmark
    {
        private const int MemoryIterations = 1000;

        private LambdaExpression lambda;

        [GlobalSetup]
        public void Setup()
        {
            string status = "active";
            Expression<Func<bool>> expr = () => status == "active";
            this.lambda = Expression.Lambda(expr.Body);
        }

        [Benchmark(Baseline = true)]
        public object Compile()
        {
            Delegate fn = this.lambda.Compile();
            return fn.DynamicInvoke(null);
        }

        [Benchmark]
        public object CompileWithInterpretation()
        {
            Delegate fn = this.lambda.Compile(preferInterpretation: true);
            return fn.DynamicInvoke(null);
        }

        [Benchmark]
        public long CompileMemoryGrowth()
        {
            long before = GC.GetTotalMemory(forceFullCollection: true);

            for (int i = 0; i < MemoryIterations; i++)
            {
                Delegate fn = this.lambda.Compile();
                fn.DynamicInvoke(null);
            }

            long after = GC.GetTotalMemory(forceFullCollection: true);
            return after - before;
        }

        [Benchmark]
        public long CompileWithInterpretationMemoryGrowth()
        {
            long before = GC.GetTotalMemory(forceFullCollection: true);

            for (int i = 0; i < MemoryIterations; i++)
            {
                Delegate fn = this.lambda.Compile(preferInterpretation: true);
                fn.DynamicInvoke(null);
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
