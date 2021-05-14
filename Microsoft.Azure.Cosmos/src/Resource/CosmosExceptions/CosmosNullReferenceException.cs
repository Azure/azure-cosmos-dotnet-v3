//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// The exception is a wrapper for NullReferenceException. This wrapper
    /// adds a way to access the CosmosDiagnostics and appends additional information
    /// to the message for easier troubleshooting.
    /// </summary>
    internal class CosmosNullReferenceException : NullReferenceException
    {
        private readonly NullReferenceException originalException;

        /// <summary>
        /// Create an instance of CosmosNullReferenceException
        /// </summary>
        internal CosmosNullReferenceException(
            NullReferenceException originalException,
            ITrace trace)
        {
            this.originalException = originalException ?? throw new ArgumentNullException(nameof(originalException));
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            this.Diagnostics = new CosmosTraceDiagnostics(trace);

            string diagnosticString;
            try
            {
                diagnosticString = this.Diagnostics.ToString();
            }
            catch (Exception e)
            {
                diagnosticString = $"CosmosDiagnostics.ToString() failed: {e}";
            }

            this.Message = $"{originalException.Message} CosmosDiagnostics: {diagnosticString}";

        }

        /// <inheritdoc/>
        public override string Source
        {
            get => this.originalException.Source;
            set => this.originalException.Source = value;
        }

        /// <inheritdoc/>
        public override string Message { get; }

        /// <inheritdoc/>
        public override string StackTrace => this.originalException.StackTrace;

        /// <inheritdoc/>
        public override IDictionary Data => this.originalException.Data;

        /// <summary>
        /// Gets the diagnostics for the request
        /// </summary>
        public CosmosDiagnostics Diagnostics { get; }

        /// <inheritdoc/>
        public override string HelpLink
        {
            get => this.originalException.HelpLink;
            set => this.originalException.HelpLink = value;
        }

        /// <inheritdoc/>
        public override Exception GetBaseException()
        {
            return this.originalException.GetBaseException();
        }
    }
}
