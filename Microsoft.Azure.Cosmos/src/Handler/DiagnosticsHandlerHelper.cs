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

        private bool isDiagnosticsMonitoringEnabled = false;
        private bool isTelemetryMonitoringEnabled = false;

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

        /// <summary>
        /// Start System Usage Monitor with Diagnostic and Telemetry Recorder if Telemetry is enabled 
        /// Otherwise Start System Usage Monitor with only Diagnostic Recorder
        /// </summary>
        private DiagnosticsHandlerHelper()
        {
            Console.WriteLine("DiagnosticsHandlerHelper => initialized" );
            this.isDiagnosticsMonitoringEnabled = false;

            // If the CPU monitor fails for some reason don't block the application
            try
            {
                this.isTelemetryMonitoringEnabled = ClientTelemetryOptions.IsClientTelemetryEnabled();

                Console.WriteLine("DiagnosticsHandlerHelper => isTelemetryMonitoringEnabled " + this.isTelemetryMonitoringEnabled);
                List<SystemUsageRecorder> recorders = new List<SystemUsageRecorder>()
                {
                    this.diagnosticSystemUsageRecorder,
                };

               /* if (this.isTelemetryMonitoringEnabled)
                {*/
                recorders.Add(this.telemetrySystemUsageRecorder);
              //  }

                SystemUsageMonitor.CreateAndStart(recorders);

                Console.WriteLine("DiagnosticsHandlerHelper => monitor started with recorder count " + recorders.Count);

                this.isDiagnosticsMonitoringEnabled = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("DiagnosticsHandlerHelper =>" + ex.Message);
                DefaultTrace.TraceError(ex.Message);

                this.isDiagnosticsMonitoringEnabled = false;
                this.isTelemetryMonitoringEnabled = false;
            }
        }

        /// <summary>
        /// This method will give CPU Usage(%), Memory Usage(kb) and ThreadPool Information from Diagnostic recorder, 
        /// It will return null if Diagnostic Monitoring is not enabled or throws any error while reading data from the recorder.
        /// </summary>
        public SystemUsageHistory GetDiagnosticsSystemHistory()
        {
            Console.WriteLine("GetDiagnosticsSystemHistory => " + this.isTelemetryMonitoringEnabled);
            if (!this.isDiagnosticsMonitoringEnabled)
            {
                return null;
            }

            try
            {
                return this.diagnosticSystemUsageRecorder.Data;
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetDiagnosticsSystemHistory =>" + ex.Message);
                DefaultTrace.TraceError(ex.Message);
                this.isDiagnosticsMonitoringEnabled = false;
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
            Console.WriteLine("GetClientTelemetrySystemHistory => " + this.isTelemetryMonitoringEnabled);
            if (!this.isTelemetryMonitoringEnabled)
            {
                return null;
            }

            try
            {
                Console.WriteLine("GetClientTelemetrySystemHistory => is enabled");
                return this.telemetrySystemUsageRecorder.Data;
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetClientTelemetrySystemHistory =>" + ex.Message);

                DefaultTrace.TraceError(ex.Message);
                this.isTelemetryMonitoringEnabled = false;
                return null;
            }
        }
    }
}
