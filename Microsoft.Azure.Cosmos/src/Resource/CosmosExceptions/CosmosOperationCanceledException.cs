//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Threading;

    /// <summary>
    /// The Cosmos Client exception
    /// </summary>
    internal class CosmosOperationCanceledException : OperationCanceledException
    {
        private readonly OperationCanceledException originalException;

        public static OperationCanceledException Create(
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

            if (originalException.CancellationToken != null)
            {
                return new CosmosOperationCanceledException(
                    originalException,
                    originalException.CancellationToken,
                    diagnosticsContext);
            }

            return new CosmosOperationCanceledException(
                    originalException,
                    diagnosticsContext);
        }

        private CosmosOperationCanceledException(
            OperationCanceledException originalException,
            CosmosDiagnosticsContext diagnosticsContext)
        {
            this.originalException = originalException;
            this.DiagnosticsContext = diagnosticsContext;
        }

        private CosmosOperationCanceledException(
            OperationCanceledException originalException,
            CancellationToken cancellationToken,
            CosmosDiagnosticsContext diagnosticsContext)
            : base(cancellationToken)
        {
            this.originalException = originalException;
            this.DiagnosticsContext = diagnosticsContext;
        }

        public override string Source
        {
            get => this.originalException.Source;
            set => this.originalException.Source = value;
        }

        public override string Message => this.originalException.Message;

        public override string StackTrace => this.originalException.StackTrace;

        public override IDictionary Data => this.originalException.Data;

        public override string HelpLink
        {
            get => this.originalException.HelpLink;
            set => this.originalException.HelpLink = value;
        }

        internal CosmosDiagnosticsContext DiagnosticsContext { get; }

        public override Exception GetBaseException()
        {
            return this.originalException.GetBaseException();
        }

        public override string ToString()
        {
            return $"{this.originalException.ToString()} {Environment.NewLine}CosmosDiagnostics: {this.DiagnosticsContext.ToString()}";
        }
    }
}
