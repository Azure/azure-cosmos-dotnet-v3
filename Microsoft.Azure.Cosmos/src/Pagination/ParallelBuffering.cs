// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal static class ParallelBuffering
    {
        public static async Task BufferInParallelAsync(IEnumerable<IBufferable> bufferables, int maxConcurrency)
        {
            HashSet<Task> tasks = new HashSet<Task>();
            IEnumerator<IBufferable> bufferablesEnumerator = bufferables.GetEnumerator();
            for (int i = 0; i < maxConcurrency; i++)
            {
                if (bufferablesEnumerator.MoveNext())
                {
                    IBufferable bufferable = bufferablesEnumerator.Current;
                    tasks.Add(Task.Run(async () => await bufferable.BufferAsync()));
                }
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

                if (bufferablesEnumerator.MoveNext())
                {
                    IBufferable bufferable = bufferablesEnumerator.Current;
                    tasks.Add(Task.Run(async () => await bufferable.BufferAsync()));
                }
            }
        }
    }
}
