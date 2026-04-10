namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A task scheduler that processes a single task at a time.
    /// It is used for testing async deadlocks
    /// </summary>
    /// <see cref="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/758"/>
    internal sealed class SingleTaskScheduler : TaskScheduler
    {
        private readonly Queue<Task> TaskQueue = new Queue<Task>();
        private readonly object SyncObject = new object();
        private bool IsActive = false;

        public override int MaximumConcurrencyLevel => 1;

        protected override IEnumerable<Task> GetScheduledTasks() { throw new NotSupportedException(); }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        protected override void QueueTask(Task task)
        {
            lock (this.SyncObject)
            {
                this.TaskQueue.Enqueue(task);

                if (!this.IsActive)
                {
                    this.IsActive = true;
                    ThreadPool.QueueUserWorkItem(
                        _ =>
                        {
                            Task nextTask = null;
                            while ((nextTask = this.TryGetNextTask()) != null)
                            {
                                this.TryExecuteTask(nextTask);
                            }
                        });
                }
            }
        }

        private Task TryGetNextTask()
        {
            lock (this.SyncObject)
            {
                if (this.TaskQueue.Count > 0)
                {
                    return this.TaskQueue.Dequeue();
                }
                else
                {
                    this.IsActive = false;
                    return null;
                }
            }
        }
    }
}