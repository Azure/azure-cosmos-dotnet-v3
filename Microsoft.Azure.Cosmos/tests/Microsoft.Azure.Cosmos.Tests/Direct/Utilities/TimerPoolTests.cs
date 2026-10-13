// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TimerPoolTests
    {
        [Owner("visridha")]
        [TestMethod]
        public async Task TimerPoolConcurrentEnqueuePreservesOrderingAsync()
        {
            using TimerPool pool = new TimerPool(500_000);

            TaskCompletionSource<bool> waitTask = new TaskCompletionSource<bool>();
            List<Task> tasksToWait = new List<Task>();
            for (int i = 0; i < 50; i++)
            {
                tasksToWait.Add(
                    Task.Run(
                        async () =>
                        {
                            await waitTask.Task;
                            Stopwatch sw = Stopwatch.StartNew();
                            for (int i = 0; i < 20_000 || sw.Elapsed < TimeSpan.FromMilliseconds(50); i++)
                            {
                                PooledTimer timer = new PooledTimer(5, pool);
                                Task timerTask = timer.StartTimerAsync();
                            }
                        }));
            }

            waitTask.SetResult(true);
            await Task.WhenAll(tasksToWait);
            Assert.IsTrue(pool.PooledTimersByTimeout.All(x => x.Count == 0 || x.Count == 1));

            int sumOfEnqueuedTimers = 0;
            foreach (ConcurrentDictionary<int, ConcurrentQueue<PooledTimer>> timerBucket in pool.PooledTimersByTimeout)
            {
                Assert.IsTrue(timerBucket.Count == 0 || timerBucket.Count == 1);

                if (timerBucket.Count == 0)
                {
                    continue;
                }

                sumOfEnqueuedTimers += timerBucket.First().Value.Count;
            }

            Assert.IsTrue(sumOfEnqueuedTimers >= 20_000 * 50);

            // within each pool, order is monotonic.
            foreach (ConcurrentDictionary<int, ConcurrentQueue<PooledTimer>> timerBucket in pool.PooledTimersByTimeout)
            {
                ConcurrentQueue<PooledTimer> timers = timerBucket.FirstOrDefault().Value;
                if (timers == null)
                {
                    continue;
                }

                long prevTimeoutTicks = long.MinValue;
                foreach (PooledTimer timer in timers)
                {
                    // timers are monotonically increasing
                    Assert.IsTrue(timer.TimeoutTicks >= prevTimeoutTicks);
                    prevTimeoutTicks = timer.TimeoutTicks;
                }
            }
                
        }
    }
}
