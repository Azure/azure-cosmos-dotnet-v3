//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;

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
        /// <summary>
        /// It is a feature flag to enable/disable distributed tracing feature completely at network level. 
        /// Even this flag is switched on, exporter needs to be subscribed for "Azure.Cosmos.Request" Source
        /// </summary>
        internal bool IsDistributedTracingEnabled { get; set; }

        /// <summary>
        /// This Predicate is called for each and every request with the <see cref="DocumentServiceRequest"/>
        /// object. 
        /// 
        /// This DistributedTracingOption can be used by the application to add Request level control on 
        /// enabling/disabling DT. An example use-case will be - If an Application does not want DT
        /// to be enabled for DATA operations, it can use this Predicate to enable DT only for Account 
        /// and Database resource operation and disable for Document resource operations.
        /// </summary>
        internal Func<DocumentServiceRequest, bool> RequestEnabledPredicate { get; set; } = (request) => true;

        internal CosmosDistributedContextPropagatorBase Propagator { get; set; } = new DefaultCosmosDistributedContextPropagator();
#endif
    }
}
