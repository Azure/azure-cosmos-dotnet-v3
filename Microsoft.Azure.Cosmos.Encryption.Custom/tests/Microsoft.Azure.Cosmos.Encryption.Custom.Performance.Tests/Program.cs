namespace Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests
{
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Diagnosers;
    using BenchmarkDotNet.Jobs;
    using BenchmarkDotNet.Running;
    using BenchmarkDotNet.Toolchains.InProcess.Emit;

    internal class Program
    {
        public static void Main(string[] args)
        {
            ManualConfig dontRequireSlnToRunBenchmarks = ManualConfig
                .Create(DefaultConfig.Instance)
                .AddJob(Job.MediumRun.WithToolchain(InProcessEmitToolchain.Instance))
                .AddDiagnoser(MemoryDiagnoser.Default);

            // Run any benchmarks in this assembly; respects --filter and avoids crashing when a filter matches none
            BenchmarkSwitcher
                .FromAssembly(typeof(Program).Assembly)
                .Run(args, dontRequireSlnToRunBenchmarks);
        }
    }
}