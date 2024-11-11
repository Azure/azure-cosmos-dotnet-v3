//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class ParallelAsync
    {
        public static Task ForEachAsync<T>(this IEnumerable<T> source, int dop, Func<T, Task> body)
        {
            return Task.WhenAll(
                Partitioner.Create(source)
                    .GetPartitions(dop)
                    .Select(partition =>
                        // Schedule execution on current .NET task scheduler.
                        // Compute gateway uses custom task scheduler to track tenant resource utilization.
                        // Task.Run() switches to default task scheduler for entire sub-tree of tasks making compute gateway incapable of tracking resource usage accurately.
                        // Task.Factory.StartNew() allows specifying task scheduler to use.
                        Task.Factory.StartNew(async () =>
                        {
                            using (partition)
                            {
                                while (partition.MoveNext())
                                {
                                    await body(partition.Current);
                                }
                            }
                        },
                        default,
                        TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.Current)
                            .Unwrap()));
        }
    }
}