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
    public class CosmosOperationCanceledException : OperationCanceledException, ICloneable
    {
        private readonly Object thisLock = new object();
        private readonly OperationCanceledException originalException;
        private readonly bool tokenCancellationRequested;

        private string lazyMessage;
        private string toStringMessage;

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
            this.toStringMessage = null;
            this.lazyMessage = null;
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
#pragma warning disable CDX1000 // DontConvertExceptionToObject
                child.AddDatum("Operation Cancelled Exception", originalException);
#pragma warning restore CDX1000 // DontConvertExceptionToObject
            }
            this.Diagnostics = new CosmosTraceDiagnostics(trace);
            this.tokenCancellationRequested = originalException.CancellationToken.IsCancellationRequested;
            this.toStringMessage = null;
            this.lazyMessage = null;
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
            this.lazyMessage = null;
            this.toStringMessage = null;
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
        public override string Message => this.EnsureLazyMessage();

        /// <inheritdoc/>
#pragma warning disable CDX1002 // DontUseExceptionStackTrace
        public override string StackTrace => this.originalException.StackTrace;
#pragma warning restore CDX1002 // DontUseExceptionStackTrace

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
            return this.EnsureToStringMessage(false);
        }

        private string EnsureLazyMessage()
        {
            if (this.lazyMessage != null)
            {
                return this.lazyMessage;
            }

            lock (this.thisLock)
            {
                return this.lazyMessage ??=
                     $"{this.originalException.Message}{Environment.NewLine}Cancellation Token has expired: {this.tokenCancellationRequested}. Learn more at: https://aka.ms/cosmosdb-tsg-request-timeout{Environment.NewLine}";
            }
        }

        internal string EnsureToStringMessage(Boolean skipDiagnostics)
        {
            if (this.toStringMessage != null)
            {
                return this.toStringMessage;
            }

            lock (this.thisLock)
            {
                if (skipDiagnostics)
                {
                    return this.toStringMessage ??=
                                         $"{this.originalException}{Environment.NewLine}Cancellation Token has expired: {this.tokenCancellationRequested}. Learn more at: https://aka.ms/cosmosdb-tsg-request-timeout";
                }
                return this.toStringMessage ??=
                     $"{this.originalException}{Environment.NewLine}Cancellation Token has expired: {this.tokenCancellationRequested}. Learn more at: https://aka.ms/cosmosdb-tsg-request-timeout{Environment.NewLine}CosmosDiagnostics: {this.Diagnostics}";
            }
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
#pragma warning disable CDX1000 // DontConvertExceptionToObject
            info.AddValue("originalException", this.originalException);
#pragma warning restore CDX1000 // DontConvertExceptionToObject
            info.AddValue("tokenCancellationRequested", this.tokenCancellationRequested);
            info.AddValue("lazyMessage", this.EnsureLazyMessage());
            info.AddValue("toStringMessage", this.EnsureToStringMessage(false));
        }

        /// <summary>
        /// Creates a shallow copy of the current exception instance.
        /// This ensures that the cloned exception retains the same properties but does not
        /// excessively proliferate stack traces or deep-copy unnecessary objects.
        /// </summary>
        /// <returns>A shallow copy of the current <see cref="CosmosOperationCanceledException"/>.</returns>
        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
