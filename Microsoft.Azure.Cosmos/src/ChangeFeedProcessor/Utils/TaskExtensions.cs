//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    internal static class TaskExtensions
    {
        public static void LogException(this Task task)
        {
#pragma warning disable VSTHRD110 // Observe result of async calls
            task.ContinueWith(_ => Extensions.TraceException(task.Exception), TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore VSTHRD110 // Observe result of async calls
        }
    }
}