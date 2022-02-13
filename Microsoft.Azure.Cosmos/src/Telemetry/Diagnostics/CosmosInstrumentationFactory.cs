// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using Diagnostics;
    using global::Azure.Core.Pipeline;

    internal static class CosmosInstrumentationFactory
    {
        public const string DiagnosticNamespace = "Azure.Cosmos";
        public const string ResourceProviderNamespace = "Microsoft.Azure.Cosmos";
        public const string OperationPrefix = "Cosmos";

        public static DiagnosticScopeFactory ScopeFactory { get; } = new DiagnosticScopeFactory(DiagnosticNamespace, ResourceProviderNamespace, true);

        public static ICosmosInstrumentation Get(string operationName)
        {
            DiagnosticScope scope = CosmosInstrumentationFactory
                .ScopeFactory
                .CreateScope($"{OperationPrefix}.{operationName}");

            if (scope.IsEnabled)
            {
                return new CosmosInstrumentation(scope);
            }

            return new CosmosInstrumentationNoOp();
        }
    }
}
