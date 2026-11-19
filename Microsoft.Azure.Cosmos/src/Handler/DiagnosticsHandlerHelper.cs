//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handler
{
    using System;
    using System.Collections.Generic;
    using Documents.Rntbd;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// This is a helper class that creates a single static instance to avoid each
    /// client instance from creating a new System Usage monitor for diagnostics.
    /// The diagnostics should never block a request, and is a best attempt
    /// If the CPU load history fails then don't try it in the future.
    /// </summary>
    internal class DiagnosticsHandlerHelper
    {
        private const string Diagnostickey = "diagnostic";

        public static readonly TimeSpan DiagnosticsRefreshInterval = TimeSpan.FromSeconds(10);

        // Need to reset it in Tests hence kept it non-readonly.
        private static SystemUsageRecorder DiagnosticSystemUsageRecorder = new SystemUsageRecorder(
            identifier: Diagnostickey,
            historyLength: 6,
            refreshInterval: DiagnosticsHandlerHelper.DiagnosticsRefreshInterval);

        /// <summary>
        /// Singleton to make sure only one instance of DiagnosticHandlerHelper is there.
        /// The system usage collection is disabled for internal builds so it is set to null to avoid
        /// compute for accidentally creating an instance or trying to use it.
        /// </summary>
        private static DiagnosticsHandlerHelper Instance =
#if INTERNAL
            null; 
#else
            new DiagnosticsHandlerHelper();
#endif

        private static bool isDiagnosticsMonitoringEnabled;

        private readonly SystemUsageMonitor systemUsageMonitor = null;

        public static DiagnosticsHandlerHelper GetInstance()
        {
            return DiagnosticsHandlerHelper.Instance;
        }

        /// <summary>
        /// Returns true when <see cref="DefaultTrace"/> is configured to emit traces for the
        /// supplied <paramref name="traceLevel"/>. Call sites use this to gate trace calls and
        /// avoid eagerly serializing diagnostics when the trace sink is a no-op.
        /// </summary>
        public static bool ShouldTrace(System.Diagnostics.TraceEventType traceLevel)
        {
            return DefaultTrace.TraceSource.Switch.ShouldTrace(traceLevel);
        }

        private void StopSystemMonitor()
        {
            try
            {
                this.systemUsageMonitor?.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                DefaultTrace.TraceError("Error while stopping system usage monitor. {0} ", ex.Message);
            }
        }

        /// <summary>
        /// Start System Usage Monitor with the Diagnostic Recorder.
        /// </summary>
        private DiagnosticsHandlerHelper()
        {
            DiagnosticsHandlerHelper.isDiagnosticsMonitoringEnabled = false;

            // If the CPU monitor fails for some reason don't block the application
            try
            {
                List<SystemUsageRecorder> recorders = new List<SystemUsageRecorder>()
                {
                    DiagnosticsHandlerHelper.DiagnosticSystemUsageRecorder,
                };

                this.systemUsageMonitor = SystemUsageMonitor.CreateAndStart(recorders);

                DiagnosticsHandlerHelper.isDiagnosticsMonitoringEnabled = true;
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError(ex.Message);

                DiagnosticsHandlerHelper.isDiagnosticsMonitoringEnabled = false;
            }
        }

        /// <summary>
        /// This method will give CPU Usage(%), Memory Usage(kb) and ThreadPool Information from Diagnostic recorder, 
        /// It will return null if Diagnostic Monitoring is not enabled or throws any error while reading data from the recorder.
        /// </summary>
        public SystemUsageHistory GetDiagnosticsSystemHistory()
        {
            if (!DiagnosticsHandlerHelper.isDiagnosticsMonitoringEnabled)
            {
                return null;
            }

            try
            {
                return DiagnosticsHandlerHelper.DiagnosticSystemUsageRecorder.Data;
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError(ex.Message);
                DiagnosticsHandlerHelper.isDiagnosticsMonitoringEnabled = false;
                return null;
            }
        }

    }
}
