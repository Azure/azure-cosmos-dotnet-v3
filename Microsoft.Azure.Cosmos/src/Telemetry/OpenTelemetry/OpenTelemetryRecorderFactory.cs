// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using global::Azure.Core.Pipeline;

    internal static class OpenTelemetryRecorderFactory
    {
        private static DiagnosticScopeFactory ScopeFactory { get; set; } 

        public static OpenTelemetryCoreRecorder CreateRecorder(string operationName, CosmosClientContext clientContext, bool isFeatureEnabled)
        {
            if (isFeatureEnabled)
            {
                ScopeFactory = new DiagnosticScopeFactory(clientNamespace: OpenTelemetryAttributeKeys.DiagnosticNamespace,
                                                    resourceProviderNamespace: OpenTelemetryAttributeKeys.ResourceProviderNamespace,
                                                    isActivityEnabled: true);
                DiagnosticScope scope = OpenTelemetryRecorderFactory
                    .ScopeFactory
                    .CreateScope($"{OpenTelemetryAttributeKeys.OperationPrefix}.{operationName}");

                if (scope.IsEnabled)
                {
                    scope.AddAttribute(OpenTelemetryAttributeKeys.DbSystemName, "cosmosdb");

                    return new OpenTelemetryCoreRecorder(scope, operationName, clientContext);
                }
            }

            return default;
        }
    }
}
