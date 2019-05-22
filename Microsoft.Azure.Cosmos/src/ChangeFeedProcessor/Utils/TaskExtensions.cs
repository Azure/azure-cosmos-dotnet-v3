//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Utils
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal static class TaskExtensions
    {
        public static void LogException(this Task task)
        {
            task.ContinueWith(_ => DefaultTrace.TraceException(task.Exception), TaskContinuationOptions.OnlyOnFaulted);
        }

        public static async Task IgnoreException(this Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // ignore
            }
        }
    }
}