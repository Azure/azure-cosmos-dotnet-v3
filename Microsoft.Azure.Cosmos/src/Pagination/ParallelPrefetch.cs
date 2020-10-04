// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class ParallelPrefetch
    {
        public static async Task PrefetchInParallelAsync(IEnumerable<IPrefetcher> prefetchers, int maxConcurrency, CancellationToken cancellationToken)
        {
            if (prefetchers == null)
            {
                throw new ArgumentNullException(nameof(prefetchers));
            }

            HashSet<Task> tasks = new HashSet<Task>();
            IEnumerator<IPrefetcher> prefetchersEnumerator = prefetchers.GetEnumerator();
            for (int i = 0; i < maxConcurrency; i++)
            {
                if (!prefetchersEnumerator.MoveNext())
                {
                    break;
                }

                IPrefetcher prefetcher = prefetchersEnumerator.Current;
                tasks.Add(Task.Run(async () => await prefetcher.PrefetchAsync(cancellationToken)));
            }

            while (tasks.Count != 0)
            {
                Task completedTask = await Task.WhenAny(tasks);
                tasks.Remove(completedTask);
                try
                {
                    await completedTask;
                }
                catch
                {
                    // Observe the remaining tasks
                    try
                    {
                        await Task.WhenAll(tasks);
                    }
                    catch
                    {
                    }

                    throw;
                }

                if (prefetchersEnumerator.MoveNext())
                {
                    IPrefetcher bufferable = prefetchersEnumerator.Current;
                    tasks.Add(Task.Run(async () => await bufferable.PrefetchAsync(cancellationToken)));
                }
            }
        }
    }
}
