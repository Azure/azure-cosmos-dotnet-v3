//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Documents.Rntbd;

    internal static class ClientTelemetryHelper
    {
        /// <summary>
        /// Task to get Account Properties from cache if available otherwise make a network call.
        /// </summary>
        /// <returns>Async Task</returns>
        internal static async Task<AccountProperties> SetAccountNameAsync(GlobalEndpointManager globalEndpointManager)
        {
            DefaultTrace.TraceVerbose("Getting Account Information for Telemetry.");
            try
            {
                if (globalEndpointManager != null)
                {
                    return await globalEndpointManager.GetDatabaseAccountAsync();
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception while getting account information in client telemetry : {0}", ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Record System Usage and update passed system Info collection. Right now, it collects following metrics
        /// 1) CPU Usage
        /// 2) Memory Remaining
        /// 3) Available Threads
        /// 
        /// </summary>
        /// <param name="systemUsageHistory"></param>
        /// <param name="isDirectConnectionMode"></param>
        private static List<SystemInfo> RecordSystemUsage(
                SystemUsageHistory systemUsageHistory, 
                bool isDirectConnectionMode)
        {
            if (systemUsageHistory.Values == null)
            {
                return null;
            }

            DefaultTrace.TraceVerbose("System Usage recorded by telemetry is : {0}", systemUsageHistory);
            
            List<SystemInfo> systemInfoCollection = new List<SystemInfo>(6)
            {
                TelemetrySystemUsage.GetCpuInfo(systemUsageHistory.Values),
                TelemetrySystemUsage.GetMemoryRemainingInfo(systemUsageHistory.Values),
                TelemetrySystemUsage.GetAvailableThreadsInfo(systemUsageHistory.Values),
                TelemetrySystemUsage.GetThreadWaitIntervalInMs(systemUsageHistory.Values),
                TelemetrySystemUsage.GetThreadStarvationSignalCount(systemUsageHistory.Values)
            }; // Reset System Information

            if (isDirectConnectionMode)
            {
                systemInfoCollection.Add(TelemetrySystemUsage.GetTcpConnectionCount(systemUsageHistory.Values));
            }

            return systemInfoCollection;
        }
        
        /// <summary>
        /// Record CPU and memory usage which will be sent as part of telemetry information
        /// </summary>
        internal static List<SystemInfo> RecordSystemUtilization(DiagnosticsHandlerHelper helper, bool isDirectMode)
        {
            try
            {
                DefaultTrace.TraceVerbose("Started Recording System Usage for telemetry.");

                SystemUsageHistory systemUsageHistory = helper.GetClientTelemetrySystemHistory();

                if (systemUsageHistory != null)
                {
                    return ClientTelemetryHelper.RecordSystemUsage(
                        systemUsageHistory: systemUsageHistory,
                        isDirectConnectionMode: isDirectMode);
                }
                else
                {
                    DefaultTrace.TraceWarning("System Usage History not available");
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("System Usage Recording Error : {0} ", ex);
            }
            return null;
        }

        /// <summary>
        /// Get comma separated list of regions contacted from the diagnostic
        /// </summary>
        /// <returns>Comma separated region list</returns>
        internal static string GetContactedRegions(IReadOnlyCollection<(string regionName, Uri uri)> regionList)
        {
            if (regionList == null || regionList.Count == 0)
            {
                return null;
            }

            if (regionList.Count == 1)
            {
                return regionList.ElementAt(0).regionName;
            }
            
            StringBuilder regionsContacted = new StringBuilder();
            foreach ((string name, _) in regionList)
            {
                if (regionsContacted.Length > 0)
                {
                    regionsContacted.Append(",");

                }

                regionsContacted.Append(name);
            }

            return regionsContacted.ToString();
        }

    }
}
