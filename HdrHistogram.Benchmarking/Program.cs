using BenchmarkDotNet.Running;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;

namespace HdrHistogram.Benchmarking
{
    class Program
    {
        static void Main(string[] args)
        {
            var manualConfig = ManualConfig.Create(DefaultConfig.Instance);
            manualConfig.Add(new MemoryDiagnoser());
            //manualConfig.Add(new BenchmarkDotNet.Diagnostics.Windows.InliningDiagnoser());
            //manualConfig.Add(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions);
            var config = manualConfig
                .With(Job.Clr.With(Jit.LegacyJit))
                .With(Job.Clr.With(Jit.RyuJit))
                .With(Job.Core.With(Jit.RyuJit));

            var switcher = new BenchmarkSwitcher(new[] {
                typeof(LeadingZeroCount.LeadingZeroCount64BitBenchmark),
                typeof(LeadingZeroCount.LeadingZeroCount32BitBenchmark),
                typeof(Recording.Recording32BitBenchmark),
            });
            switcher.Run(args, config);
        }
    }
}
