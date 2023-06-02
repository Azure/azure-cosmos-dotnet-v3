// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Diagnostics;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// This class is used to generate Activities with Azure.Cosmos.Operation Source Name
    /// </summary>
    internal static class OpenTelemetryRecorderFactory
    {
        /// <summary>
        /// Singleton to make sure we only have one instance of the DiagnosticScopeFactory and pattern matching of listener happens only once
        /// </summary>
        private static readonly Lazy<DiagnosticScopeFactory> LazyOperationScopeFactory = new Lazy<DiagnosticScopeFactory>(
            valueFactory: () => new DiagnosticScopeFactory(
                           clientNamespace: $"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.{OpenTelemetryAttributeKeys.OperationPrefix}",
                           resourceProviderNamespace: OpenTelemetryAttributeKeys.ResourceProviderNamespace,
                           isActivityEnabled: true,
                           suppressNestedClientActivities: true),
            isThreadSafe: true);

        private static readonly Lazy<DiagnosticScopeFactory> LazyNetworkScopeFactory = new Lazy<DiagnosticScopeFactory>(
            valueFactory: () => new DiagnosticScopeFactory(
                           clientNamespace: $"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.{OpenTelemetryAttributeKeys.NetworkLevelPrefix}",
                           resourceProviderNamespace: OpenTelemetryAttributeKeys.ResourceProviderNamespace,
                           isActivityEnabled: true,
                           suppressNestedClientActivities: true),
            isThreadSafe: true);

        public static OpenTelemetryCoreRecorder CreateRecorder(string operationName,
            string containerName,
            string databaseName,
            Documents.OperationType operationType,
            RequestOptions requestOptions, 
            ITrace trace,
            CosmosClientContext clientContext)
        {
            OpenTelemetryCoreRecorder openTelemetryRecorder = default;
            if (clientContext is { ClientOptions.IsDistributedTracingEnabled: true })
            {
                // If there is no source then it will return default otherwise a valid diagnostic scope
                DiagnosticScope scope = LazyOperationScopeFactory.Value.CreateScope(name: operationName,
                                 kind: clientContext.ClientOptions.ConnectionMode == ConnectionMode.Gateway ? DiagnosticScope.ActivityKind.Internal : DiagnosticScope.ActivityKind.Client);

                // Record values only when we have a valid Diagnostic Scope
                if (scope.IsEnabled)
                {
                    scope.SetDisplayName($"{operationName} {containerName}.{databaseName}");

                    openTelemetryRecorder = OpenTelemetryCoreRecorder.CreateOperationLevelParentActivity(
                        operationScope: scope,
                        operationName: operationName,
                        containerName: containerName,
                        databaseName: databaseName,
                        operationType: operationType,
                        clientContext: clientContext,
                        config: requestOptions?.DistributedTracingOptions ?? clientContext.ClientOptions?.DistributedTracingOptions);
                }
#if !INTERNAL
                else if (Activity.Current is null)
                {
                    DiagnosticScope requestScope = LazyNetworkScopeFactory.Value.CreateScope(name: operationName);

                    openTelemetryRecorder = requestScope.IsEnabled ? OpenTelemetryCoreRecorder.CreateNetworkLevelParentActivity(networkScope: requestScope) : OpenTelemetryCoreRecorder.CreateParentActivity(operationName);
                }
#endif
                trace.AddDatum("DistributedTraceId", Activity.Current?.TraceId);
            }
            return openTelemetryRecorder;
        }
    }
}
