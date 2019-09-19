//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections.Generic;
    using Microsoft.Azure.Documents;

    internal sealed class ComparableTaskScheduler : IDisposable
    {
        private const int MinimumBatchSize = 1;
        private readonly AsyncCollection<IComparableTask> taskQueue;
        private readonly ConcurrentDictionary<IComparableTask, Task> delayedTasks;
        private readonly CancellationTokenSource tokenSource;
        private readonly SemaphoreSlim canRunTaskSemaphoreSlim;
        private readonly Task schedulerTask;
        private int maximumConcurrencyLevel;
        private volatile bool isStopped;

        public ComparableTaskScheduler()
            : this(Environment.ProcessorCount)
        {
        }

        public ComparableTaskScheduler(int maximumConcurrencyLevel)
            : this(Enumerable.Empty<IComparableTask>(), maximumConcurrencyLevel)
        {
        }

        public ComparableTaskScheduler(IEnumerable<IComparableTask> tasks, int maximumConcurrencyLevel)
        {
            this.taskQueue = new AsyncCollection<IComparableTask>(new PriorityQueue<IComparableTask>(tasks, true));
            this.delayedTasks = new ConcurrentDictionary<IComparableTask, Task>();
            this.maximumConcurrencyLevel = maximumConcurrencyLevel;
            this.tokenSource = new CancellationTokenSource();
            this.canRunTaskSemaphoreSlim = new SemaphoreSlim(maximumConcurrencyLevel);
            this.schedulerTask = this.ScheduleAsync();
        }

        public int MaximumConcurrencyLevel
        {
            get
            {
                return this.maximumConcurrencyLevel;
            }
        }

        public int CurrentRunningTaskCount
        {
            get
            {
                return this.maximumConcurrencyLevel - Math.Max(0, this.canRunTaskSemaphoreSlim.CurrentCount);
            }
        }

        public bool IsStopped
        {
            get
            {
                return this.isStopped;
            }
        }

        private CancellationToken CancellationToken
        {
            get
            {
                return this.tokenSource.Token;
            }
        }

        public void IncreaseMaximumConcurrencyLevel(int delta)
        {
            if (delta <= 0)
            {
                throw new ArgumentOutOfRangeException("delta must be a positive number.");
            }

            this.canRunTaskSemaphoreSlim.Release(delta);
            this.maximumConcurrencyLevel += delta;
        }

        public void Dispose()
        {
            this.Stop();
        }

        public void Stop()
        {
            this.isStopped = true;
            this.tokenSource.Cancel();
            this.delayedTasks.Clear();
        }

        public bool TryQueueTask(IComparableTask comparableTask, TimeSpan delay = default(TimeSpan))
        {
            if (comparableTask == null)
            {
                throw new ArgumentNullException("task");
            }

            if (this.isStopped)
            {
                return false;
            }

            Task newTask = new Task<Task>(() => this.QueueDelayedTaskAsync(comparableTask, delay), this.CancellationToken);

            if (this.delayedTasks.TryAdd(comparableTask, newTask))
            {
                newTask.Start();
                return true;
            }

            return false;
        }

        private async Task QueueDelayedTaskAsync(IComparableTask comparableTask, TimeSpan delay)
        {
            Task task;
            if (this.delayedTasks.TryRemove(comparableTask, out task) && !task.IsCanceled)
            {
                if (delay > default(TimeSpan))
                {
                    await Task.Delay(delay, this.CancellationToken);
                }

                IComparableTask firstComparableTask;
                if (this.taskQueue.TryPeek(out firstComparableTask) && comparableTask.CompareTo(firstComparableTask) <= 0)
                {
                    await this.ExecuteComparableTaskAsync(comparableTask);
                }
                else
                {
                    await this.taskQueue.AddAsync(comparableTask, this.CancellationToken);
                }
            }
        }

        private async Task ScheduleAsync()
        {
            while (!this.isStopped)
            {
                await this.ExecuteComparableTaskAsync(await this.taskQueue.TakeAsync(this.CancellationToken));
            }
        }

        private async Task ExecuteComparableTaskAsync(IComparableTask comparableTask)
        {
            await this.canRunTaskSemaphoreSlim.WaitAsync(this.CancellationToken);

#pragma warning disable 4014
            // Schedule execution on current .NET task scheduler.
            // Compute gateway uses custom task scheduler to track tenant resource utilization.
            // Task.Run() switches to default task scheduler for entire sub-tree of tasks making compute gateway incapable of tracking resource usage accurately.
            // Task.Factory.StartNew() allows specifying task scheduler to use.
            Task.Factory.StartNewOnCurrentTaskSchedulerAsync(() =>
                comparableTask.StartAsync(this.CancellationToken)
                    .ContinueWith((antecendent) =>
                    {
                        this.canRunTaskSemaphoreSlim.Release();
                    },
                    TaskScheduler.Current),
                this.CancellationToken);
#pragma warning restore 4014
        }
    }
}