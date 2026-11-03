//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Custom implementation of the task scheduler used to verify ambient task scheduler propagation through SDK.
    /// </summary>
    /// <remarks>
    /// This scheduler adds no functional value other than the type of class itself, which is used in validation code.
    /// </remarks>
    internal sealed class TestTaskScheduler : TaskScheduler
    {
        /// <summary>
        /// List of tasks pending execution
        /// </summary>
        private readonly LinkedList<Task> tasks = new LinkedList<Task>();

        /// <summary>
        /// Default constructor
        /// </summary>
        public TestTaskScheduler()
        {

        }

        /// <summary>
        /// Return an enumeration of scheduled tasks
        /// </summary>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            bool lockTaken = false;

            try
            {
                Monitor.TryEnter(this.tasks, ref lockTaken);
                if (!lockTaken)
                {
                    throw new NotSupportedException();
                }

                return this.tasks;
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(this.tasks);
                }
            }
        }

        /// <summary>
        /// Enqueue task into the scheduler
        /// </summary>
        /// <param name="task">Task to be enqueued</param>
        protected override void QueueTask(Task task)
        {
            // Add the task to the list of tasks to be processed.  If there aren't enough 
            // delegates currently queued or running to process tasks, schedule another. 
            lock (this.tasks)
            {
                this.tasks.AddLast(task);
                this.NotifyThreadPoolOfPendingWork();
            }
        }

        /// <summary>
        /// Attempts to execute the specified task on the current thread. 
        /// </summary>
        /// <param name="task">Task to execute</param>
        /// <param name="taskWasPreviouslyQueued">Flag that indicates whether this task was queued previously using QueueTask() routine.</param>
        /// <returns></returns>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If the task was previously queued, remove it from the queue
            if (taskWasPreviouslyQueued)
            {
                // Try to run the task. 
                if (!TryDequeue(task))
                {
                    return false;
                }
            }

            return base.TryExecuteTask(task);
        }

        /// <summary>
        /// Attempt to remove a previously scheduled task from the scheduler. 
        /// </summary>
        /// <param name="task">Task do dequeue.</param>
        /// <returns>True if task was dequeued, False otherwise.</returns>
        protected sealed override bool TryDequeue(Task task)
        {
            lock (this.tasks)
            {
                return this.tasks.Remove(task);
            }
        }

        /// <summary>
        /// Schedule work against the thread pool to process tasks in the queue
        /// </summary>
        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                // Process all available items in the queue.
                while (true)
                {
                    Task item;
                    lock (this.tasks)
                    {
                        // When there are no more items to be processed,
                        // note that we're done processing, and get out.
                        if (this.tasks.Count == 0)
                        {
                            break;
                        }

                        // Get the next item from the queue
                        item = this.tasks.First.Value;
                        this.tasks.RemoveFirst();
                    }

                    // Execute the task we pulled out of the queue
                    base.TryExecuteTask(item);
                }
            }, 
            null);
        }
    }
}
