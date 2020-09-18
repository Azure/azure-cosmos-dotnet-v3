//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Timers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TimerWheelCoreTests
    {
        [TestMethod]
        public void CreatesTimerWheel()
        {
            Assert.IsNotNull(TimerWheel.CreateTimerWheel(TimeSpan.FromMilliseconds(50), 1));
        }

        [DataTestMethod]
        [DataRow(0, 1)]
        [DataRow(-1, 1)]
        [DataRow(50, 0)]
        [DataRow(50, -1)]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void InvalidConstructor(int resolutionInMs, int buckets)
        {
            new TimerWheelCore(TimeSpan.FromMilliseconds(resolutionInMs), buckets);
        }

        [TestMethod]
        public void CreatesTimer()
        {
            TimerWheelCore wheel = new TimerWheelCore(TimeSpan.FromMilliseconds(30), 10);
            TimerWheelTimer timer = wheel.CreateTimer(TimeSpan.FromMilliseconds(90));
            Assert.IsNotNull(timer);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        [DataRow(1)]
        [DataRow(35)]
        [DataRow(330)]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void InvalidTimeout(int timeout)
        {
            TimerWheelCore wheel = new TimerWheelCore(TimeSpan.FromMilliseconds(30), 10);
            wheel.CreateTimer(TimeSpan.FromMilliseconds(timeout));
        }

        [TestMethod]
        public void IndexMovesAsTimerPasses()
        {
            TimerWheelCore wheel = new TimerWheelCore(TimeSpan.FromMilliseconds(30), 3, timer: null); // deactivate timer to fire manually
            TimerWheelTimer timer = wheel.CreateTimer(TimeSpan.FromMilliseconds(90));
            Task timerTask = timer.StartTimerAsync();
            wheel.OnTimer(null);
            Assert.AreEqual(TaskStatus.WaitingForActivation, timerTask.Status);
            wheel.OnTimer(null);
            Assert.AreEqual(TaskStatus.WaitingForActivation, timerTask.Status);
            TimerWheelTimer secondTimer = wheel.CreateTimer(TimeSpan.FromMilliseconds(60));
            Task secondTimerTask = secondTimer.StartTimerAsync();
            wheel.OnTimer(null);
            Assert.AreEqual(TaskStatus.RanToCompletion, timerTask.Status);
            wheel.OnTimer(null);
            Assert.AreEqual(TaskStatus.RanToCompletion, secondTimerTask.Status);
        }

        [TestMethod]
        public void DisposedCannotCreateTimers()
        {
            TimerWheelCore wheel = new TimerWheelCore(TimeSpan.FromMilliseconds(30), 3);
            TimerWheelTimer timer = wheel.CreateTimer(TimeSpan.FromMilliseconds(90));
            Assert.IsNotNull(timer);
            wheel.Dispose();
            Assert.ThrowsException<ObjectDisposedException>(() => wheel.CreateTimer(TimeSpan.FromMilliseconds(90)));
        }

        [TestMethod]
        public void DisposedCannotStartTimers()
        {
            TimerWheelCore wheel = new TimerWheelCore(TimeSpan.FromMilliseconds(30), 3);
            TimerWheelTimer timer = wheel.CreateTimer(TimeSpan.FromMilliseconds(90));
            Assert.IsNotNull(timer);
            wheel.Dispose();
            Assert.ThrowsException<ObjectDisposedException>(() => timer.StartTimerAsync());
        }

        [TestMethod]
        public void DisposeCancelsTimers()
        {
            TimerWheelCore wheel = new TimerWheelCore(TimeSpan.FromMilliseconds(30), 3);
            TimerWheelTimer timer = wheel.CreateTimer(TimeSpan.FromMilliseconds(90));
            Task timerTask = timer.StartTimerAsync();
            wheel.Dispose();
            Assert.AreEqual(TaskStatus.Canceled, timerTask.Status);
        }

        [TestMethod]
        [Timeout(1000)]
        public async Task TimeoutFires()
        {
            const int timerTimeout = 200;
            const int resolution = 50;
            TimerWheelCore wheel = new TimerWheelCore(TimeSpan.FromMilliseconds(resolution), 10); // 10 buckets of 50 ms go up to 500ms
            TimerWheelTimer timer = wheel.CreateTimer(TimeSpan.FromMilliseconds(timerTimeout));
            Stopwatch stopwatch = Stopwatch.StartNew();
            await timer.StartTimerAsync();
            stopwatch.Stop();
            Assert.IsTrue(stopwatch.ElapsedMilliseconds >= timerTimeout - resolution && stopwatch.ElapsedMilliseconds <= timerTimeout + resolution);
        }

        [TestMethod]
        [Timeout(1000)]
        public async Task TimeoutFires_SameTimeout()
        {
            const int timerTimeout = 200;
            const int resolution = 50;
            TimerWheelCore wheel = new TimerWheelCore(TimeSpan.FromMilliseconds(resolution), 10); // 10 buckets of 50 ms go up to 500ms
            TimerWheelTimer timer = wheel.CreateTimer(TimeSpan.FromMilliseconds(timerTimeout));
            TimerWheelTimer timer2 = wheel.CreateTimer(TimeSpan.FromMilliseconds(timerTimeout));
            Stopwatch stopwatch = Stopwatch.StartNew();
            await Task.WhenAll(timer.StartTimerAsync(), timer2.StartTimerAsync());
            stopwatch.Stop();
            Assert.IsTrue(stopwatch.ElapsedMilliseconds >= timerTimeout - resolution, $"{stopwatch.ElapsedMilliseconds} >= {timerTimeout - resolution}, timerTimeout: {timerTimeout}, resolution: {resolution}");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds <= timerTimeout + resolution, $"{stopwatch.ElapsedMilliseconds} <= {timerTimeout + resolution}, timerTimeout: {timerTimeout}, resolution: {resolution}");
        }

        [TestMethod]
        [Timeout(2000)]
        public async Task MultipleTimeouts()
        {
            const int timerTimeout = 100;
            const int buckets = 20;
            const int resolution = 50;
            TimerWheelCore wheel = new TimerWheelCore(TimeSpan.FromMilliseconds(resolution), buckets); // 20 buckets of 50 ms go up to 1000ms
            List<Task<(int, long)>> tasks = new List<Task<(int, long)>>();
            for (int i = 0; i < 10; i++)
            {
                int estimatedTimeout = (i + 1) * timerTimeout;
                TimerWheelTimer timer = wheel.CreateTimer(TimeSpan.FromMilliseconds(estimatedTimeout));
                tasks.Add(Task.Run(async () =>
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    await timer.StartTimerAsync();
                    stopwatch.Stop();
                    return (estimatedTimeout, stopwatch.ElapsedMilliseconds);
                }));
            }

            await Task.WhenAll(tasks);
            foreach (Task<(int, long)> task in tasks)
            {
                Assert.IsTrue(task.Result.Item2 >= task.Result.Item1 - resolution && task.Result.Item2 <= task.Result.Item1 + resolution, $"Timer configured with {task.Result.Item1} took {task.Result.Item2} to fire.");
            }
        }
    }
}
