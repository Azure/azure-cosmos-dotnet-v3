// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.BenchmarkStrategies
{
    using System;
    using System.Collections.Generic;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Horology;
    using BenchmarkDotNet.Jobs;
    using BenchmarkDotNet.Toolchains;
    using BenchmarkDotNet.Toolchains.CsProj;

    /// <summary>
    /// Attribute to run the benchmark on Throughput mode.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class NetThroughputAttribute : Attribute, IConfigSource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NetThroughputAttribute"/> class.
        /// </summary>
        /// <param name="selectedFrameworks">Frameworks to use for the benchmark.</param>
        /// <param name="invocationCount">Amount of iterations per invocation.</param>
        /// <param name="warmupCount">Amount of warmup iterations.</param>
        /// <param name="targetCount">Amount of target iterations.</param>
        /// <param name="launchCount">Amount of launch iterations.</param>
        /// <param name="iterationTimeMs">Amount of time in milliseconds to run the benchmark.</param>
        public NetThroughputAttribute(BenchmarkFrameworks[] selectedFrameworks, int invocationCount = -1, int warmupCount = -1, int targetCount = -1, int launchCount = -1, int iterationTimeMs = -1, int maxIterations = -1)
        {
            List<Job> jobs = new List<Job>(selectedFrameworks.Length);

            foreach (BenchmarkFrameworks selectedFramework in selectedFrameworks)
            {
                IToolchain toolchain = null;
                switch (selectedFramework)
                {
                    case BenchmarkFrameworks.NetCore21:
                        toolchain = CsProjCoreToolchain.NetCoreApp21;
                        break;
                    case BenchmarkFrameworks.NetFx471:
                        toolchain = CsProjClassicNetToolchain.Net471;
                        break;
                }

                if (toolchain == null)
                {
                    throw new ArgumentOutOfRangeException("selectedFramework", "Not implementad BenchmarkFrameworks value.");
                }

                Job job = Job.Default.With(toolchain).WithGcServer(true);

                if (invocationCount >= 0)
                {
                    job = job.WithInvocationCount(invocationCount);
                }

                if (warmupCount >= 0)
                {
                    job = job.WithWarmupCount(warmupCount);
                }

                if (targetCount >= 0)
                {
                    job = job.WithIterationCount(targetCount);
                }

                if (launchCount >= 0)
                {
                    job = job.WithLaunchCount(launchCount);
                }

                if (iterationTimeMs > 0)
                {
                    job = job.WithIterationTime(new TimeInterval(iterationTimeMs, TimeUnit.Millisecond));
                }

                if (maxIterations > 0)
                {
                    job = job.WithMaxIterationCount(maxIterations);
                }

                jobs.Add(job);
            }

            this.Config = ManualConfig.CreateEmpty().With(jobs.ToArray());
        }

        /// <inheritdoc/>
        public IConfig Config { get; }
    }
}
