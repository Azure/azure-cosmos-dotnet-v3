// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Monads
{
    using System;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class ExceptionWithStackTraceException : Exception
    {
        private static readonly string EndOfInnerExceptionString = "--- End of inner exception stack trace ---";
        private readonly StackTrace stackTrace;

        public ExceptionWithStackTraceException(StackTrace stackTrace)
            : this(message: null, innerException: null, stackTrace: stackTrace)
        {
        }

        public ExceptionWithStackTraceException(string message, StackTrace stackTrace)
            : this(message: message, innerException: null, stackTrace: stackTrace)
        {
        }

        public ExceptionWithStackTraceException(
            string message,
            Exception innerException,
            StackTrace stackTrace)
            : base(
                  message: message,
                  innerException: innerException)
        {
            if (stackTrace == null)
            {
#pragma warning disable IDE0016 // Use 'throw' expression
                throw new ArgumentNullException(nameof(stackTrace));
#pragma warning restore IDE0016 // Use 'throw' expression
            }

            this.stackTrace = stackTrace;
        }

        public override string StackTrace => this.stackTrace.ToString();

        public override string ToString()
        {
            // core2.x does not honor the StackTrace property in .ToString() (it uses the private internal stack trace).
            // core3.x uses the property as it should
            // For now just copying and pasting the 2.x implementation (this can be removed in 3.x)
            string s;

            if ((this.Message == null) || (this.Message.Length <= 0))
            {
                s = this.GetClassName();
            }
            else
            {
                s = this.GetClassName() + ": " + this.Message;
            }

            if (this.InnerException != null)
            {
#pragma warning disable CDX1003 // DontUseExceptionToString
                s = s
                    + " ---> "
                    + this.InnerException.ToString()
                    + Environment.NewLine
                    + "   "
                    + EndOfInnerExceptionString;
#pragma warning restore CDX1003 // DontUseExceptionToString

            }

#pragma warning disable CDX1002 // DontUseExceptionStackTrace
            s += Environment.NewLine + this.StackTrace;
#pragma warning restore CDX1002 // DontUseExceptionStackTrace
            return s;
        }

        private string GetClassName()
        {
            return this.GetType().ToString();
        }

        public static Exception UnWrapMonadExcepion(
            Exception exception,
            ITrace trace)
        {
            if (exception is ExceptionWithStackTraceException exceptionWithStackTrace)
            {
                return ExceptionWithStackTraceException.UnWrapMonadExcepion(exceptionWithStackTrace.InnerException, trace);
            }

            if (!(exception is CosmosOperationCanceledException)
                && exception is OperationCanceledException operationCanceledException)
            {
                return new CosmosOperationCanceledException(operationCanceledException, trace);
            }

            return exception;
        }
    }
}
