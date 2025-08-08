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
        /// Tries to create a shallow copy of specific exception types (e.g., TaskCanceledException, TimeoutException, OperationCanceledException)
        /// to prevent excessive stack trace growth. Returns true if the exception was cloned, otherwise false.
        /// </summary>
        public static bool TryCloneException(Exception e, out Exception clonedException)
        {
#pragma warning disable CDX1000 // DontConvertExceptionToObject
            clonedException = e switch
            {
                ICloneable cloneableEx => (Exception)cloneableEx.Clone(),
                OperationCanceledException operationCanceledException => AddMessageData((Exception)Activator.CreateInstance(operationCanceledException.GetType(), operationCanceledException.Message, operationCanceledException), e), //Handles all OperationCanceledException types
                TimeoutException timeoutEx => AddMessageData(new TimeoutException(timeoutEx.Message, timeoutEx), e),
                _ => null
            };
#pragma warning restore CDX1000 // DontConvertExceptionToObject

            return clonedException is not null;
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
