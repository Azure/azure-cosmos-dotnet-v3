// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Monads
{
    using System;
    using System.Diagnostics;

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
                throw new ArgumentNullException(nameof(stackTrace));
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
                s = s
                    + " ---> "
                    + this.InnerException.ToString()
                    + Environment.NewLine
                    + "   "
                    + EndOfInnerExceptionString;

            }

            s += Environment.NewLine + this.StackTrace;
            return s;
        }

        private string GetClassName()
        {
            return this.GetType().ToString();
        }
    }
}
