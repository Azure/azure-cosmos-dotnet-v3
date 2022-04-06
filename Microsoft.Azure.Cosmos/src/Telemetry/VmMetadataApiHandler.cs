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
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Task to collect virtual machine metadata information. using instance metedata service API.
    /// ref: https://docs.microsoft.com/en-us/azure/virtual-machines/windows/instance-metadata-service?tabs=windows
    /// Collects only application region and environment information
    /// </summary>
    internal static class VmMetadataApiHandler
    {
        internal static readonly Uri vmMetadataEndpointUrl = new ("http://169.254.169.254/metadata/instance?api-version=2020-06-01");
        
        private static readonly string UniqueId = "uuid:" + Guid.NewGuid().ToString();
        private static readonly string HashedMachineName = "hashedMachineName:" + VmMetadataApiHandler.ComputeHash(Environment.MachineName);

        private static readonly object lockObject = new object();

        private static bool isInitialized = false;
        private static AzureVMMetadata azMetadata = null;
       
        internal static void TryInitialize(CosmosHttpClient httpClient)
        {
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

                _ = Task.Run(() => MetadataApiCallAsync(httpClient), default)
                            .ContinueWith(t => DefaultTrace.TraceWarning($"Exception while making metadata call {t.Exception}"),
                            TaskContinuationOptions.OnlyOnFaulted);

            }
        }

        private static async Task MetadataApiCallAsync(CosmosHttpClient httpClient)
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
        /// Get VM Id if it is Azure System 
        ///             else Get Hashed MachineName
        ///             else Generate an unique id for this machine/process
        /// </summary>
        /// <returns>machine id</returns>
        internal static string GetMachineId()
        {
            if (VmMetadataApiHandler.azMetadata != null)
            {
                return VmMetadataApiHandler.azMetadata.Compute.VMId;
            }

            try
            {
                return VmMetadataApiHandler.HashedMachineName;
            }
            catch (Exception)
            {
                return VmMetadataApiHandler.UniqueId;
            }
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
        /// Hash a passed Value
        /// </summary>
        /// <param name="rawData"></param>
        /// <returns>hashed Value</returns>
        internal static string ComputeHash(string rawData)
        {
            // Create a SHA256    
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        internal static void Clear()
        {
            VmMetadataApiHandler.azMetadata = null;
            VmMetadataApiHandler.isInitialized = false; 
        }
    }
}
