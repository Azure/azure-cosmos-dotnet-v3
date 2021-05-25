using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

//It seems for the most reliable tests, running them single threaded doesn't 
//  saturate the OS Thread scheduler and therfore doesn't lead to extended 
//  pauses when executing one of our many dealy/sleep paths.
//I have tried to consolidate them all to use this one class, so if they are 
//  abandoned, then this DisableTestParallelization can be removed too. -LC
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace HdrHistogram.UnitTests
{
    public static class Spin
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Wait(TimeSpan period)
        {
            var sw = Stopwatch.StartNew();
            SpinWait.SpinUntil(() => period < sw.Elapsed);
        }
    }
}