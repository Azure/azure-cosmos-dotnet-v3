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

            BenchmarkRunner.Run<EncryptionBenchmark>(dontRequireSlnToRunBenchmarks, args);
        }
    }
}