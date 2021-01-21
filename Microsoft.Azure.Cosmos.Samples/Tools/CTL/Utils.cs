//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class Utils
    {
        public static Task ForEachAsync<TSource>(
            this IEnumerable<TSource> source,
            Func<TSource, Task> worker,
            int maxParallelTaskCount = 0,
            CancellationToken cancellationToken = default)
        {
            if (maxParallelTaskCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxParallelTaskCount));
            }

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
