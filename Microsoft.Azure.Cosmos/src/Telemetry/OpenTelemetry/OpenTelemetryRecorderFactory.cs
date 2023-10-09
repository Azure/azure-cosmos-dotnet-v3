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
            if (clientContext is { ClientOptions.CosmosClientTelemetryOptions.DisableDistributedTracing: false })
            {
                // If there is no source then it will return default otherwise a valid diagnostic scope
                DiagnosticScope scope = LazyOperationScopeFactory.Value.CreateScope(name: operationName,
                                 kind: clientContext.ClientOptions.ConnectionMode == ConnectionMode.Gateway ? DiagnosticScope.ActivityKind.Internal : DiagnosticScope.ActivityKind.Client);

                // Need a parent activity id associated with the operation which is logged in diagnostics and used for tracing purpose.
                // If there are listeners at operation level then scope is enabled and it tries to create activity.
                // However, if available listeners are not subscribed to operation level event then it will lead to scope being enabled but no activity is created.
                if (scope.IsEnabled)
                {
                    scope.SetDisplayName($"{operationName} {containerName}");

                    openTelemetryRecorder = OpenTelemetryCoreRecorder.CreateOperationLevelParentActivity(
                        operationScope: scope,
                        operationName: operationName,
                        containerName: containerName,
                        databaseName: databaseName,
                        operationType: operationType,
                        clientContext: clientContext,
                        config: requestOptions?.CosmosThresholdOptions ?? clientContext.ClientOptions?.CosmosClientTelemetryOptions.CosmosThresholdOptions);
                }
#if !INTERNAL
                // Need a parent activity which groups all network activities under it and is logged in diagnostics and used for tracing purpose.
                // If there are listeners at network level then scope is enabled and it tries to create activity.
                // However, if available listeners are not subscribed to network event then it will lead to scope being enabled but no activity is created.
                else
                {
                    DiagnosticScope requestScope = LazyNetworkScopeFactory.Value.CreateScope(name: operationName);
                    openTelemetryRecorder = requestScope.IsEnabled ? OpenTelemetryCoreRecorder.CreateNetworkLevelParentActivity(networkScope: requestScope) : openTelemetryRecorder;
                }

                // If there are no listeners at operation level and network level and no parent activity created.
                // Then create a dummy activity as there should be a parent level activity always when Distributed tracing is on.
                // The parent activity id is logged in diagnostics and used for tracing purpose.
                if (Activity.Current is null)
                {
                    openTelemetryRecorder = OpenTelemetryCoreRecorder.CreateParentActivity(operationName);
                }
#endif
                // Safety check as diagnostic logs should not break the code.
                if (Activity.Current?.TraceId != null)
                {
                    trace.AddDatum("DistributedTraceId", Activity.Current.TraceId);
                }
            }
            return openTelemetryRecorder;
        }
    }
}
