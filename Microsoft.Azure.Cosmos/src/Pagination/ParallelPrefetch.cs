// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal static class ParallelPrefetch
    {
        public static async Task PrefetchInParallelAsync(
            IEnumerable<IPrefetcher> prefetchers,
            int maxConcurrency,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (prefetchers == null)
            {
                throw new ArgumentNullException(nameof(prefetchers));
            }

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (maxConcurrency <= 0)
            {
                return;
            }

            using (ITrace prefetchTrace = trace.StartChild(name: "Prefetching", TraceComponent.Pagination, TraceLevel.Info))
            {
                SemaphoreSlim throttler = new SemaphoreSlim(initialCount: maxConcurrency);
                IEnumerator<IPrefetcher> prefetchersEnumerator = prefetchers.GetEnumerator();
                List<Func<Task>> actions = new List<Func<Task>>();
                while (prefetchersEnumerator.MoveNext())
                {
                    IPrefetcher prefetcher = prefetchersEnumerator.Current;
                    ITrace prefetchChildTrace = prefetchTrace.StartChild(name: "Prefetching child", TraceComponent.Pagination, TraceLevel.Info);
                    actions.Add(async () =>
                    {
                        try
                        {
                            await throttler.WaitAsync();
                            prefetchChildTrace.ResetDuration();
                            await prefetcher.PrefetchAsync(prefetchChildTrace, cancellationToken);
                        }
                        catch (Exception exception)
                        {
                            prefetchChildTrace.AddDatum("Exception", new ExceptionTraceDatum(exception));
                            throw;
                        }
                        finally
                        {
                            prefetchChildTrace.Dispose();
                            throttler.Release();
                        }
                    });
                }

                IEnumerable<Task> tasks = actions.Select(x => Task.Run(x));
                await Task.WhenAll(tasks);
            }
        }
    }
}
