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
    /// Benchmark measuring SubtreeEvaluator constant evaluation performance and memory impact.
    /// Validates fix for GitHub Issue #5487: Unbounded JIT/IL growth from Expression.Compile()
    /// </summary>
    [MemoryDiagnoser]
    public class SubtreeEvaluatorBenchmark
    {
        private const int MemoryIterations = 1000;

        private Expression expression;
        private LambdaExpression lambda;
        private SubtreeEvaluator evaluator;

        [GlobalSetup]
        public void Setup()
        {
            int capturedValue = 42;
            Expression<Func<int>> expr = () => capturedValue + 1;
            this.expression = expr.Body;
            this.lambda = Expression.Lambda(this.expression);
            this.evaluator = new SubtreeEvaluator(new HashSet<Expression> { this.expression });
        }

        [Benchmark(Baseline = true)]
        public object CompileBaseline()
        {
            // Baseline: duplicates old code path that emits DynamicMethod with IL per call
            Delegate function = this.lambda.Compile();
            return function.DynamicInvoke(null);
        }

        [Benchmark]
        public Expression EvaluateWithFix()
        {
            // Measures actual SubtreeEvaluator code path with the preferInterpretation fix
            return this.evaluator.Evaluate(this.expression);
        }

        /// <summary>
        /// Demonstrates native memory growth: each Compile() emits a new DynamicMethod
        /// whose IL is never reclaimed by the GC. Over many iterations, process memory
        /// grows unboundedly. Compare with CompileLambdaMemory below.
        /// </summary>
        [Benchmark]
        public long NativeCompileMemoryGrowth()
        {
            long before = GC.GetTotalMemory(forceFullCollection: true);

            for (int i = 0; i < MemoryIterations; i++)
            {
                Delegate function = this.lambda.Compile();
                function.DynamicInvoke(null);
            }

            long after = GC.GetTotalMemory(forceFullCollection: true);
            return after - before;
        }

        /// <summary>
        /// With the interpretation fix, no new DynamicMethods are emitted so memory
        /// remains stable across iterations. Compare with NativeCompileMemoryGrowth above.
        /// </summary>
        [Benchmark]
        public long InterpretedCompileMemoryGrowth()
        {
            long before = GC.GetTotalMemory(forceFullCollection: true);

            for (int i = 0; i < MemoryIterations; i++)
            {
                Delegate function = ExpressionCompileHelper.CompileLambda(this.lambda);
                function.DynamicInvoke(null);
            }

            long after = GC.GetTotalMemory(forceFullCollection: true);
            return after - before;
        }
    }
}
