//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading.Tasks;

    internal static class CompletedTask
    {
        private static Task instance;

        public static Task Instance
        {
            get
            {
                if (CompletedTask.instance == null)
                {
                    TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>();
                    completionSource.SetResult(true);
                    CompletedTask.instance = completionSource.Task;
                }
                return CompletedTask.instance;
            }
        }

        public static Task<T> SetExceptionAsync<T>(Exception exception)
        {
            TaskCompletionSource<T> completionSource = new TaskCompletionSource<T>();
            completionSource.SetException(exception);
            return completionSource.Task;
        }
    }
}
