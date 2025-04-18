//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Globalization;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// The exception is a wrapper for ObjectDisposedExceptions. This wrapper
    /// adds a way to access the CosmosDiagnostics and appends additional information
    /// to the message for easier troubleshooting.
    /// </summary>
    internal class CosmosObjectDisposedException : ObjectDisposedException, ICloneable
    {
        private readonly ObjectDisposedException originalException;
        private readonly CosmosClient cosmosClient;

        /// <summary>
        /// Create an instance of CosmosObjectDisposedException
        /// </summary>
        internal CosmosObjectDisposedException(
            ObjectDisposedException originalException,
            CosmosClient cosmosClient,
            ITrace trace) 
            : base(originalException.ObjectName)
        {
            this.cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(CosmosClient));
            this.originalException = originalException ?? throw new ArgumentNullException(nameof(originalException));

            string additionalInfo = $"CosmosClient Endpoint: {this.cosmosClient.Endpoint}; Created at: {this.cosmosClient.ClientConfigurationTraceDatum.ClientCreatedDateTimeUtc.ToString("o", CultureInfo.InvariantCulture)};" +
                $" UserAgent: {this.cosmosClient.ClientConfigurationTraceDatum.UserAgentContainer.UserAgent};";
            this.Message = this.cosmosClient.DisposedDateTimeUtc.HasValue
                ? $"Cannot access a disposed 'CosmosClient'. Follow best practices and use the CosmosClient as a singleton." +
                    $" CosmosClient was disposed at: {this.cosmosClient.DisposedDateTimeUtc.Value.ToString("o", CultureInfo.InvariantCulture)}; {additionalInfo}"
                : $"{originalException.Message} The CosmosClient is still active and NOT disposed of. {additionalInfo}";

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
        public override string Message { get; }

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
            return $"{this.Message} {Environment.NewLine}CosmosDiagnostics: {this.Diagnostics}";
        }

        /// <summary>
        /// RecordOtelAttributes
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="scope"></param>
        internal static void RecordOtelAttributes(CosmosObjectDisposedException exception, DiagnosticScope scope)
        {
            scope.AddAttribute(OpenTelemetryAttributeKeys.Region, 
                ClientTelemetryHelper.GetContactedRegions(exception.Diagnostics?.GetContactedRegions()));
            scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionMessage, exception.Message);

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
