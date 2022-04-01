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
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Task to collect virtual machine metadata information. using instance metedata service API.
    /// ref: https://docs.microsoft.com/en-us/azure/virtual-machines/windows/instance-metadata-service?tabs=windows
    /// Collects only application region and environment information
    /// </summary>
    internal class VmMetadataApiHandler
    {
        internal static readonly Uri vmMetadataEndpointUrl = new ("http://169.254.169.254/metadata/instance?api-version=2020-06-01");

        private static readonly string UniqueId = "uuid:" + Guid.NewGuid().ToString();
        private static readonly object lockObject = new object();

        private static VmMetadataApiHandler instance = null;
        private static bool isInitialized = false;

        private static AzureVMMetadata azMetadata = null;

        private readonly CosmosHttpClient httpClient;

        private VmMetadataApiHandler(CosmosHttpClient httpClient)
        {
            this.httpClient = httpClient;

            _ = Task.Run(this.MetadataApiCallAsync, default)
                .ContinueWith(t => DefaultTrace.TraceWarning(
                         $"Exception while making metadata call {t.Exception}"),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        internal static VmMetadataApiHandler Initialize(CosmosHttpClient httpClient)
        {
            if (VmMetadataApiHandler.isInitialized)
            {
                return VmMetadataApiHandler.instance;
            }

            lock (VmMetadataApiHandler.lockObject)
            {
                DefaultTrace.TraceInformation("Initializing VM Metadata API ");
                VmMetadataApiHandler.instance = new VmMetadataApiHandler(httpClient);

                VmMetadataApiHandler.isInitialized = true;

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
                    RequestUri = VmMetadataApiHandler.vmMetadataEndpointUrl,
                    Method = HttpMethod.Get,
                };
                request.Headers.Add("Metadata", "true");

                return new ValueTask<HttpRequestMessage>(request);
            }

            HttpResponseMessage response = await this.httpClient
                .SendHttpAsync(createRequestMessageAsync: CreateRequestMessage,
                                resourceType: ResourceType.Telemetry,
                                timeoutPolicy: HttpTimeoutPolicyNoRetry.Instance,
                                clientSideRequestStatistics: null,
                                cancellationToken: default);

            azMetadata = await VmMetadataApiHandler.ProcessResponseAsync(response);

            DefaultTrace.TraceInformation($"Succesfully get Instance Metadata Response : {azMetadata.Compute.VMId}");
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

        /// <summary>
        /// Get VM Id if it is Azure System else Generate an unique id for this machine
        /// </summary>
        /// <returns>machine id</returns>
        internal static string GetMachineId()
        {
            return VmMetadataApiHandler.azMetadata == null ? VmMetadataApiHandler.UniqueId : VmMetadataApiHandler.azMetadata.Compute.VMId;
        }

        /// <summary>
        /// Get Machine Information (If Azure System) else null
        /// </summary>
        /// <returns>Compute</returns>
        internal static Compute GetMachineInfo()
        {
            return VmMetadataApiHandler.azMetadata?.Compute;     
        }

        /// <summary>
        /// Only for tests, as this cache needs to clear while switching between Azure System tests and non Azure system tests
        /// </summary>
        internal static void Clear()
        {
            VmMetadataApiHandler.azMetadata = null;
            VmMetadataApiHandler.isInitialized = false;
        }
    }
}
