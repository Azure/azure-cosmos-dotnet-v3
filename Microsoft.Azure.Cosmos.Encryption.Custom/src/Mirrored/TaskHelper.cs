//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The helper function relates to the async Task.
    /// </summary>
    internal static class TaskHelper
    {
        public static Task InlineIfPossibleAsync(Func<Task> function, IRetryPolicy retryPolicy, CancellationToken cancellationToken = default)
        {
            if (SynchronizationContext.Current == null)
            {
                if (retryPolicy == null)
                {
                    // shortcut
                    return function();
                }
                else
                {
                    return BackoffRetryUtility<int>.ExecuteAsync(
                        async () =>
                        {
                            await function();
                            return 0;
                        }, retryPolicy,
                        cancellationToken);
                }
            }
            else
            {
                if (retryPolicy == null)
                {
                    // shortcut
                    return Task.Run(function);
                }
                else
                {
                    return Task.Run(() => BackoffRetryUtility<int>.ExecuteAsync(
                        async () =>
                        {
                            await function();
                            return 0;
                        }, retryPolicy,
                        cancellationToken));
                }
            }
        }

#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods
        public static Task<TResult> InlineIfPossible<TResult>(Func<Task<TResult>> function, IRetryPolicy retryPolicy, CancellationToken cancellationToken = default)
#pragma warning restore VSTHRD200 // Use "Async" suffix for async methods
        {
            if (SynchronizationContext.Current == null)
            {
                if (retryPolicy == null)
                {
                    // shortcut
                    return function();
                }
                else
                {
                    return BackoffRetryUtility<TResult>.ExecuteAsync(
                        () => function(),
                        retryPolicy,
                        cancellationToken);
                }
            }
            else
            {
                if (retryPolicy == null)
                {
                    // shortcut
                    return Task.Run(function);
                }
                else
                {
                    return Task.Run(() => BackoffRetryUtility<TResult>.ExecuteAsync(
                        () => function(),
                        retryPolicy,
                        cancellationToken));
                }
            }
        }

        public static Task<TResult> RunInlineIfNeededAsync<TResult>(Func<Task<TResult>> task)
        {
            if (SynchronizationContext.Current == null)
            {
                return task();
            }

            // Used on NETFX applications with SynchronizationContext when doing locking calls
            return Task.Run(task);
        }
    }
}