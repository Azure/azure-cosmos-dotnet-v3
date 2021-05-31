//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handler
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Documents.Rntbd;
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
        internal const string Telemetrykey = "telemetry";

        private bool isMonitoringEnabled = false;

        /// <summary>
        /// Singleton to make sureonly one intsane of DiadnosticHandlerHelper is there
        /// </summary>
        public static DiagnosticsHandlerHelper Instance()
        {
            if (helper == null)
            {
                helper = new DiagnosticsHandlerHelper();
            }
            return helper;
        }

        private DiagnosticsHandlerHelper()
        {
            // If the CPU monitor fails for some reason don't block the application
            try
            {
                this.systemUsageMonitor = SystemUsageMonitorBase.Create(
                    new List<CpuAndMemoryUsageRecorder> 
                    {
                        new CpuAndMemoryUsageRecorder(Diagnostickey, 6, TimeSpan.FromSeconds(10)),
                        new CpuAndMemoryUsageRecorder(Telemetrykey, 120, TimeSpan.FromSeconds(5))
                    });

                if (!(this.systemUsageMonitor is SystemUsageMonitorNoOps))
                {
                    this.systemUsageMonitor.Start();
                    this.isMonitoringEnabled = true;
                }
               
            }
            catch (Exception)
            {
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
                catch (Exception)
                {
                    this.isMonitoringEnabled = false;
                }
            }
        }

        /// <summary>
        /// This method will give CPU Usage(%) and Memory Usage(kb) for a given recorder, 
        /// Right now only 2 recorders are available : Diagnostic and Telemetry
        /// </summary>
        /// <param name="recorderKey"></param>
        /// <returns>CpuLoadHistory and MemoryLoadHistory</returns>
        public Tuple<CpuLoadHistory, MemoryLoadHistory> GetCpuAndMemoryUsage(string recorderKey)
        {
            CpuLoadHistory cpuHistory = null;
            MemoryLoadHistory memoryLoadHistory = null;
            if (this.isMonitoringEnabled)
            {
                try
                {
                    CpuAndMemoryUsageRecorder recorder = this.systemUsageMonitor.GetRecorder(recorderKey);
                    cpuHistory = recorder.CpuUsage;
                    memoryLoadHistory = recorder.MemoryUsage;
                }
                catch (Exception)
                {
                    this.isMonitoringEnabled = false;
                }
            }
            return new Tuple<CpuLoadHistory, MemoryLoadHistory>(cpuHistory, memoryLoadHistory);
        }
    }
}
