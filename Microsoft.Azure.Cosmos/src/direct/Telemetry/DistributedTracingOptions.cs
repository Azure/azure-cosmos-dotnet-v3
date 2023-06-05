//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Telemetry
{
    /// <summary>
    /// This class has all the configuration related to distributed tracing.
    /// Distributed Tracing flag will always false for, less than NETSTANDARD. Hnce not available to the client.
    /// </summary>
    internal class DistributedTracingOptions
    {
        public const string NetworkLevelPrefix = "Request";
        public const string DiagnosticNamespace = "Azure.Cosmos";

        public const string ResourceProviderNamespace = "Microsoft.DocumentDB";

#if NETSTANDARD2_0_OR_GREATER
        internal bool IsDistributedTracingEnabled { get; set; }

        internal CosmosDistributedContextPropagatorBase Propagator { get; set; } = new DefaultCosmosDistributedContextPropagator();
#endif
    }
}
