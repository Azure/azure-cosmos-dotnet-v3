//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Runtime.Serialization;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// The exception that is thrown in a thread upon cancellation of an operation that
    ///  the thread was executing. This extends the OperationCanceledException to include the
    ///  diagnostics of the operation that was canceled.
    /// </summary> 
    [Serializable]
    public class CosmosOperationCanceledException : OperationCanceledException
    {
        private readonly OperationCanceledException originalException;
        private readonly Lazy<string> lazyMessage;
        private readonly Lazy<string> toStringMessage;
        private readonly bool tokenCancellationRequested;

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
            this.originalException = originalException ?? throw new ArgumentNullException(nameof(originalException));
            this.Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            this.tokenCancellationRequested = originalException.CancellationToken.IsCancellationRequested;
            this.toStringMessage = this.CreateToStringMessage();
            this.lazyMessage = this.CreateLazyMessage();
        }

        internal CosmosOperationCanceledException(
            OperationCanceledException originalException,
            ITrace trace)
            : base(originalException.CancellationToken)
        {
            this.originalException = originalException ?? throw new ArgumentNullException(nameof(originalException));
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            using (ITrace child = trace.StartChild("CosmosOperationCanceledException"))
            {
                child.AddDatum("Operation Cancelled Exception", originalException);
            }
            this.Diagnostics = new CosmosTraceDiagnostics(trace);
            this.tokenCancellationRequested = originalException.CancellationToken.IsCancellationRequested;
            this.toStringMessage = this.CreateToStringMessage();
            this.lazyMessage = this.CreateLazyMessage();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosOperationCanceledException"/> class.
        /// </summary>
        /// <param name="info">The SerializationInfo object that holds serialized object data for the exception being thrown.</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
        protected CosmosOperationCanceledException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.originalException = (OperationCanceledException)info.GetValue("originalException", typeof(OperationCanceledException));
            this.tokenCancellationRequested = (bool)info.GetValue("tokenCancellationRequested", typeof(bool));
            this.lazyMessage = new Lazy<string>(() => (string)info.GetValue("lazyMessage", typeof(string)));
            this.toStringMessage = new Lazy<string>(() => (string)info.GetValue("toStringMessage", typeof(string)));
            //Diagnostics cannot be serialized
            this.Diagnostics = new CosmosTraceDiagnostics(NoOpTrace.Singleton);
        }

        /// <inheritdoc/>
        public override string Source
        {
            get => this.originalException.Source;
            set => this.originalException.Source = value;
        }

        /// <inheritdoc/>
        public override string Message => this.lazyMessage.Value;

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
            return this.toStringMessage.Value;
        }

        private Lazy<string> CreateLazyMessage()
        {
            return new Lazy<string>(() => $"{this.originalException.Message}{Environment.NewLine}Cancellation Token has expired: {this.tokenCancellationRequested}. Learn more at: https://aka.ms/cosmosdb-tsg-request-timeout{Environment.NewLine}CosmosDiagnostics: {this.Diagnostics}");
        }
        private Lazy<string> CreateToStringMessage()
        {
            return new Lazy<string>(() => $"{this.originalException}{Environment.NewLine}Cancellation Token has expired: {this.tokenCancellationRequested}. Learn more at: https://aka.ms/cosmosdb-tsg-request-timeout{Environment.NewLine}CosmosDiagnostics: {this.Diagnostics}");
        }
         
        /// <summary>
        /// RecordOtelAttributes
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="scope"></param>
        internal static void RecordOtelAttributes(CosmosOperationCanceledException exception, DiagnosticScope scope)
        {
            scope.AddAttribute(OpenTelemetryAttributeKeys.Region, 
                ClientTelemetryHelper.GetContactedRegions(exception.Diagnostics?.GetContactedRegions()));
            scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionMessage, 
                exception.GetBaseException().Message);

            CosmosDbEventSource.RecordDiagnosticsForExceptions(exception.Diagnostics);
        }

        /// <summary>
        /// Sets the System.Runtime.Serialization.SerializationInfo with information about the exception.
        /// </summary>
        /// <param name="info">The SerializationInfo object that holds serialized object data for the exception being thrown.</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("originalException", this.originalException);
            info.AddValue("tokenCancellationRequested", this.tokenCancellationRequested);
            info.AddValue("lazyMessage", this.lazyMessage.Value);
            info.AddValue("toStringMessage", this.toStringMessage.Value);
        }
    }
}
