//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handler
{
    using System;
    using System.Collections.Generic;
    using Documents.Rntbd;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;

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
        private static readonly SystemUsageRecorder DiagnosticSystemUsageRecorder = new SystemUsageRecorder(
            identifier: Diagnostickey,
            historyLength: 6,
            refreshInterval: DiagnosticsHandlerHelper.DiagnosticsRefreshInterval);

        private static readonly TimeSpan ClientTelemetryRefreshInterval = TimeSpan.FromSeconds(5);
        private static readonly SystemUsageRecorder TelemetrySystemUsageRecorder = new SystemUsageRecorder(
            identifier: Telemetrykey,
            historyLength: 120,
            refreshInterval: DiagnosticsHandlerHelper.ClientTelemetryRefreshInterval);

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
        private static bool isTelemetryMonitoringEnabled;

        private readonly SystemUsageMonitor systemUsageMonitor = null;

        public static DiagnosticsHandlerHelper GetInstance()
        {
            return DiagnosticsHandlerHelper.Instance;
        }

        /// <summary>
        /// Restart the monitor with client telemetry recorder if telemetry is enabled
        /// </summary>
        /// <param name="isClientTelemetryEnabled"></param>
        public static void Refresh(bool isClientTelemetryEnabled)
        {
            if (isClientTelemetryEnabled != DiagnosticsHandlerHelper.isTelemetryMonitoringEnabled)
            {
                DiagnosticsHandlerHelper.Instance.StopSystemMonitor();

                // Update telemetry flag
                DiagnosticsHandlerHelper.isTelemetryMonitoringEnabled = isClientTelemetryEnabled;

                // Create new instance, it will start a new system monitor job
                DiagnosticsHandlerHelper.Instance = new DiagnosticsHandlerHelper();
            }
        }

        private void StopSystemMonitor()
        {
            try
            {
                this.systemUsageMonitor?.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                DefaultTrace.TraceError($"Error while stopping system usage monitor. Stacktrace: {0} ", ex.StackTrace);
            }
        }

        /// <summary>
        /// Start System Usage Monitor with Diagnostic and Telemetry Recorder if Telemetry is enabled 
        /// Otherwise Start System Usage Monitor with only Diagnostic Recorder
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

                if (DiagnosticsHandlerHelper.isTelemetryMonitoringEnabled)
                {
                    recorders.Add(DiagnosticsHandlerHelper.TelemetrySystemUsageRecorder);
                }

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
                return DiagnosticsHandlerHelper.TelemetrySystemUsageRecorder.Data;
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
