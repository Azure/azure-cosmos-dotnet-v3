//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry
{
    using System;
    using global::Azure.Core;

    /// <summary>
    /// Factory for handling telemetry trace stability modes, allowing attribute settings
    /// based on environment-specified stability mode configurations.
    /// </summary>
    internal class TracesStabilityFactory
    {
        // Specifies the stability mode for telemetry attributes, configured via the OTEL_SEMCONV_STABILITY_OPT_IN environment variable.
        private static string otelStabilityMode = Environment.GetEnvironmentVariable("OTEL_SEMCONV_STABILITY_OPT_IN");

        /// <summary>
        /// Sets trace attributes based on stability mode for standard telemetry events.
        /// </summary>
        /// <param name="scope">The diagnostic scope to be enriched with attributes.</param>
        /// <param name="operationName">The name of the operation being traced.</param>
        /// <param name="databaseName">The name of the database in context.</param>
        /// <param name="containerName">The name of the container in context.</param>
        /// <param name="accountName">The account name associated with the operation.</param>
        /// <param name="userAgent">The user agent performing the operation.</param>
        /// <param name="machineId">The machine identifier (optional).</param>
        /// <param name="clientId">The client identifier performing the operation.</param>
        /// <param name="connectionMode">The connection mode in use.</param>
        public static void SetAttributes(DiagnosticScope scope,
            string operationName,
            string databaseName,
            string containerName,
            string accountName,
            string userAgent,
            string machineId,
            string clientId,
            string connectionMode)
        {
            PopulateAttributesBasedOnMode(
           () => AppInsightClassicAttributeKeys.PopulateAttributes(scope, operationName, databaseName, containerName, accountName, userAgent, machineId, clientId, connectionMode),
           () => OpenTelemetryAttributeKeys.PopulateAttributes(scope, operationName, databaseName, containerName, accountName, userAgent, clientId, connectionMode));
        }

        /// <summary>
        /// Sets trace attributes for telemetry events related to an exception.
        /// </summary>
        /// <param name="scope">The diagnostic scope to be enriched with attributes.</param>
        /// <param name="exception">The exception that occurred.</param>
        public static void SetAttributes(DiagnosticScope scope, Exception exception)
        {
            PopulateAttributesBasedOnMode(
           () => AppInsightClassicAttributeKeys.PopulateAttributes(scope, exception),
           () => OpenTelemetryAttributeKeys.PopulateAttributes(scope, exception));
        }

        /// <summary>
        /// Sets trace attributes for telemetry events
        /// </summary>
        /// <param name="scope">The diagnostic scope to be enriched with attributes.</param>
        /// <param name="operationType">The type of operation being traced.</param>
        /// <param name="queryTextMode">The query text mode in use.</param>
        /// <param name="response">The telemetry attributes for the response.</param>
        public static void SetAttributes(DiagnosticScope scope, 
            string operationType,
            QueryTextMode? queryTextMode,
            OpenTelemetryAttributes response)
        {
            PopulateAttributesBasedOnMode(
                   () => AppInsightClassicAttributeKeys.PopulateAttributes(scope, operationType, response),
                   () => OpenTelemetryAttributeKeys.PopulateAttributes(scope, queryTextMode, response));
        }

        /// <summary>
        /// Executes attribute population actions based on the specified stability mode.
        /// </summary>
        /// <param name="populateClassicAttributes">Action to populate AppInsightClassic attributes.</param>
        /// <param name="populateOpenTelemetryAttributes">Action to populate OpenTelemetry attributes.</param>
        private static void PopulateAttributesBasedOnMode(Action populateClassicAttributes, Action populateOpenTelemetryAttributes)
        {
            switch (otelStabilityMode)
            {
                case OpenTelemetryStablityModes.Database:
                    populateOpenTelemetryAttributes();
                    break;
                case OpenTelemetryStablityModes.DatabaseDupe:
                    populateClassicAttributes();
                    populateOpenTelemetryAttributes();
                    break;
                default:
                    populateClassicAttributes();
                    break;
            }
        }

        internal static void RefreshStabilityMode()
        {
            otelStabilityMode = Environment.GetEnvironmentVariable("OTEL_SEMCONV_STABILITY_OPT_IN");
        }
    }
}
