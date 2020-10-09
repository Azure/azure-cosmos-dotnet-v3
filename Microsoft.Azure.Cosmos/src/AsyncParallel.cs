// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class AsyncParallel
    {
        public static Task ForEachAllOrNothingAsync<TSource>(
            IEnumerable<TSource> source,
            Func<TSource, CancellationToken, Task> taskFactory,
            int maxDegreeOfParallelism,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (taskFactory == null)
            {
                throw new ArgumentNullException(nameof(taskFactory));
            }

            if (maxDegreeOfParallelism < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
            }

            // Semaphore to uphold maxDegreeOfParallelism
            SemaphoreSlim semaphore = new SemaphoreSlim(
                initialCount: maxDegreeOfParallelism,
                maxCount: maxDegreeOfParallelism);

            // Create a cancellation token that be in the cancelled state if 
            // the user supplied one is cancelled or 
            // any of the child tasks end up in a faulted state
            CancellationTokenSource linkedCancellationTokensSource = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);

            IEnumerable<Task> tasks = source.Select(item => ProccessAsync<TSource>(
                item,
                taskFactory,
                semaphore,
                linkedCancellationTokensSource));
            return Task.WhenAll(tasks);
        }

        private static async Task ProccessAsync<TSource>(
            TSource source,
            Func<TSource, CancellationToken, Task> taskFactory,
            SemaphoreSlim semaphore,
            CancellationTokenSource cancellationTokenSource)
        {
            await semaphore.WaitAsync();

            CancellationToken cancellationToken = cancellationTokenSource.Token;
            Task task = taskFactory(source, cancellationToken);

            try 
            {
                await task;
            }
            catch
            {
                // Let your siblings know to stop what they are doing.
                cancellationTokenSource.Cancel();
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
