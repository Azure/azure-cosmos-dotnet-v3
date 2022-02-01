//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using global::Azure.Core.Pipeline;

    internal class CosmosDbInstrumentation
    {
        public const string DiagnosticNamespace = "Azure.Cosmos";

        public const string ResourceProviderNamespace = "Azure.Cosmos";

        public static DiagnosticScopeFactory ScopeFactory { get; } = new DiagnosticScopeFactory(DiagnosticNamespace, ResourceProviderNamespace, true);
    }
}
