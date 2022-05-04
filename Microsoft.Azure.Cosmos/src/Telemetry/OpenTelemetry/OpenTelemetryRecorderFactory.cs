// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using global::Azure.Core.Pipeline;

    internal static class OpenTelemetryRecorderFactory
    {
//#if PREVIEW
        public static DiagnosticScopeFactory ScopeFactory { get; } = new DiagnosticScopeFactory(
                                                                                clientNamespace: OpenTelemetryAttributeKeys.DiagnosticNamespace, 
                                                                                resourceProviderNamespace: OpenTelemetryAttributeKeys.ResourceProviderNamespace,
                                                                                isActivityEnabled: true);
//#endif
        public static IOpenTelemetryRecorder CreateRecorder(string operationName)
        {
//#if PREVIEW
            DiagnosticScope scope = OpenTelemetryRecorderFactory
                .ScopeFactory
                .CreateScope($"{OpenTelemetryAttributeKeys.OperationPrefix}.{operationName}");

            if (scope.IsEnabled)
            {
                scope.AddAttribute(OpenTelemetryAttributeKeys.DbSystemName, "cosmosdb");

                return new OpenTelemetryDefaultRecorder(scope);
            }
//#endif

            return OpenTelemetryRecorderNoOp.Singleton;
        }
    }
}
