// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using global::Azure.Core.Pipeline;

    internal static class CosmosInstrumentationFactory
    {
#if PREVIEW
        public static DiagnosticScopeFactory ScopeFactory { get; } = new DiagnosticScopeFactory(
                                                                                clientNamespace: OTelAttributes.DiagnosticNamespace, 
                                                                                resourceProviderNamespace: OTelAttributes.ResourceProviderNamespace,
                                                                                isActivityEnabled: true);
#endif
        public static ICosmosInstrumentation Get(string operationName)
        {
#if PREVIEW
            DiagnosticScope scope = CosmosInstrumentationFactory
                .ScopeFactory
                .CreateScope($"{OTelAttributes.OperationPrefix}.{operationName}");

            if (scope.IsEnabled)
            {
                scope.AddAttribute(OTelAttributes.DbSystemName, "cosmosdb");

                return new CosmosInstrumentation(scope);
            }
#endif

            return new CosmosInstrumentationNoOp();
        }
    }
}
