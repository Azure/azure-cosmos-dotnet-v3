//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class ClientTelemetryOptions
    {
        internal const String RequestKey = "telemetry";

        internal const long AdjustmentFactor = 100;
        
        internal const int BytesToMb = 1024 * 1024;
        internal const int OneKbToBytes = 1024;

        internal const int RequestLatencyMaxMilliSec = Int32.MaxValue;
        internal const int RequestLatencyPrecision = 5;
        internal const string RequestLatencyName = "RequestLatency";
        internal const string RequestLatencyUnit = "MilliSecond";

        internal const int RequestChargePrecision = 5;
        internal const string RequestChargeName = "RequestCharge";
        internal const string RequestChargeUnit = "RU";

        internal const int CpuMax = 100;
        internal const int CpuPrecision = 3;
        internal const String CpuName = "CPU";
        internal const String CpuUnit = "Percentage";

        internal const long MemoryMax = Int64.MaxValue;
        internal const int MemoryPrecision = 5;
        internal const String MemoryName = "Memory Remaining";
        internal const String MemoryUnit = "MB";

        internal const string DefaultVmMetadataUrL = "http://169.254.169.254/metadata/instance?api-version=2020-06-01";
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

        internal static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

        internal static readonly int RequestChargeMax = 99999 * Convert.ToInt32(AdjustmentFactor);
        internal static readonly int RequestChargeMin = 1 * Convert.ToInt32(AdjustmentFactor);

        private static Uri vmMetadataUrl;
        private static TimeSpan scheduledTimeSpan = TimeSpan.Zero;
        private static Uri clientTelemetryEndpoint;
        private static string environmentName;

        internal static Uri GetVmMetadataUrl()
        {
            if (vmMetadataUrl == null)
            {
                string vmMetadataUrlProp = ConfigurationManager.GetEnvironmentVariable<string>(
                   EnvPropsClientTelemetryVmMetadataUrl, DefaultVmMetadataUrL);
                if (!String.IsNullOrEmpty(vmMetadataUrlProp))
                {
                    vmMetadataUrl = new Uri(vmMetadataUrlProp);
                }

                DefaultTrace.TraceInformation("VM metadata URL for telemetry " + vmMetadataUrlProp);
            }
            return vmMetadataUrl;
        }

        internal static TimeSpan GetScheduledTimeSpan()
        {
            if (scheduledTimeSpan.Equals(TimeSpan.Zero))
            {
                double scheduledTimeInSeconds = ConfigurationManager
                .GetEnvironmentVariable<double>(
                    ClientTelemetryOptions.EnvPropsClientTelemetrySchedulingInSeconds,
                    ClientTelemetryOptions.DefaultTimeStampInSeconds);

                if (scheduledTimeInSeconds <= 0)
                {
                    throw new ArgumentException("Telemetry Scheduled time can not be less than or equal to 0.");
                }
                scheduledTimeSpan = TimeSpan.FromSeconds(scheduledTimeInSeconds);

                DefaultTrace.TraceInformation("Telemetry Scheduled in Seconds " + scheduledTimeSpan.TotalSeconds);

            }
            return scheduledTimeSpan;
        }

        internal static async Task<AzureVMMetadata> ProcessResponseAsync(HttpResponseMessage httpResponseMessage)
        {
            if (httpResponseMessage.Content == null)
            {
                return null;
            }
            string jsonVmInfo = await httpResponseMessage.Content.ReadAsStringAsync();
            return JObject.Parse(jsonVmInfo).ToObject<AzureVMMetadata>();
        }

        internal static string GetHostInformation(Compute vmInformation) 
        {
            return String.Concat(vmInformation.OSType, "|",
                    vmInformation.SKU, "|",
                    vmInformation.VMSize, "|",
                    vmInformation.AzEnvironment);
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

                DefaultTrace.TraceInformation("Telemetry Endpoint URL is  " + uriProp);
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
    }
}
