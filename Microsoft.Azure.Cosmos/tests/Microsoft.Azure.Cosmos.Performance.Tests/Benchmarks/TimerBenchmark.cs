// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Documents;

    [MemoryDiagnoser]
    public class TimerWheelBenchmark
    {
        private readonly TimerPool timerPool;
        private readonly TimerWheel mainWheel;
        private readonly IReadOnlyList<int> timeouts;
        public TimerWheelBenchmark()
        {
            this.timeouts = TimerUtilities.GenerateTimeoutList(10000, 10000, 1000);
            this.mainWheel = TimerWheel.CreateTimerWheel(TimeSpan.FromMilliseconds(1000), 10);
            this.timerPool = new TimerPool(1);
        }

        [Benchmark]
        public async Task TenK_WithTimerWheel()
        {
            TimerWheel wheel = TimerWheel.CreateTimerWheel(TimeSpan.FromMilliseconds(1000), 10);
            List<Task> timers = new List<Task>(this.timeouts.Count);
            for (int i = 0; i < this.timeouts.Count; i++)
            {
                TimerWheelTimer timer = wheel.CreateTimer(TimeSpan.FromMilliseconds(this.timeouts[i]));
                timers.Add(timer.StartTimerAsync());
            }

            await Task.WhenAll(timers);
            wheel.Dispose();
        }

        [Benchmark]
        public async Task TenK_WithPooledTimer()
        {
            TimerPool timerPool = new TimerPool(1);
            List<Task> timers = new List<Task>(this.timeouts.Count);
            for (int i = 0; i < this.timeouts.Count; i++)
            {
                PooledTimer timer = timerPool.GetPooledTimer(this.timeouts[i] / 1000);
                timers.Add(timer.StartTimerAsync());
            }

            await Task.WhenAll(timers);
            timerPool.Dispose();
        }

        [Benchmark]
        public async Task One_WithTimerWheel()
        {
            TimerWheelTimer timer = this.mainWheel.CreateTimer(TimeSpan.FromMilliseconds(1000));
            await timer.StartTimerAsync();
        }

        [Benchmark]
        public async Task One_WithPooledTimer()
        {
            PooledTimer timer = this.timerPool.GetPooledTimer(1);
            await timer.StartTimerAsync();
        }

        public void DoNothing(object state) { }

        public class WorkerWithTimer : IDisposable
        {
            private readonly TaskCompletionSource<object> taskCompletionSource;
            private readonly int timeout;
            private Timer timer;
            public WorkerWithTimer(int timeout)
            {
                this.timeout = timeout;
                this.taskCompletionSource = new TaskCompletionSource<object>();
            }

            public Task StartTimerAsync()
            {
                this.timer = new Timer(this.OnTimer, null, this.timeout, this.timeout);
                return this.taskCompletionSource.Task;
            }

            public void OnTimer(object stateInfo)
            {
                this.taskCompletionSource.TrySetResult(null);
            }

            public void Dispose()
            {
                this.timer.Dispose();
            }
        }
    }
}