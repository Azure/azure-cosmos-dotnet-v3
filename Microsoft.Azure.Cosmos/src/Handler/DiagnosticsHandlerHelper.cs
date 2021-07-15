//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handler
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Documents.Rntbd;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Tracing.TraceData;

    /// <summary>
    /// This is a helper class that creates a single static instance to avoid each
    /// client instance from creating a new CPU monitor.
    /// </summary>
    internal class DiagnosticsHandlerHelper
    {
        private static DiagnosticsHandlerHelper helper;
        private readonly SystemUsageMonitorBase systemUsageMonitor = null;

        internal const string Diagnostickey = "diagnostic";
        private const int HistoryLengthForDiagnostics = 6;
        private readonly TimeSpan refreshIntervalForDiagnostics = TimeSpan.FromSeconds(10);

        internal const string Telemetrykey = "telemetry";
        private const int HistoryLengthForTelemetry = 120;
        private readonly TimeSpan refreshIntervalForTelemetry = TimeSpan.FromSeconds(5);

        private bool isMonitoringEnabled = false;

        private static readonly object staticLock = new object();

        /// <summary>
        /// Singleton to make sureonly one intsane of DiagnosticHandlerHelper is there
        /// </summary>
        public static DiagnosticsHandlerHelper Instance()
        {
            lock (staticLock)
            {
                if (helper != null)
                {
                    return helper;
                }
                return helper = new DiagnosticsHandlerHelper();
            }
        }

        private DiagnosticsHandlerHelper()
        {
            // If the CPU monitor fails for some reason don't block the application
            try
            {
                this.systemUsageMonitor = SystemUsageMonitorBase.Create(
                    new List<CpuAndMemoryUsageRecorder>
                    {
                        new CpuAndMemoryUsageRecorder(Diagnostickey, HistoryLengthForDiagnostics, this.refreshIntervalForDiagnostics),
                        new CpuAndMemoryUsageRecorder(Telemetrykey, HistoryLengthForTelemetry, this.refreshIntervalForTelemetry)
                    });

                if (this.systemUsageMonitor is SystemUsageMonitorNoOps)
                {
                    throw new Exception("Unsupported System Usage Monitor");
                }

                this.systemUsageMonitor.Start();
                this.isMonitoringEnabled = true;
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError(ex.Message);
                this.isMonitoringEnabled = false;
            }
        }

        /// <summary>
        /// The diagnostics should never block a request, and is a best attempt
        /// If the CPU load history fails then don't try it in the future.
        /// </summary>
        public void RecordCpuDiagnostics(RequestMessage request, string recorderKey)
        {
            if (this.isMonitoringEnabled)
            {
                try
                {
                    CpuLoadHistory cpuHistory = this.systemUsageMonitor.GetRecorder(recorderKey).CpuUsage;
                    if (cpuHistory != null)
                    {
                        request.Trace.AddDatum(
                            "CPU Load History",
                            new CpuHistoryTraceDatum(cpuHistory));
                    }
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceError(ex.Message);
                    this.isMonitoringEnabled = false;
                }
            }
        }

        /// <summary>
        /// This method will give CPU Usage(%) and Memory Usage(kb) for a given recorder, 
        /// Right now only 2 recorders are available : Diagnostic and Telemetry
        /// </summary>
        /// <param name="recorderKey"></param>
        /// <returns> CpuAndMemoryUsageRecorder</returns>
        public CpuAndMemoryUsageRecorder GetUsageRecorder(string recorderKey)
        {
            if (this.isMonitoringEnabled)
            {
                try
                {
                    return this.systemUsageMonitor.GetRecorder(recorderKey);
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceError(ex.Message);
                    this.isMonitoringEnabled = false;
                }
            }

            return null;
        }
    }
}
