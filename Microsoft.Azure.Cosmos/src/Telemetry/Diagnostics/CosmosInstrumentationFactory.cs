// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using global::Azure.Core.Pipeline;

    internal static class CosmosInstrumentationFactory
    {
        public static DiagnosticScopeFactory ScopeFactory { get; } = new DiagnosticScopeFactory(
                                                                                clientNamespace: CosmosInstrumentationConstants.DiagnosticNamespace, 
                                                                                resourceProviderNamespace: CosmosInstrumentationConstants.ResourceProviderNamespace,
                                                                                isActivityEnabled: true);

        public static ICosmosInstrumentation Get(string operationName)
        {
            DiagnosticScope scope = CosmosInstrumentationFactory
                .ScopeFactory
                .CreateScope($"{CosmosInstrumentationConstants.OperationPrefix}.{operationName}");

            if (scope.IsEnabled)
            {
                return new CosmosInstrumentation(scope);
            }

            return new CosmosInstrumentationNoOp();
        }
    }
}
