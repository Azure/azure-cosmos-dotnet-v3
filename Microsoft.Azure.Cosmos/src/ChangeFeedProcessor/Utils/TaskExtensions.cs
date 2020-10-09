//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Utils
{
    using System.Threading.Tasks;

    internal static class TaskExtensions
    {
        public static void LogException(this Task task)
        {
            if (!task.IsCompleted)
            {
                _ = task.ContinueWith(t => Extensions.TraceException(t.Exception), TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
            }
            else if (task.IsFaulted)
            {
                Extensions.TraceException(task.Exception);
            }
        }
    }
}