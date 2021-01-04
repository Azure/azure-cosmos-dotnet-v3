//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Extensions to task factory methods that are meant to be used as patterns of invocation of asynchronous operations
    /// inside compute gateway ensuring continuity of execution on the current task scheduler.
    /// Task scheduler is used to track resource consumption per tenant so it is critical that all async activity
    /// pertaining to the tenant runs on the same task scheduler.
    /// </summary>
    internal static class TaskFactoryExtensions
    {
        /// <summary>Creates and starts a <see cref="System.Threading.Tasks.Task{TResult}" /> on the current task scheduler.</summary>
        /// <returns>The started <see cref="System.Threading.Tasks.Task{TResult}" />.</returns>
        /// <param name="taskFactory">Instance of the <see cref="System.Threading.Tasks.TaskFactory" /> to use for starting the task.</param>
        /// <param name="function">A function delegate that returns the future result to be available through the <see cref="System.Threading.Tasks.Task{TResult}" />.</param>
        /// <typeparam name="TResult">The type of the result available through the <see cref="System.Threading.Tasks.Task{TResult}" />.</typeparam>
        /// <exception cref="System.ArgumentNullException">The exception that is thrown when the <paramref name="function" /> argument is null.</exception>
        public static Task<TResult> StartNewOnCurrentTaskSchedulerAsync<TResult>(this TaskFactory taskFactory, Func<TResult> function)
        {
            return taskFactory.StartNew(function, default, TaskCreationOptions.None, TaskScheduler.Current);
        }

        /// <summary>Creates and starts a <see cref="System.Threading.Tasks.Task{TResult}" /> on the current task scheduler.</summary>
        /// <returns>The started <see cref="System.Threading.Tasks.Task{TResult}" />.</returns>
        /// <param name="taskFactory">Instance of the <see cref="System.Threading.Tasks.TaskFactory" /> to use for starting the task.</param>
        /// <param name="function">A function delegate that returns the future result to be available through the <see cref="System.Threading.Tasks.Task{TResult}" />.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.Tasks.TaskFactory.CancellationToken" /> that will be assigned to the new task.</param>
        /// <typeparam name="TResult">The type of the result available through the <see cref="System.Threading.Tasks.Task{TResult}" />.</typeparam>
        /// <exception cref="System.ArgumentNullException">The exception that is thrown when the <paramref name="function" /> argument is null.</exception>
        public static Task<TResult> StartNewOnCurrentTaskSchedulerAsync<TResult>(this TaskFactory taskFactory, Func<TResult> function, CancellationToken cancellationToken)
        {
            return taskFactory.StartNew(function, cancellationToken, TaskCreationOptions.None, TaskScheduler.Current);
        }
    }
}