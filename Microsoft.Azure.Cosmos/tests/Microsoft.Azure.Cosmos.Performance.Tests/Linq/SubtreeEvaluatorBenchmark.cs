// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq.Expressions;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Diagnostics.Windows.Configs;

    /// <summary>
    /// Benchmark comparing Expression.Compile() vs Compile(preferInterpretation: true)
    /// in the context of CosmosDB LINQ-to-SQL query generation.
    /// Validates fix for GitHub Issue #5487: Unbounded JIT/IL growth from Expression.Compile().
    ///
    /// [MemoryDiagnoser] reports managed GC allocations (Gen0/Gen1/Allocated columns).
    /// [NativeMemoryProfiler] uses ETW to track native memory allocations and leaks per method,
    /// adding "Allocated native memory" and "Native memory leak" columns to the results table.
    /// Note: NativeMemoryProfiler requires Windows and elevated (admin) privileges.
    /// </summary>
    [ShortRunJob]
    [MemoryDiagnoser]
    // [NativeMemoryProfiler] // Enable this line to include native memory profiling, requires Windows and admin privileges.
    public class SubtreeEvaluatorBenchmark
    {
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
    }
}
