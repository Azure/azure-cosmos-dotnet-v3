//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;
    using Util;

    /// <summary>
    /// Task to collect virtual machine metadata information. using instance metedata service API.
    /// ref: https://docs.microsoft.com/en-us/azure/virtual-machines/windows/instance-metadata-service?tabs=windows
    /// Collects only application region and environment information
    /// </summary>
    internal static class VmMetadataApiHandler
    {
        internal const string HashedMachineNamePrefix = "hashedMachineName:";
        internal const string VmIdPrefix = "vmId:";
        internal const string UuidPrefix = "uuid:";

        internal static readonly Uri vmMetadataEndpointUrl = new ("http://169.254.169.254/metadata/instance?api-version=2020-06-01");

        private static readonly string nonAzureCloud = "NonAzureVM";

        private static readonly object lockObject = new object();

        private static bool isInitialized = false;
        private static AzureVMMetadata azMetadata = null;

        /// <summary>
        /// Check for environment variable COSMOS_DISABLE_IMDS_ACCESS to decide if VM metadata call should be made or not.
        /// If environment variable is set to true, then VM metadata call will not be made.
        /// If environment variable is set to false, then VM metadata call will be made.
        /// If environment variable is not set, then VM metadata call will be made.
        /// </summary>.
        /// <param name="httpClient"></param>
        internal static void TryInitialize(CosmosHttpClient httpClient)
        {
            bool isVMMetadataAccessDisabled = 
                ConfigurationManager.GetEnvironmentVariable<bool>("COSMOS_DISABLE_IMDS_ACCESS", false);
            if (isVMMetadataAccessDisabled)
            {
                return;
            }
  
            if (VmMetadataApiHandler.isInitialized)
            {
                return;
            }

            lock (VmMetadataApiHandler.lockObject)
            {
                if (VmMetadataApiHandler.isInitialized)
                {
                    return;
                }

                DefaultTrace.TraceInformation("Initializing VM Metadata API ");

                VmMetadataApiHandler.isInitialized = true;

                _ = Task.Run(() => MetadataApiCallAsync(httpClient), default);
            }
        }

        private static async Task MetadataApiCallAsync(CosmosHttpClient httpClient)
        {
            try
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

                HttpResponseMessage response = await httpClient
                    .SendHttpAsync(createRequestMessageAsync: CreateRequestMessage,
                                    resourceType: ResourceType.Telemetry,
                                    timeoutPolicy: HttpTimeoutPolicyNoRetry.Instance,
                                    clientSideRequestStatistics: null,
                                    cancellationToken: default);

                azMetadata = await VmMetadataApiHandler.ProcessResponseAsync(response);

                DefaultTrace.TraceInformation("Successfully get Instance Metadata Response : {0}", azMetadata.Compute.VMId);
            }
            catch (Exception e)
            {
                DefaultTrace.TraceInformation("Azure Environment metadata information not available. {0}", e.Message);
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

        /// <summary>
        /// Get VM Id if it is Azure System 
        ///             else Get Hashed MachineName
        ///             else Generate an unique id for this machine/process
        /// </summary>
        /// <returns>machine id</returns>
        internal static string GetMachineId()
        {
            if (!String.IsNullOrWhiteSpace(VmMetadataApiHandler.azMetadata?.Compute?.VMId))
            {
                return VmMetadataApiHandler.azMetadata.Compute.VMId;
            }

            return VmMetadataApiHandler.uniqueId.Value;
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
        /// Get Machine Region (If Azure System) else null
        /// </summary>
        /// <returns>VM region</returns>
        internal static string GetMachineRegion()
        {
            return VmMetadataApiHandler.azMetadata?.Compute?.Location;
        }

        /// <summary>
        /// Get Machine Region (If Azure System) else null
        /// </summary>
        /// <returns>VM region</returns>
        internal static string GetCloudInformation()
        {
            return VmMetadataApiHandler.azMetadata?.Compute?.AzEnvironment ?? VmMetadataApiHandler.nonAzureCloud;
        }

        private static readonly Lazy<string> uniqueId = new Lazy<string>(() =>
        {
            try
            {
                return $"{VmMetadataApiHandler.HashedMachineNamePrefix}{HashingExtension.ComputeHash(Environment.MachineName)}";
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning("Error while generating hashed machine name {0}", ex.Message);
            }

            return $"{VmMetadataApiHandler.UuidPrefix}{Guid.NewGuid()}";
        });

    }
}
