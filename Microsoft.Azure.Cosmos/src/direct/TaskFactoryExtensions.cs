//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if COSMOS_CORE
namespace Microsoft.Azure.Cosmos.Core
#else
namespace Microsoft.Azure.Documents
#endif
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
        /// <summary>Creates and starts a <see cref="System.Threading.Tasks.Task" /> on the current task scheduler.</summary>
        /// <returns>The started <see cref="System.Threading.Tasks.Task" />.</returns>
        /// <param name="taskFactory">Instance of the <see cref="System.Threading.Tasks.TaskFactory" /> to use for starting the task.</param>
        /// <param name="action">The action delegate to execute asynchronously.</param>
        /// <exception cref="System.ArgumentNullException">The exception that is thrown when the <paramref name="action" /> argument is null.</exception>
        public static Task StartNewOnCurrentTaskSchedulerAsync(this TaskFactory taskFactory, Action action)
        {
            return taskFactory.StartNew(action, default(CancellationToken), TaskCreationOptions.None, TaskScheduler.Current);
        }

        /// <summary>Creates and starts a <see cref="System.Threading.Tasks.Task" /> on the current task scheduler.</summary>
        /// <returns>The started <see cref="System.Threading.Tasks.Task" />.</returns>
        /// <param name="taskFactory">Instance of the <see cref="System.Threading.Tasks.TaskFactory" /> to use for starting the task.</param>
        /// <param name="action">The action delegate to execute asynchronously.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.Tasks.TaskFactory.CancellationToken" /> that will be assigned to the new task.</param>
        /// <exception cref="System.ObjectDisposedException">The provided <see cref="System.Threading.CancellationToken" /> has already been disposed.</exception>
        /// <exception cref="System.ArgumentNullException">The exception that is thrown when the <paramref name="action" /> argument is null.</exception>
        public static Task StartNewOnCurrentTaskSchedulerAsync(this TaskFactory taskFactory, Action action, CancellationToken cancellationToken)
        {
            return taskFactory.StartNew(action, cancellationToken, TaskCreationOptions.None, TaskScheduler.Current);
        }

        /// <summary>Creates and starts a <see cref="System.Threading.Tasks.Task" /> on the current task scheduler.</summary>
        /// <returns>The started <see cref="System.Threading.Tasks.Task" />.</returns>
        /// <param name="taskFactory">Instance of the <see cref="System.Threading.Tasks.TaskFactory" /> to use for starting the task.</param>
        /// <param name="action">The action delegate to execute asynchronously.</param>
        /// <param name="creationOptions">A TaskCreationOptions value that controls the behavior of the created <see cref="System.Threading.Tasks.Task" />.</param>
        /// <exception cref="System.ObjectDisposedException">The provided <see cref="System.Threading.CancellationToken" /> has already been disposed.</exception>
        /// <exception cref="System.ArgumentNullException">The exception that is thrown when the <paramref name="action" /> argument is null.</exception>
        public static Task StartNewOnCurrentTaskSchedulerAsync(this TaskFactory taskFactory, Action action, TaskCreationOptions creationOptions)
        {
            return taskFactory.StartNew(action, default(CancellationToken), creationOptions, TaskScheduler.Current);
        }

        /// <summary>Creates and starts a <see cref="System.Threading.Tasks.Task{TResult}" /> on the current task scheduler.</summary>
        /// <returns>The started <see cref="System.Threading.Tasks.Task{TResult}" />.</returns>
        /// <param name="taskFactory">Instance of the <see cref="System.Threading.Tasks.TaskFactory" /> to use for starting the task.</param>
        /// <param name="function">A function delegate that returns the future result to be available through the <see cref="System.Threading.Tasks.Task{TResult}" />.</param>
        /// <typeparam name="TResult">The type of the result available through the <see cref="System.Threading.Tasks.Task{TResult}" />.</typeparam>
        /// <exception cref="System.ArgumentNullException">The exception that is thrown when the <paramref name="function" /> argument is null.</exception>
        public static Task<TResult> StartNewOnCurrentTaskSchedulerAsync<TResult>(this TaskFactory taskFactory, Func<TResult> function)
        {
            return taskFactory.StartNew(function, default(CancellationToken), TaskCreationOptions.None, TaskScheduler.Current);
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

        /// <summary>Creates and starts a <see cref="System.Threading.Tasks.Task{TResult}" />.</summary>
        /// <returns>The started <see cref="System.Threading.Tasks.Task{TResult}" />.</returns>
        /// <param name="taskFactory">Instance of the <see cref="System.Threading.Tasks.TaskFactory" /> to use for starting the task.</param>
        /// <param name="function">A function delegate that returns the future result to be available through the <see cref="System.Threading.Tasks.Task{TResult}" />.</param>
        /// <param name="creationOptions">A TaskCreationOptions value that controls the behavior of the created <see cref="System.Threading.Tasks.Task{TResult}" />.</param>
        /// <typeparam name="TResult">The type of the result available through the <see cref="System.Threading.Tasks.Task{TResult}" />.</typeparam>
        /// <exception cref="System.ArgumentNullException">The exception that is thrown when the <paramref name="function" /> argument is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">The exception that is thrown when the <paramref name="creationOptions" /> argument specifies an invalid TaskCreationOptions value. The exception that is thrown when the <paramref name="creationOptions" /> argument specifies an invalid TaskCreationOptions value. For more information, see the Remarks for <see cref="M:System.Threading.Tasks.TaskFactory.FromAsync(System.Func{System.AsyncCallback,System.Object,System.IAsyncResult},System.Action{System.IAsyncResult},System.Object,System.Threading.Tasks.TaskCreationOptions)" />.</exception>
        public static Task<TResult> StartNewOnCurrentTaskSchedulerAsync<TResult>(this TaskFactory taskFactory, Func<TResult> function, TaskCreationOptions creationOptions)
        {
            return taskFactory.StartNew(function, default(CancellationToken), creationOptions, TaskScheduler.Current);
        }

        /// <summary>
        /// Extracts the value or fault of an already-completed <paramref name="completedTask"/>
        /// as a tuple, without rethrowing. A non-null <c>exception</c> (already unwrapped from
        /// any <see cref="AggregateException"/>, with cancellation mapped to
        /// <see cref="OperationCanceledException"/>) indicates failure. This is the single shared
        /// decision point for how a faulted cache task is read: both <c>AsyncCache.TryGetAsync</c>
        /// and <c>Res.Wrap</c> extract through it, so that logic can never drift between them.
        /// </summary>
        public static (T value, Exception exception) GetValueOrException<T>(this Task<T> completedTask)
        {
            if (!completedTask.IsCompleted)
            {
                throw new InvalidOperationException("GetValueOrException requires an already-completed task.");
            }

            if (completedTask.Status == TaskStatus.RanToCompletion)
            {
                // Completed successfully - reading Result does not block.
#pragma warning disable VSTHRD002, VSTHRD103, AsyncFixer02
                return (completedTask.Result, null);
#pragma warning restore VSTHRD002, VSTHRD103, AsyncFixer02
            }

            Exception exception = completedTask.Exception;
            if (exception is AggregateException aggregate)
            {
                exception = aggregate.InnerException ?? aggregate;
            }

            if (exception == null)
            {
                exception = new OperationCanceledException();
            }

            return (default, exception);
        }

        /// <summary>
        /// Awaits <paramref name="task"/> without rethrowing, returning its value or fault as a
        /// tuple via <see cref="GetValueOrException{T}(Task{T})"/>. Faulted/canceled tasks are
        /// inspected via their completed state rather than awaited-to-throw, so the many callers
        /// coalesced onto a single faulted task do not each pay for a throw.
        /// </summary>
        public static Task<(T value, Exception exception)> TryAwaitAsync<T>(this Task<T> task)
        {
            if (task.IsCompleted)
            {
                return Task.FromResult(task.GetValueOrException());
            }

            TaskCompletionSource<(T value, Exception exception)> completionSource = new TaskCompletionSource<(T value, Exception exception)>();
            Task unused = task.ContinueWith(
                static (completedTask, state) =>
                {
                    TaskCompletionSource<(T value, Exception exception)> source = (TaskCompletionSource<(T value, Exception exception)>)state;
                    source.SetResult(completedTask.GetValueOrException());
                },
                completionSource);

            return completionSource.Task;
        }
    }
}