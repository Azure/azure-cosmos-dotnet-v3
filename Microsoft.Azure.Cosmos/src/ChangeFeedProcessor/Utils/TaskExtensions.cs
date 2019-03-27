//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.Utils
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Logging;

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