// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using global::Azure.Core.Pipeline;

    /// <summary>
    /// This class is used to generate Activities with Azure.Cosmos.Operation Source Name
    /// </summary>
    internal static class OpenTelemetryRecorderFactory
    {
        /// <summary>
        /// Singleton to make sure we only have one instance of the DiagnosticScopeFactory and pattern matching of listener happens only once
        /// </summary>
        private static DiagnosticScopeFactory ScopeFactory { get; set; } 
        
        public static OpenTelemetryCoreRecorder CreateRecorder(string operationName,
            string containerName,
            string databaseName,
            Documents.OperationType operationType,
            RequestOptions requestOptions, 
            CosmosClientContext clientContext)
        {
            if (clientContext is { ClientOptions.IsDistributedTracingEnabled: true })
            {
                OpenTelemetryRecorderFactory.ScopeFactory ??= new DiagnosticScopeFactory(clientNamespace: OpenTelemetryAttributeKeys.DiagnosticNamespace,
                        resourceProviderNamespace: OpenTelemetryAttributeKeys.ResourceProviderNamespace,
                        isActivityEnabled: true);
                
                // If there is no source then it will return default otherwise a valid diagnostic scope
                DiagnosticScope scope = OpenTelemetryRecorderFactory
                    .ScopeFactory
                    .CreateScope(name: $"{OpenTelemetryAttributeKeys.OperationPrefix}.{operationName}",
                                 kind: clientContext.ClientOptions.ConnectionMode == ConnectionMode.Gateway ? DiagnosticScope.ActivityKind.Internal : DiagnosticScope.ActivityKind.Client);

                // Record values only when we have a valid Diagnostic Scope
                if (scope.IsEnabled)
                {
                    return new OpenTelemetryCoreRecorder(
                        scope: scope,
                        operationName: operationName,
                        containerName: containerName,
                        databaseName: databaseName,
                        operationType: operationType,
                        clientContext: clientContext,
                        config: requestOptions?.DistributedTracingOptions ?? clientContext.ClientOptions?.DistributedTracingOptions);
                }
            }

            return default;
        }
    }
}
