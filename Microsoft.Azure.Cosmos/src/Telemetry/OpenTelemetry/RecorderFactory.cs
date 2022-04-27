// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using global::Azure.Core.Pipeline;

    internal static class RecorderFactory
    {
#if PREVIEW
        public static DiagnosticScopeFactory ScopeFactory { get; } = new DiagnosticScopeFactory(
                                                                                clientNamespace: Attributes.DiagnosticNamespace, 
                                                                                resourceProviderNamespace: Attributes.ResourceProviderNamespace,
                                                                                isActivityEnabled: true);
#endif
        public static IRecorder Get(string operationName)
        {
#if PREVIEW
            DiagnosticScope scope = RecorderFactory
                .ScopeFactory
                .CreateScope($"{Attributes.OperationPrefix}.{operationName}");

            if (scope.IsEnabled)
            {
                scope.AddAttribute(Attributes.DbSystemName, "cosmosdb");

                return new DefaultRecorder(scope);
            }
#endif

            return new RecorderNoOp();
        }
    }
}
