//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements.Telemetry;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class ClientTelemetryOptions
    {
        internal const String RequestKey = "telemetry";

        internal const long AdjustmentFactor = 100;
        
        internal const int BytesToMb = 1024 * 1024;
        internal const int OneKbToBytes = 1024;

        internal const int RequestLatencyMaxMicroSec = 99999;
        internal const int RequestLatencySuccessPrecision = 5;
        internal const int RequestLatencyFailurePrecision = 5;
        internal const string RequestLatencyName = "RequestLatency";
        internal const string RequestLatencyUnit = "MicroSec";

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

        internal static readonly HashSet<ResourceType> AllowedResourceTypes = new HashSet<ResourceType>(new ResourceType[]
        {
            ResourceType.Document
        });

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
                string vmMetadataUrlProp = CosmosConfigurationManager.GetEnvironmentVariable<string>(
                   EnvPropsClientTelemetryVmMetadataUrl, DefaultVmMetadataUrL);
                if (!String.IsNullOrEmpty(vmMetadataUrlProp))
                {
                    vmMetadataUrl = new Uri(vmMetadataUrlProp);
                }
            }
            return vmMetadataUrl;
        }

        internal static TimeSpan GetScheduledTimeSpan()
        {
            if (scheduledTimeSpan.Equals(TimeSpan.Zero))
            {
                double scheduledTimeInSeconds = CosmosConfigurationManager
                .GetEnvironmentVariable<double>(
                    ClientTelemetryOptions.EnvPropsClientTelemetrySchedulingInSeconds,
                    ClientTelemetryOptions.DefaultTimeStampInSeconds);

                if (scheduledTimeInSeconds <= 0)
                {
                    throw new ArgumentException("Telemetry Scheduled time can not be less than or equal to 0.");
                }
                scheduledTimeSpan = TimeSpan.FromSeconds(scheduledTimeInSeconds); 
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

        internal static string GetHostInformation(AzureVMMetadata azMetadata) 
        {
            return String.Concat(azMetadata?.OSType, "|",
                    azMetadata?.SKU, "|",
                    azMetadata?.VMSize, "|",
                    azMetadata?.AzEnvironment);
        } 

        internal static Uri GetClientTelemetryEndpoint()
        {
            if (clientTelemetryEndpoint == null)
            {
                string uriProp = CosmosConfigurationManager
                    .GetEnvironmentVariable<string>(
                        ClientTelemetryOptions.EnvPropsClientTelemetryEndpoint, null);
                if (!String.IsNullOrEmpty(uriProp))
                {
                    clientTelemetryEndpoint = new Uri(uriProp);
                }
            }
            return clientTelemetryEndpoint;
        }

        internal static string GetEnvironmentName()
        {
            if (String.IsNullOrEmpty(environmentName))
            {
                environmentName = CosmosConfigurationManager
                .GetEnvironmentVariable<string>(
                    ClientTelemetryOptions.EnvPropsClientTelemetryEnvironmentName,
                    String.Empty);
            }
            return environmentName;
        }
    }
}
