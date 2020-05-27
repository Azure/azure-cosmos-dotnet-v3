//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Threading;

    /// <summary>
    /// The exception that is thrown in a thread upon cancellation of an operation that
    ///  the thread was executing. This extends the OperationCanceledException to include the
    ///  diagnostics of the operation that was canceled.
    /// </summary>
    public class CosmosOperationCanceledException : OperationCanceledException
    {
        private readonly OperationCanceledException originalException;

        internal static OperationCanceledException Create(
            OperationCanceledException originalException,
            CosmosDiagnosticsContext diagnosticsContext)
        {
            if (originalException == null)
            {
                throw new ArgumentNullException(nameof(originalException));
            }

            if (diagnosticsContext == null)
            {
                throw new ArgumentNullException(nameof(diagnosticsContext));
            }

            return new CosmosOperationCanceledException(
                    originalException,
                    diagnosticsContext.Diagnostics);
        }

        /// <summary>
        /// Create an instance of CosmosOperationCanceledException
        /// </summary>
        /// <param name="originalException">The original operation canceled exception</param>
        /// <param name="diagnostics"></param>
        public CosmosOperationCanceledException(
            OperationCanceledException originalException,
            CosmosDiagnostics diagnostics)
            : base(originalException.CancellationToken)
        {
            this.originalException = originalException;
            this.Diagnostics = diagnostics;
        }

        /// <inheritdoc/>
        public override string Source
        {
            get => this.originalException.Source;
            set => this.originalException.Source = value;
        }

        /// <inheritdoc/>
        public override string Message => this.originalException.Message;

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

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{this.originalException.ToString()} {Environment.NewLine}CosmosDiagnostics: {this.Diagnostics.ToString()}";
        }
    }
}
