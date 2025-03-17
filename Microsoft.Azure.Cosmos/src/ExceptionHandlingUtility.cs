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
            if (e is ICloneable ex)
            {
                throw (Exception)ex.Clone();
            }

            if (e is TaskCanceledException)
            {
                TaskCanceledException taskCanceledEx = new TaskCanceledException(e.Message, e.InnerException);
                taskCanceledEx.Data["Message"] = e.Data["Message"];
                throw taskCanceledEx;
            }

            if (e is TimeoutException)
            {
                TimeoutException timeoutEx = new TimeoutException(e.Message, e.InnerException);
                timeoutEx.Data["Message"] = e.Data["Message"];
                throw timeoutEx;
            }
        }
    }
}
