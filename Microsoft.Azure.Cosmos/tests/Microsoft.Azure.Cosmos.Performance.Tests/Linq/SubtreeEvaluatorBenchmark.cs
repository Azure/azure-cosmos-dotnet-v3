// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq.Expressions;
    using BenchmarkDotNet.Attributes;

    /// <summary>
    /// Benchmark measuring SubtreeEvaluator constant evaluation performance.
    /// Validates fix for GitHub Issue #5487: Unbounded JIT/IL growth from Expression.Compile().
    ///
    /// CompileBaseline uses the original Expression.Compile() path which emits DynamicMethod IL
    /// into native memory on every call – this memory is never reclaimed, causing an unbounded leak.
    /// EvaluateWithFix uses the full SubtreeEvaluator code path with Compile(preferInterpretation: true),
    /// which avoids IL emission entirely.
    ///
    /// Each benchmark method runs an inner loop of OperationsPerInvoke iterations so that
    /// per-invocation time exceeds BenchmarkDotNet's 100 ms floor and the native-memory leak
    /// in the baseline accumulates enough to be clearly visible in the GlobalCleanup output.
    ///
    /// [MemoryDiagnoser] reports managed GC allocations per operation.  The native-memory leak
    /// (the real issue) is not visible through MemoryDiagnoser but is observable via process private
    /// bytes – the GlobalCleanup prints that value for manual inspection.
    /// </summary>
    [MemoryDiagnoser]
    public class SubtreeEvaluatorBenchmark
    {
        private const int OperationsPerInvoke = 2000;

        private long privateMemoryBeforeBytes;
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

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            using Process process = Process.GetCurrentProcess();
            process.Refresh();
            this.privateMemoryBeforeBytes = process.PrivateMemorySize64;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            using Process process = Process.GetCurrentProcess();
            process.Refresh();
            long delta = process.PrivateMemorySize64 - this.privateMemoryBeforeBytes;
            Console.WriteLine($"[NativeMemory] Private bytes delta: {delta:N0} bytes");
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = OperationsPerInvoke)]
        public object CompileBaseline()
        {
            // Baseline: duplicates the original code path that emits DynamicMethod IL per call.
            object result = null;
            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                Delegate function = this.lambda.Compile();
                result = function.DynamicInvoke(null);
            }

            return result;
        }

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public Expression EvaluateWithFix()
        {
            // Measures the full SubtreeEvaluator code path with the interpretation-based fix.
            Expression result = null;
            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                result = this.evaluator.Evaluate(this.expression);
            }

            return result;
        }
    }
}
