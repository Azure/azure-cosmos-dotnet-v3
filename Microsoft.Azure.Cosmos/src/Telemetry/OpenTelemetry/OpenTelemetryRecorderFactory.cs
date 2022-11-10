// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using global::Azure.Core.Pipeline;

    internal static class OpenTelemetryRecorderFactory
    {
        private static DiagnosticScopeFactory ScopeFactory { get; set; } 

        public static OpenTelemetryCoreRecorder CreateRecorder(string operationName,
            string containerName,
            string databaseName,
            Documents.OperationType operationType,
            RequestOptions requestOptions, 
            CosmosClientContext clientContext)
        {
            if (clientContext is { ClientOptions.EnableDistributedTracing: true })
            {
                ScopeFactory = new DiagnosticScopeFactory(clientNamespace: OpenTelemetryAttributeKeys.DiagnosticNamespace,
                    resourceProviderNamespace: OpenTelemetryAttributeKeys.ResourceProviderNamespace,
                    isActivityEnabled: true);
                DiagnosticScope scope = OpenTelemetryRecorderFactory
                    .ScopeFactory
                    .CreateScope($"{OpenTelemetryAttributeKeys.OperationPrefix}.{operationName}");

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
