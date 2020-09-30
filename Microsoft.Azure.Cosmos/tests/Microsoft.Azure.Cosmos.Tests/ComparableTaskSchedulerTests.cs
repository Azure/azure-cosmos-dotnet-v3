//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.ComparableTask;
    using Microsoft.VisualStudio.TestTools.UnitTesting;


    [TestClass]
    public class ComparableTaskSchedulerTests
    {
        [TestMethod]
        public async Task SimpleTestAsync()
        {
            foreach (bool useConstructorToAddTasks in new[] { true, false })
            {
                List<Task> tasks = new List<Task>();
                int maximumConcurrencyLevel = 10;

                for (int i = 0; i < maximumConcurrencyLevel; ++i)
                {
                    tasks.Add(new Task(() => { }));
                }

                await Task.Delay(1);

                foreach (Task task in tasks)
                {
                    Assert.AreEqual(false, task.IsCompleted);
                }

                ComparableTaskScheduler scheduler = null;
                try
                {
                    if (useConstructorToAddTasks)
                    {
                        scheduler = new ComparableTaskScheduler(
                            tasks.Select(task => new TestComparableTask(tasks.IndexOf(task), task)),
                            maximumConcurrencyLevel);
                    }
                    else
                    {
                        scheduler = new ComparableTaskScheduler(maximumConcurrencyLevel);
                        for (int i = 0; i < maximumConcurrencyLevel; ++i)
                        {
                            Assert.AreEqual(true, scheduler.TryQueueTask(new TestComparableTask(i, tasks[i])));
                        }
                    }

                    bool completionStatus = Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(10));
                    Assert.IsTrue(completionStatus);

                    foreach (Task task in tasks)
                    {
                        Assert.AreEqual(true, task.IsCompleted, $"Is overloaded constructor {useConstructorToAddTasks} and status {task.Status.ToString()}");
                    }
                }
                finally
                {
                    if (scheduler != null)
                    {
                        scheduler.Dispose();
                    }
                }
                
            }
        }

        [TestMethod]
        public void TestMaximumConcurrencyLevel()
        {
            using ComparableTaskScheduler firstScheduler = new ComparableTaskScheduler(10);
            Assert.AreEqual(10, firstScheduler.MaximumConcurrencyLevel);

            using ComparableTaskScheduler scheduler = new ComparableTaskScheduler();
            Assert.AreEqual(Environment.ProcessorCount, scheduler.MaximumConcurrencyLevel);

            scheduler.IncreaseMaximumConcurrencyLevel(1);
            Assert.AreEqual(Environment.ProcessorCount + 1, scheduler.MaximumConcurrencyLevel);

            try
            {
                scheduler.IncreaseMaximumConcurrencyLevel(-1);
                Assert.Fail("Expect ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        [TestMethod]
        public void TestStop()
        {
            using ComparableTaskScheduler scheduler = new ComparableTaskScheduler();
            Assert.AreEqual(true, scheduler.TryQueueTask(new TestComparableTask(0, Task.FromResult(false))));
            scheduler.Stop();
            Assert.AreEqual(false, scheduler.TryQueueTask(new TestComparableTask(0, Task.FromResult(false))));
        }


        [TestMethod]
        public async Task TestDelayedQueueTaskAsync()
        {
            ComparableTaskScheduler scheduler = new ComparableTaskScheduler();

            Task task = new Task(() =>
            {
                Assert.AreEqual(1, scheduler.CurrentRunningTaskCount);
            });

            Task delayedTask = new Task(() =>
            {
                Assert.AreEqual(1, scheduler.CurrentRunningTaskCount);
            });

            Assert.AreEqual(
                true,
                scheduler.TryQueueTask(new TestComparableTask(schedulePriority: 0, delayedTask), TimeSpan.FromMilliseconds(200)));
            Assert.AreEqual(
                false,
                scheduler.TryQueueTask(new TestComparableTask(schedulePriority: 0, delayedTask), TimeSpan.FromMilliseconds(200)));
            Assert.AreEqual(
                false,
                scheduler.TryQueueTask(new TestComparableTask(schedulePriority: 0, task)));
            Assert.AreEqual(
                true,
                scheduler.TryQueueTask(new TestComparableTask(schedulePriority: 1, task)));

            await Task.Delay(150);

            Assert.AreEqual(true, task.IsCompleted);
            Assert.AreEqual(false, delayedTask.IsCompleted);
            Assert.AreEqual(0, scheduler.CurrentRunningTaskCount);

            await Task.Delay(400);

            Assert.AreEqual(true, delayedTask.IsCompleted);
        }

        private sealed class TestComparableTask : ComparableTask
        {
            private readonly Task task;
            public TestComparableTask(int schedulePriority, Task task) :
                base(schedulePriority)
            {
                this.task = task;
            }

            public override Task StartAsync(CancellationToken token)
            {
                try
                {
                    this.task.Start();
                }
                catch (InvalidOperationException)
                {
                }

                return this.task;
            }

            public override int GetHashCode()
            {
                return this.schedulePriority;
            }

            public override bool Equals(IComparableTask other)
            {
                return this.CompareTo(other) == 0;
            }
        }
    }
}