//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Util;

    public static class ClientTelemetryOptions
    {
        // ConversionFactor used in Histogram calculation to maintain precision or to collect data in desired unit
        public const int HistogramPrecisionFactor = 100;
        public const double TicksToMsFactor = TimeSpan.TicksPerMillisecond;
        public const int KbToMbFactor = 1024;

        public const int OneKbToBytes = 1024;

        // Expecting histogram to have Minimum Latency of 1 and Maximum Latency of 1 hour (which is never going to happen)
        public const long RequestLatencyMax = TimeSpan.TicksPerHour;
        public const long RequestLatencyMin = 1;
        public const int RequestLatencyPrecision = 4;
        public const string RequestLatencyName = "RequestLatency";
        public const string RequestLatencyUnit = "MilliSecond";

        // Expecting histogram to have Minimum Request Charge of 1 and Maximum Request Charge of 9999900
        // For all the Document ReadWriteQuery Operations there will be at least 1 request charge.
        public const long RequestChargeMax = 9999900;
        public const long RequestChargeMin = 1;
        public const int RequestChargePrecision = 2;
        public const string RequestChargeName = "RequestCharge";
        public const string RequestChargeUnit = "RU";

        // Expecting histogram to have Minimum CPU Usage of .001% and Maximum CPU Usage of 999.99%
        public const long CpuMax = 99999;
        public const long CpuMin = 1;
        public const int CpuPrecision = 2;
        public const String CpuName = "CPU";
        public const String CpuUnit = "Percentage";

        // Expecting histogram to have Minimum Memory Remaining of 1 MB and Maximum Memory Remaining of Long Max Value
        public const long MemoryMax = Int64.MaxValue;
        public const long MemoryMin = 1;
        public const int MemoryPrecision = 2;
        public const String MemoryName = "MemoryRemaining";
        public const String MemoryUnit = "MB";

        // Expecting histogram to have Minimum Available Threads = 0 and Maximum Available Threads = it can be any anything depends on the machine
        public const long AvailableThreadsMax = Int64.MaxValue;
        public const long AvailableThreadsMin = 1;
        public const int AvailableThreadsPrecision = 2;
        public const String AvailableThreadsName = "SystemPool_AvailableThreads";
        public const String AvailableThreadsUnit = "ThreadCount";

        // Expecting histogram to have Minimum ThreadWaitIntervalInMs of 1 and Maximum ThreadWaitIntervalInMs of 1 second
        public const long ThreadWaitIntervalInMsMax = TimeSpan.TicksPerSecond;
        public const long ThreadWaitIntervalInMsMin = 1;
        public const int ThreadWaitIntervalInMsPrecision = 2;
        public const string ThreadWaitIntervalInMsName = "SystemPool_ThreadWaitInterval";
        public const string ThreadWaitIntervalInMsUnit = "MilliSecond";

        // Expecting histogram to have Minimum Number of TCP connections as 1 and Maximum Number Of TCP connection as 70000
        public const long NumberOfTcpConnectionMax = 70000;
        public const long NumberOfTcpConnectionMin = 1;
        public const int NumberOfTcpConnectionPrecision = 2;
        public const string NumberOfTcpConnectionName = "RntbdOpenConnections";
        public const string NumberOfTcpConnectionUnit = "Count";

        public const string IsThreadStarvingName = "SystemPool_IsThreadStarving_True";
        public const string IsThreadStarvingUnit = "Count";

        public const double DefaultTimeStampInSeconds = 600;
        public const double Percentile50 = 50.0;
        public const double Percentile90 = 90.0;
        public const double Percentile95 = 95.0;
        public const double Percentile99 = 99.0;
        public const double Percentile999 = 99.9;
        public const string DateFormat = "yyyy-MM-ddTHH:mm:ssZ";

        public const string EnvPropsClientTelemetrySchedulingInSeconds = "COSMOS.CLIENT_TELEMETRY_SCHEDULING_IN_SECONDS";
        public const string EnvPropsClientTelemetryEnabled = "COSMOS.CLIENT_TELEMETRY_ENABLED";
        public const string EnvPropsClientTelemetryVmMetadataUrl = "COSMOS.VM_METADATA_URL";
        public const string EnvPropsClientTelemetryEndpoint = "COSMOS.CLIENT_TELEMETRY_ENDPOINT";
        public const string EnvPropsClientTelemetryEnvironmentName = "COSMOS.ENVIRONMENT_NAME";

        internal static readonly ResourceType AllowedResourceTypes = ResourceType.Document;

        public static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        { 
            NullValueHandling = NullValueHandling.Ignore,
            MaxDepth = 64, // https://github.com/advisories/GHSA-5crp-9r3c-p9vr
        };

        private static Uri clientTelemetryEndpoint;
        private static string environmentName;
        private static TimeSpan scheduledTimeSpan = TimeSpan.Zero;

        public static bool IsClientTelemetryEnabled()
        {
            bool isTelemetryEnabled = ConfigurationManager
                .GetEnvironmentVariable<bool>(ClientTelemetryOptions
                                                        .EnvPropsClientTelemetryEnabled, false);

            DefaultTrace.TraceInformation($"Telemetry Flag is set to {isTelemetryEnabled}");

            return isTelemetryEnabled;
        }

        public static TimeSpan GetScheduledTimeSpan()
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

        public static Uri GetClientTelemetryEndpoint()
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

        public static string GetEnvironmentName()
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
    }
}
