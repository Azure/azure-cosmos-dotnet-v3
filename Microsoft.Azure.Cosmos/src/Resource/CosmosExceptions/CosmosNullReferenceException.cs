//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// The exception is a wrapper for NullReferenceExceptions. This wrapper
    /// adds a way to access the CosmosDiagnostics and appends additional information
    /// to the message for easier troubleshooting.
    /// </summary>
    internal class CosmosNullReferenceException : NullReferenceException, ICloneable
    {
        private readonly NullReferenceException originalException;

        /// <summary>
        /// Create an instance of CosmosNullReferenceException
        /// </summary>
        internal CosmosNullReferenceException(
            NullReferenceException originalException,
            ITrace trace) 
            : base(originalException?.Message ?? throw new ArgumentNullException(nameof(originalException)), originalException ?? throw new ArgumentNullException(nameof(originalException)))
        {
            this.originalException = originalException;

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            this.Diagnostics = new CosmosTraceDiagnostics(trace);
        }

        /// <inheritdoc/>
        public override string Source
        {
            get => this.originalException.Source;
            set => this.originalException.Source = value;
        }

        /// <inheritdoc/>
        public override string Message => this.originalException.Message + this.Diagnostics.ToString();

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
            return $"{this.originalException} {Environment.NewLine} CosmosDiagnostics: {this.Diagnostics}";
        }

        internal static void RecordOtelAttributes(CosmosNullReferenceException exception, DiagnosticScope scope)
        {
            scope.AddAttribute(OpenTelemetryAttributeKeys.Region, 
                ClientTelemetryHelper.GetContactedRegions(exception.Diagnostics?.GetContactedRegions()));
            scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionMessage, exception.GetBaseException().Message);

            CosmosDbEventSource.RecordDiagnosticsForExceptions(exception.Diagnostics);
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
