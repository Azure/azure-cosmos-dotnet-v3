//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal static class ClientTelemetryOptions
    {
        // ConversionFactor used in Histogram calculation to maintain precision or to collect data in desired unit
        internal const int HistogramPrecisionFactor = 100;
        internal const double TicksToMsFactor = TimeSpan.TicksPerMillisecond;
        internal const int KbToMbFactor = 1024;

        internal const int OneKbToBytes = 1024;
            
        // Expecting histogram to have Minimum Latency of 1 and Maximum Latency of 1 hour (which is never going to happen)
        internal const long RequestLatencyMax = TimeSpan.TicksPerHour;
        internal const long RequestLatencyMin = 1;
        internal const int RequestLatencyPrecision = 4;
        internal const string RequestLatencyName = "RequestLatency";
        internal const string RequestLatencyUnit = "MilliSecond";

        // Expecting histogram to have Minimum Request Charge of 1 and Maximum Request Charge of 9999900
        // For all the Document ReadWriteQuery Operations there will be at least 1 request charge.
        internal const long RequestChargeMax = 9999900;
        internal const long RequestChargeMin = 1;
        internal const int RequestChargePrecision = 2;
        internal const string RequestChargeName = "RequestCharge";
        internal const string RequestChargeUnit = "RU";

        // Expecting histogram to have Minimum CPU Usage of .001% and Maximum CPU Usage of 999.99%
        internal const long CpuMax = 99999;
        internal const long CpuMin = 1;
        internal const int CpuPrecision = 2;
        internal const String CpuName = "CPU";
        internal const String CpuUnit = "Percentage";

        // Expecting histogram to have Minimum Memory Remaining of 1 MB and Maximum Memory Remaining of Long Max Value
        internal const long MemoryMax = Int64.MaxValue;
        internal const long MemoryMin = 1;
        internal const int MemoryPrecision = 2;
        internal const String MemoryName = "MemoryRemaining";
        internal const String MemoryUnit = "MB";

        // Expecting histogram to have Minimum Available Threads = 0 and Maximum Available Threads = it can be any anything depends on the machine
        internal const long AvailableThreadsMax = Int64.MaxValue;
        internal const long AvailableThreadsMin = 1;
        internal const int AvailableThreadsPrecision = 2;
        internal const String AvailableThreadsName = "SystemPool_AvailableThreads";
        internal const String AvailableThreadsUnit = "ThreadCount";

        // Expecting histogram to have Minimum ThreadWaitIntervalInMs of 1 and Maximum ThreadWaitIntervalInMs of 1 second
        internal const long ThreadWaitIntervalInMsMax = TimeSpan.TicksPerSecond;
        internal const long ThreadWaitIntervalInMsMin = 1;
        internal const int ThreadWaitIntervalInMsPrecision = 2;
        internal const string ThreadWaitIntervalInMsName = "SystemPool_ThreadWaitInterval";
        internal const string ThreadWaitIntervalInMsUnit = "MilliSecond";

        // Expecting histogram to have Minimum Number of TCP connections as 1 and Maximum Number Of TCP connection as 70000
        internal const long NumberOfTcpConnectionMax = 70000;
        internal const long NumberOfTcpConnectionMin = 1;
        internal const int NumberOfTcpConnectionPrecision = 2;
        internal const string NumberOfTcpConnectionName = "RntbdOpenConnections";
        internal const string NumberOfTcpConnectionUnit = "Count";

        internal const string IsThreadStarvingName = "SystemPool_IsThreadStarving_True";
        internal const string IsThreadStarvingUnit = "Count";

        internal const double DefaultTimeStampInSeconds = 600;
        internal const double Percentile50 = 50.0;
        internal const double Percentile90 = 90.0;
        internal const double Percentile95 = 95.0;
        internal const double Percentile99 = 99.0;
        internal const double Percentile999 = 99.9;
        internal const string DateFormat = "yyyy-MM-ddTHH:mm:ssZ";
        
        internal const string EnvPropsClientTelemetrySchedulingInSeconds = "COSMOS.CLIENT_TELEMETRY_SCHEDULING_IN_SECONDS";
        internal const string EnvPropsClientTelemetryEnabled = "COSMOS.CLIENT_TELEMETRY_ENABLED";
        internal const string EnvPropsClientTelemetryVmMetadataUrl = "COSMOS.VM_METADATA_URL";
        internal const string EnvPropsClientTelemetryEndpoint = "COSMOS.CLIENT_TELEMETRY_ENDPOINT";
        internal const string EnvPropsClientTelemetryEnvironmentName = "COSMOS.ENVIRONMENT_NAME";
        
        internal static readonly ResourceType AllowedResourceTypes = ResourceType.Document;
        // Why 5 sec? As of now, if any network request is taking more than 5 millisecond sec, we will consider it slow request this value can be revisited in future
        private static readonly TimeSpan NetworkLatencyThreshold = TimeSpan.FromMilliseconds(5);
        internal static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings 
        { 
            NullValueHandling = NullValueHandling.Ignore,
            MaxDepth = 64, // https://github.com/advisories/GHSA-5crp-9r3c-p9vr
        };
        
        private static readonly List<int> ExcludedStatusCodes = new List<int> { 404, 409 };
        internal static readonly HashSet<string> PropertiesContainMetrics = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "OperationInfo", "CacheRefreshInfo" };

        internal static int PayloadSizeThreshold = 1024 * 1024 * 2; // 2MB
        internal static Dictionary<string, int> PropertiesWithPageSize = new Dictionary<string, int>
        {
            { "OperationInfo", 1000 },
            { "CacheRefreshInfo", 2000 }
        };
        
        private static Uri clientTelemetryEndpoint;
        private static string environmentName;
        private static TimeSpan scheduledTimeSpan = TimeSpan.Zero;
        
        internal static bool IsClientTelemetryEnabled()
        {
            bool isTelemetryEnabled = ConfigurationManager
                .GetEnvironmentVariable<bool>(ClientTelemetryOptions
                                                        .EnvPropsClientTelemetryEnabled, false);

            DefaultTrace.TraceInformation($"Telemetry Flag is set to {isTelemetryEnabled}");

            return isTelemetryEnabled;
        }

        internal static TimeSpan GetScheduledTimeSpan()
        {
            if (scheduledTimeSpan.Equals(TimeSpan.Zero))
            {
                double scheduledTimeInSeconds = ClientTelemetryOptions.DefaultTimeStampInSeconds;
                try
                {
                    scheduledTimeInSeconds = ConfigurationManager
                                                    .GetEnvironmentVariable<double>(
                                                           ClientTelemetryOptions.EnvPropsClientTelemetrySchedulingInSeconds,
                                                           ClientTelemetryOptions.DefaultTimeStampInSeconds);

                    if (scheduledTimeInSeconds <= 0)
                    {
                        throw new ArgumentException("Telemetry Scheduled time can not be less than or equal to 0.");
                    }
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceError($"Error while getting telemetry scheduling configuration : {ex.Message}. Falling back to default configuration i.e. {scheduledTimeInSeconds}" );
                }
               
                scheduledTimeSpan = TimeSpan.FromSeconds(scheduledTimeInSeconds);

                DefaultTrace.TraceInformation($"Telemetry Scheduled in Seconds {scheduledTimeSpan.TotalSeconds}");

            }
            return scheduledTimeSpan;
        }

        internal static string GetHostInformation(Compute vmInformation)
        {
            return String.Concat(vmInformation?.OSType, "|",
                    vmInformation?.SKU, "|",
                    vmInformation?.VMSize, "|",
                    vmInformation?.AzEnvironment);
        }

        internal static Uri GetClientTelemetryEndpoint()
        {
            if (clientTelemetryEndpoint == null)
            {
                string uriProp = ConfigurationManager
                    .GetEnvironmentVariable<string>(
                        ClientTelemetryOptions.EnvPropsClientTelemetryEndpoint, null);
                if (!String.IsNullOrEmpty(uriProp))
                {
                    clientTelemetryEndpoint = new Uri(uriProp);
                }

                DefaultTrace.TraceInformation($"Telemetry Endpoint URL is  {uriProp}");
            }
            return clientTelemetryEndpoint;
        }

        internal static string GetEnvironmentName()
        {
            if (String.IsNullOrEmpty(environmentName))
            {
                environmentName = ConfigurationManager
                .GetEnvironmentVariable<string>(
                    ClientTelemetryOptions.EnvPropsClientTelemetryEnvironmentName,
                    String.Empty);
            }
            return environmentName;
        }

        /// <summary>
        /// This method will return true if the request is failed with User or Server Exception and not excluded from telemetry.
        /// This method will return true if the request latency is more than the threshold.
        /// otherwise return false
        /// </summary>
        /// <param name="statusCode"></param>
        /// <param name="subStatusCode"></param>
        /// <param name="latencyInMs"></param>
        /// <returns>true/false</returns>
        internal static bool IsEligible(int statusCode, int subStatusCode, TimeSpan latencyInMs)
        {
            return
                ClientTelemetryOptions.IsStatusCodeNotExcluded(statusCode, subStatusCode) && 
                    (ClientTelemetryOptions.IsUserOrServerError(statusCode) || latencyInMs >= ClientTelemetryOptions.NetworkLatencyThreshold);
        }

        private static bool IsUserOrServerError(int statusCode)
        {
            return statusCode >= 400 && statusCode <= 599;
        }

        private static bool IsStatusCodeNotExcluded(int statusCode, int subStatusCode)
        {
            return !(ClientTelemetryOptions.ExcludedStatusCodes.Contains(statusCode) && subStatusCode == 0);
        }

    }
}
