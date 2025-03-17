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
        public const string ExceptionHandlingForStackTraceOptimizationEnabled = "AZURE_COSMOS_STACK_TRACE_OPTIMIZATION_ENABLED";
        /// <summary>
        /// Creates a shallow copy of specific exception types (e.g., TaskCanceledException, TimeoutException, OperationCanceledException) 
        /// to prevent excessive stack trace growth and rethrows them. All other exceptions are not processed.
        /// </summary>
        /// <param name="e">The exception to process.</param>
        /// <exception cref="TaskCanceledException">Thrown if the input exception is a TaskCanceledException.</exception>
        /// <exception cref="TimeoutException">Thrown if the input exception is a TimeoutException.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the input exception is an OperationCanceledException.</exception>
        public static void CloneAndRethrowException(Exception e)
        {
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

            if (e is DocumentClientException dce)
            {
                //TODO. Add support for shallow object clones method in the direct package.
                DocumentClientException clonedDocumentClientEx = new DocumentClientException(
                    dce.Message,
                    dce.InnerException,
                    dce.StatusCode);
                clonedDocumentClientEx.Data["Message"] = e.Data["Message"];
                throw clonedDocumentClientEx;
            }

            if (e is CosmosException cosmosException)
            {
                throw cosmosException.ShallowObjectClone();
            }
        }
    }
}
