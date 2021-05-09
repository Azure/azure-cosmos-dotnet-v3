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
    using Newtonsoft.Json.Linq;

    internal class ClientTelemetryOptions
    {
        internal const String RequestKey = "telemetry";

        internal const int OneKbToBytes = 1024;

        internal const int RequestLatencyMaxMicroSec = 300000000;
        internal const int RequestLatencySuccessPrecision = 4;
        internal const int RequestLatencyFailurePrecision = 2;
        internal const string RequestLatencyName = "RequestLatency";
        internal const string RequestLatencyUnit = "MicroSec";

        internal const int RequestChargeMax = 10000;
        internal const int RequestChargePrecision = 2;
        internal const string RequestChargeName = "RequestCharge";
        internal const string RequestChargeUnit = "RU";

        internal const int CpuMax = 100;
        internal const int CpuPrecision = 2;
        internal const String CpuName = "CPU";
        internal const String CpuUnit = "Percentage";

        internal const string DefaultVmMetadataUrL = "http://169.254.169.254/metadata/instance?api-version=2020-06-01";
        internal const double DefaultTimeStampInSeconds = 600;
        internal const double Percentile50 = 50.0;
        internal const double Percentile90 = 90.0;
        internal const double Percentile95 = 95.0;
        internal const double Percentile99 = 99.0;
        internal const double Percentile999 = 99.9;
        internal const string DateFormat = "yyyy-MM-ddTHH:mm:ssZ";

        public const string EnvPropsClientTelemetrySchedulingInSeconds = "COSMOS.CLIENT_TELEMETRY_SCHEDULING_IN_SECONDS";
        public const string EnvPropsClientTelemetryEnabled = "COSMOS.CLIENT_TELEMETRY_ENABLED";
        public const string EnvPropsClientTelemetryVmMetadataUrl = "COSMOS.VM_METADATA_URL";
    
        internal static readonly List<ResourceType> AllowedResourceTypes = new List<ResourceType>(new ResourceType[]
        {
            ResourceType.Document
        });

        internal static string GetVmMetadataUrl()
        {
            return CosmosConfigurationManager.GetEnvironmentVariable<string>(
                    EnvPropsClientTelemetryVmMetadataUrl,
                    DefaultVmMetadataUrL);
        }

        internal static double GetSchedulingInSeconds()
        {
            return CosmosConfigurationManager
                .GetEnvironmentVariable<double>(
                    ClientTelemetryOptions.EnvPropsClientTelemetrySchedulingInSeconds,
                    ClientTelemetryOptions.DefaultTimeStampInSeconds);
        }

        internal static async Task<AzureVMMetadata> ProcessResponseAsync(HttpResponseMessage httpResponseMessage)
        {
            string jsonVmInfo = await httpResponseMessage.Content.ReadAsStringAsync();
            return JObject.Parse(jsonVmInfo).ToObject<AzureVMMetadata>();
        }

        internal static string GetHostInformation(AzureVMMetadata azMetadata) 
        {
            return String.Concat(azMetadata.OSType, "|",
                    azMetadata.SKU, "|",
                    azMetadata.VMSize, "|",
                    azMetadata.AzEnvironment);
        }

        internal static HttpClient GetHttpClient(ConnectionPolicy connectionPolicy)
        {
            return connectionPolicy.HttpClientFactory != null
                    ? connectionPolicy.HttpClientFactory.Invoke()
                    : new HttpClient(CosmosHttpClientCore.CreateHttpClientHandler(
                        gatewayModeMaxConnectionLimit: connectionPolicy.MaxConnectionLimit,
                        webProxy: null));
        }
    }
}
