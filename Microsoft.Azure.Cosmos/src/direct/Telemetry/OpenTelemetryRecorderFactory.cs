// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
#if NETSTANDARD2_0_OR_GREATER
namespace Microsoft.Azure.Documents.Telemetry
{
    using System;
    using System.Diagnostics;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// This class is used to generate Activities with Azure.Cosmos.Operation Source Name
    /// </summary>
    internal static class OpenTelemetryRecorderFactory
    {
        /// <summary>
        /// Singleton to make sure we only have one instance of the DiagnosticScopeFactory and pattern matching of listener happens only once
        /// </summary>
        private static readonly Lazy<DiagnosticScopeFactory> LazyScopeFactory = new Lazy<DiagnosticScopeFactory>(
            valueFactory: () => new DiagnosticScopeFactory(
                           clientNamespace: $"{DistributedTracingOptions.DiagnosticNamespace}.{DistributedTracingOptions.NetworkLevelPrefix}",
                           resourceProviderNamespace: DistributedTracingOptions.ResourceProviderNamespace,
                           isActivityEnabled: true,
                           suppressNestedClientActivities: false),
           isThreadSafe: true);

        public static OpenTelemetryRecorder CreateRecorder(DistributedTracingOptions options, 
            DocumentServiceRequest request)
        {
            OpenTelemetryRecorder openTelemetryRecorder = default;

            if (options?.IsDistributedTracingEnabled ?? false)
            {
                try
                {
                    string operationType = request.OperationType.ToOperationTypeString();

                    DiagnosticScope scope = LazyScopeFactory.Value.CreateScope(
                        name: "RequestAsync", 
                        kind: DiagnosticScope.ActivityKind.Client);

                    scope.SetDisplayName(operationType + " " + request.ResourceType.ToResourceTypeString());

                    // Record values only when we have a valid Diagnostic Scope
                    if (scope.IsEnabled)
                    {
                        openTelemetryRecorder = new OpenTelemetryRecorder(scope: scope, 
                            request: request, 
                            options: options);
                    }
                    options.Propagator?.Inject(Activity.Current, request.Headers);
                }
                catch(Exception ex)
                {
                    DefaultTrace.TraceWarning("Error with distributed tracing {0}", ex.ToString());
                }
            }

            return openTelemetryRecorder;
        }
    }
}
#endif