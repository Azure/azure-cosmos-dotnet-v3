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
    /// client instance from creating a new CPU monitor.
    /// </summary>
    internal class DiagnosticsHandlerHelper
    {
        public static readonly TimeSpan DiagnosticsRefreshInterval = TimeSpan.FromSeconds(10);
        private readonly SystemUsageRecorder diagnosticSystemUsageRecorder = new SystemUsageRecorder(
            identifier: Diagnostickey,
            historyLength: 6,
            refreshInterval: DiagnosticsHandlerHelper.DiagnosticsRefreshInterval);

        private readonly SystemUsageRecorder telemetrySystemUsageRecorder = new SystemUsageRecorder(
            identifier: Diagnostickey,
            historyLength: 120,
            refreshInterval: TimeSpan.FromSeconds(5));

        internal const string Diagnostickey = "diagnostic";
        internal const string Telemetrykey = "telemetry";

        private bool isMonitoringEnabled = false;

        /// <summary>
        /// Singleton to make sure only one instance of DiagnosticHandlerHelper is there
        /// </summary>
        public static readonly DiagnosticsHandlerHelper Instance = new DiagnosticsHandlerHelper();

        private DiagnosticsHandlerHelper()
        {
            // Disable system usage for internal builds. Cosmos DB owns the VMs and already logs
            // the system information so no need to track it.
#if INTERNAL
            this.isMonitoringEnabled = false;
#else
            // If the CPU monitor fails for some reason don't block the application
            try
            {
                SystemUsageMonitor systemUsageMonitor = SystemUsageMonitor.CreateAndStart(
                    new List<SystemUsageRecorder>
                    {
                        this.diagnosticSystemUsageRecorder,
                        this.telemetrySystemUsageRecorder,
                    });

                this.isMonitoringEnabled = true;
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError(ex.Message);
                this.isMonitoringEnabled = false;
            }
#endif
        }

        /// <summary>
        /// The diagnostics should never block a request, and is a best attempt
        /// If the CPU load history fails then don't try it in the future.
        /// </summary>
        public SystemUsageHistory GetDiagnosticsSystemHistory()
        {
            if (!this.isMonitoringEnabled)
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
                this.isMonitoringEnabled = false;
                return null;
            }
        }

        /// <summary>
        /// This method will give CPU Usage(%) and Memory Usage(kb) for a given recorder, 
        /// Right now only 2 recorders are available : Diagnostic and Telemetry
        /// </summary>
        /// <returns> CpuAndMemoryUsageRecorder</returns>
        public SystemUsageHistory GetClientTelemtrySystemHistory()
        {
            if (!this.isMonitoringEnabled)
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
                this.isMonitoringEnabled = false;
                return null;
            }
        }
    }
}
