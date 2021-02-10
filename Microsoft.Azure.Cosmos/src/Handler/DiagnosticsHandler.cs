//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// Handler which add process level info like CPU usage to the
    /// diagnostics. This is a best effort scenario. It will not
    /// add or attempt to add it if an exception occurs to avoid
    /// impacting users.
    /// </summary>
    internal class DiagnosticsHandler : RequestHandler
    {
        public override Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            DiagnosticsHandlerHelper.Instance.RecordCpuDiagnostics(request);
            return base.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// This is a helper class that creates a single static instance to avoid each
        /// client instance from creating a new CPU monitor.
        /// </summary>
        private class DiagnosticsHandlerHelper
        {
            public static readonly DiagnosticsHandlerHelper Instance = new DiagnosticsHandlerHelper();
            private readonly CpuMonitor cpuMonitor = null;
            private bool isCpuMonitorEnabled = false;

            private DiagnosticsHandlerHelper()
            {
                // If the CPU monitor fails for some reason don't block the application
                try
                {
                    this.cpuMonitor = new CpuMonitor();
                    this.cpuMonitor.Start();
                    this.isCpuMonitorEnabled = true;
                }
                catch (Exception)
                {
                    this.isCpuMonitorEnabled = false;
                }
            }

            /// <summary>
            /// The diagnostics should never block a request, and is a best attempt
            /// If the CPU load history fails then don't try it in the future.
            /// </summary>
            public void RecordCpuDiagnostics(RequestMessage request)
            {
                if (this.isCpuMonitorEnabled)
                {
                    try
                    {
                        CpuLoadHistory cpuHistory = this.cpuMonitor.GetCpuLoad();
                        if (cpuHistory != null)
                        {
                            request.Trace.AddDatum(
                                "CPU Load History",
                                new CpuHistoryTraceDatum(cpuHistory));
                        }
                    }
                    catch (Exception)
                    {
                        this.isCpuMonitorEnabled = false;
                    }
                }
            }
        }
    }
}