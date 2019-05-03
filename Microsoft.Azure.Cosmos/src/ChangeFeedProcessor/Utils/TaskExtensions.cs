//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Utils
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Logging;

    internal static class TaskExtensions
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();

        public static void LogException(this Task task)
        {
            task.ContinueWith(_ => Logger.ErrorException("exception caught", task.Exception), TaskContinuationOptions.OnlyOnFaulted);
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