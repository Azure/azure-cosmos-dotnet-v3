//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Task to collect virtual machine metadata information. using instance metedata service API.
    /// ref: https://docs.microsoft.com/en-us/azure/virtual-machines/windows/instance-metadata-service?tabs=windows
    /// Collects only application region and environment information
    /// </summary>
    internal class VmMetadataApiHandler : IDisposable
    {
        private const string DefaultVmMetadataUrL = "http://169.254.169.254/metadata/instance?api-version=2020-06-01";
        private static readonly string UniqueId = "uuid:" + Guid.NewGuid().ToString();
        private static readonly object lockObject = new object();
        private static readonly Uri vmMetadataEndpointUrl = VmMetadataApiHandler.GetVmMetadataUrl();

        private static VmMetadataApiHandler instance = null;
        private static Uri vmMetadataUrl;

        private static volatile AzureVMMetadata azMetadata = null;

        private readonly CosmosHttpClient httpClient;
        private Task apiCallTask;

        private VmMetadataApiHandler(CosmosHttpClient httpClient)
        {
            this.httpClient = httpClient;
            this.apiCallTask = Task.Run(this.MetadataApiCallAsync, default);
        }

        internal static VmMetadataApiHandler Initialize(CosmosHttpClient httpClient)
        {
            if (VmMetadataApiHandler.instance != null)
            {
                return VmMetadataApiHandler.instance;
            }

            lock (VmMetadataApiHandler.lockObject)
            {
                DefaultTrace.TraceInformation("Initializing VM Metadata API ");
                VmMetadataApiHandler.instance = new VmMetadataApiHandler(httpClient);
 
                return VmMetadataApiHandler.instance;
            }
        }

        private async Task MetadataApiCallAsync()
        {
            DefaultTrace.TraceInformation($"Loading VM Metadata");

            static ValueTask<HttpRequestMessage> CreateRequestMessage()
            {
                HttpRequestMessage request = new HttpRequestMessage()
                {
                    RequestUri = vmMetadataEndpointUrl,
                    Method = HttpMethod.Get,
                };
                request.Headers.Add("Metadata", "true");

                return new ValueTask<HttpRequestMessage>(request);
            }

            try
            {
                HttpResponseMessage response = await this.httpClient
                  .SendHttpAsync(createRequestMessageAsync: CreateRequestMessage,
                                 resourceType: ResourceType.Telemetry,
                                 timeoutPolicy: HttpTimeoutPolicyNoRetry.Instance,
                                 clientSideRequestStatistics: null,
                                 cancellationToken: default);
                azMetadata = await VmMetadataApiHandler.ProcessResponseAsync(response);

                DefaultTrace.TraceWarning("Succesfully get Instance Metedata Response : " + azMetadata.Compute.VMId);

            }
            catch (Exception ex)
            {
                VmMetadataApiHandler.azMetadata = null;

                DefaultTrace.TraceWarning("Exception while making metadata call " + ex.ToString());
            }

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

        internal static Uri GetVmMetadataUrl()
        {
            if (vmMetadataUrl == null)
            {
                string vmMetadataUrlProp = ConfigurationManager.GetEnvironmentVariable<string>(
                   ClientTelemetryOptions.EnvPropsClientTelemetryVmMetadataUrl, DefaultVmMetadataUrL);
                if (!String.IsNullOrEmpty(vmMetadataUrlProp))
                {
                    vmMetadataUrl = new Uri(vmMetadataUrlProp);
                }
                DefaultTrace.TraceInformation($"VM Metadata URL {vmMetadataUrlProp}");
            }
            return vmMetadataUrl;
        }

        internal static string GetMachineId()
        {
            return VmMetadataApiHandler.azMetadata == null ? VmMetadataApiHandler.UniqueId : VmMetadataApiHandler.azMetadata.Compute.VMId;
        }

        internal static Compute GetMachineInfo()
        {
            return VmMetadataApiHandler.azMetadata?.Compute;     
        }

        public void Dispose()
        {
            this.apiCallTask = null;
            VmMetadataApiHandler.instance = null;
        }
    }
}
