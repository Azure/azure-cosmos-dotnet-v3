// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq.Expressions;
    using BenchmarkDotNet.Attributes;

    /// <summary>
    /// Benchmark comparing Expression.Compile() strategies.
    /// Validates fix for GitHub Issue #5487: Unbounded JIT/IL growth from Expression.Compile()
    /// </summary>
    public class SubtreeEvaluatorBenchmark
    {
        private LambdaExpression lambda;

        [GlobalSetup]
        public void Setup()
        {
            int capturedValue = 42;
            Expression<Func<int>> expr = () => capturedValue + 1;
            this.lambda = Expression.Lambda(expr.Body);
        }

        [Benchmark(Baseline = true)]
        public object CompileStandard()
        {
            // Standard Compile() - emits DynamicMethod with IL, JITs it
            // Each call creates native memory that is NOT garbage collected
            Delegate function = this.lambda.Compile();
            return function.DynamicInvoke(null);
        }

#if NET6_0_OR_GREATER
        [Benchmark]
        public object CompileInterpreted()
        {
            // Compile(preferInterpretation: true) - interprets without IL emission
            // No native memory growth, better for one-shot expression evaluation
            Delegate function = this.lambda.Compile(preferInterpretation: true);
            return function.DynamicInvoke(null);
        }
#endif
    }
}
