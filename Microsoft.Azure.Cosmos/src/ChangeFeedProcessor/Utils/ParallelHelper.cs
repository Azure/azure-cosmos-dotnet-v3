//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.Utils
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class ParallelHelper
    {
        public static Task ForEachAsync<TSource>(
            this IEnumerable<TSource> source,
            Func<TSource, Task> worker,
            int maxParallelTaskCount = 0,
            CancellationToken cancellationToken = new CancellationToken())
        {
            Debug.Assert(source != null, "source is null");
            Debug.Assert(worker != null, "worker is null");
            if (maxParallelTaskCount <= 0)
                maxParallelTaskCount = 100;

            return Task.WhenAll(
                Partitioner.Create(source)
                           .GetPartitions(maxParallelTaskCount)
                           .Select(partition => Task.Run(
                               async () =>
                               {
                                   using (partition)
                                   {
                                       while (partition.MoveNext())
                                       {
                                           cancellationToken.ThrowIfCancellationRequested();
                                           await worker(partition.Current).ConfigureAwait(false);
                                       }
                                   }
                               })));
        }
    }
}