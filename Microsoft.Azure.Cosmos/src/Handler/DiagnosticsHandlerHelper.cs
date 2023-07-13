//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handler
{
    using System;
    using System.Collections.Generic;
    using Documents.Rntbd;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Telemetry;

    /// <summary>
    /// This is a helper class that creates a single static instance to avoid each
    /// client instance from creating a new System Usage monitor with Diagnostics and Telemetry Recorders(if enabled).
    /// The diagnostics should never block a request, and is a best attempt
    /// If the CPU load history fails then don't try it in the future.
    /// </summary>
    internal class DiagnosticsHandlerHelper
    {
        private const string Diagnostickey = "diagnostic";
        private const string Telemetrykey = "telemetry";

        public static readonly TimeSpan DiagnosticsRefreshInterval = TimeSpan.FromSeconds(10);
        private readonly SystemUsageRecorder diagnosticSystemUsageRecorder = new SystemUsageRecorder(
            identifier: Diagnostickey,
            historyLength: 6,
            refreshInterval: DiagnosticsHandlerHelper.DiagnosticsRefreshInterval);

        private static readonly TimeSpan ClientTelemetryRefreshInterval = TimeSpan.FromSeconds(5);
        private readonly SystemUsageRecorder telemetrySystemUsageRecorder = new SystemUsageRecorder(
            identifier: Telemetrykey,
            historyLength: 120,
            refreshInterval: DiagnosticsHandlerHelper.ClientTelemetryRefreshInterval);

        private static bool isDiagnosticsMonitoringEnabled = false;
        private static bool isTelemetryMonitoringEnabled = false;

        /// <summary>
        /// Singleton to make sure only one instance of DiagnosticHandlerHelper is there.
        /// The system usage collection is disabled for internal builds so it is set to null to avoid
        /// compute for accidentally creating an instance or trying to use it.
        /// </summary>
        public static readonly DiagnosticsHandlerHelper Instance =
#if INTERNAL
            null; 
#else
            new DiagnosticsHandlerHelper();
#endif

        private readonly SystemUsageMonitor systemUsageMonitor = null;

        /// <summary>
        /// Start System Usage Monitor with Diagnostic and Telemetry Recorder if Telemetry is enabled 
        /// Otherwise Start System Usage Monitor with only Diagnostic Recorder
        /// </summary>
        private DiagnosticsHandlerHelper()
        {
            DiagnosticsHandlerHelper.isDiagnosticsMonitoringEnabled = false;
            DiagnosticsHandlerHelper.isTelemetryMonitoringEnabled = false;
            
            // If the CPU monitor fails for some reason don't block the application
            try
            {
                List<SystemUsageRecorder> recorders = new List<SystemUsageRecorder>()
                {
                    this.diagnosticSystemUsageRecorder,
                    this.telemetrySystemUsageRecorder
                };

                this.systemUsageMonitor = SystemUsageMonitor.CreateAndStart(recorders);

                DiagnosticsHandlerHelper.isDiagnosticsMonitoringEnabled = true;
                DiagnosticsHandlerHelper.isTelemetryMonitoringEnabled = true;
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError(ex.Message);

                DiagnosticsHandlerHelper.isDiagnosticsMonitoringEnabled = false;
                DiagnosticsHandlerHelper.isTelemetryMonitoringEnabled = false;
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
                return this.diagnosticSystemUsageRecorder.Data;
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError(ex.Message);
                DiagnosticsHandlerHelper.isDiagnosticsMonitoringEnabled = false;
                return null;
            }
        }

        /// <summary>
        /// This method will give CPU Usage(%), Memory Usage(kb) and ThreadPool Information from Client Telemetry recorder.
        /// It will return null if Diagnostic Monitoring is not enabled or throws any error while reading data from the recorder.
        /// </summary>
        /// <returns> CpuAndMemoryUsageRecorder</returns>
        public SystemUsageHistory GetClientTelemetrySystemHistory()
        {
            if (!DiagnosticsHandlerHelper.isTelemetryMonitoringEnabled)
            {
                return null;
            }

            try
            {
                return this.telemetrySystemUsageRecorder.Data;
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError(ex.Message);
                DiagnosticsHandlerHelper.isTelemetryMonitoringEnabled = false;
                return null;
            }
        }
    }
}
