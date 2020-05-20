//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// The Cosmos Client exception
    /// </summary>
    internal class CosmosTaskCanceledException : TaskCanceledException
    {
        private readonly TaskCanceledException originalException;

        public static TaskCanceledException Create(
                   TaskCanceledException originalException,
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

            if (originalException.Task != null)
            {
                return new CosmosTaskCanceledException(
                    originalException,
                    originalException.Task,
                    diagnosticsContext);
            }

            return new CosmosTaskCanceledException(
                    originalException,
                    diagnosticsContext);
        }

        private CosmosTaskCanceledException(
            TaskCanceledException originalException,
            CosmosDiagnosticsContext diagnosticsContext)
        {
            this.originalException = originalException;
            this.DiagnosticsContext = diagnosticsContext;
        }

        private CosmosTaskCanceledException(
            TaskCanceledException originalException,
            Task originalTask,
            CosmosDiagnosticsContext diagnosticsContext)
            : base(originalTask)
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
