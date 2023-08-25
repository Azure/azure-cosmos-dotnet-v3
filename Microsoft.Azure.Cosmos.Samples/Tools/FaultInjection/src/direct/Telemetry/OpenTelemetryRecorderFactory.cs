// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
#if NETSTANDARD2_0_OR_GREATER
namespace Microsoft.Azure.Documents.Telemetry
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using static Microsoft.Azure.Documents.RntbdConstants;

    /// <summary>
    /// This class is used to generate Activities with Azure.Cosmos.Operation Source Name
    /// </summary>
    internal static class OpenTelemetryRecorderFactory
    {
        
        private const string NetworkLevelPrefix = "Request";
        private const string DiagnosticNamespace = "Azure.Cosmos";
        private const string ResourceProviderNamespace = "Microsoft.DocumentDB";
        private const string traceParent = "traceparent";
        /// <summary>
        /// Singleton to make sure we only have one instance of the DiagnosticScopeFactory and pattern matching of listener happens only once
        /// </summary>
        private static readonly Lazy<DiagnosticScopeFactory> LazyScopeFactory = new Lazy<DiagnosticScopeFactory>(
            valueFactory: () => new DiagnosticScopeFactory(
                           clientNamespace: DiagnosticNamespace,
                           resourceProviderNamespace: ResourceProviderNamespace,
                           isActivityEnabled: true,
                           suppressNestedClientActivities: false),
           isThreadSafe: true);
        public static OpenTelemetryRecorder CreateRecorder(bool IsDistributedTracingEnabled, DocumentServiceRequest request)
        {
            if (IsDistributedTracingEnabled)
            {
                try
                {
                    DiagnosticScope scope = LazyScopeFactory.Value.CreateScope(name: $"{NetworkLevelPrefix}.{request.OperationType}", kind: DiagnosticScope.ActivityKind.Client);

                    // Record values only when we have a valid Diagnostic Scope
                    if (scope.IsEnabled)
                    {
                        OpenTelemetryRecorder openTelemetryRecorder = new OpenTelemetryRecorder(scope: scope);
                        request.Headers.Set(traceParent, Activity.Current?.Id);
                        return openTelemetryRecorder;
                    }
                    request.Headers.Set(traceParent, Activity.Current?.Id);
                }
                catch(Exception ex)
                {
                    DefaultTrace.TraceWarning("Error with distributed tracing {0}", ex.ToString());
                }
            }
            return default;
        }
    }
}
#endif