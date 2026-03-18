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
    /// Benchmark measuring SubtreeEvaluator constant evaluation performance.
    /// Validates fix for GitHub Issue #5487: Unbounded JIT/IL growth from Expression.Compile()
    /// </summary>
    public class SubtreeEvaluatorBenchmark
    {
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
    }
}
