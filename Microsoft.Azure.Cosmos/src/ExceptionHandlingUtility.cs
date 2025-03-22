// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Utility for post-processing of exceptions.
    /// </summary>
    internal static class ExceptionHandlingUtility
    {
        /// <summary>
        /// Creates a shallow copy of specific exception types (e.g., TaskCanceledException, TimeoutException, OperationCanceledException) 
        /// to prevent excessive stack trace growth and rethrows them. All other exceptions are not processed.
        /// </summary>
        public static void CloneAndRethrowException(Exception e)
        {
            Exception ex = e switch
            {
                ICloneable cloneableEx => (Exception)cloneableEx.Clone(),
                TaskCanceledException taskCanceledEx => AddMessageData(new TaskCanceledException(taskCanceledEx.Message, taskCanceledEx), e),
                TimeoutException timeoutEx => AddMessageData(new TimeoutException(timeoutEx.Message, timeoutEx), e),
                _ => null
            };

            if (ex is not null)
            {
                throw ex;
            }
        }

        private static Exception AddMessageData(Exception target, Exception source)
        {
            if (source.Data.Contains("Message"))
            {
                target.Data["Message"] = source.Data["Message"];
            }

            return target;
        }
    }
}
