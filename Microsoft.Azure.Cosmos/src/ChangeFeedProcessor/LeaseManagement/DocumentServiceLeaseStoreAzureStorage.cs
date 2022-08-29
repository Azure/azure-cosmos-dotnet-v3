//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Blobs.Models;
    using global::Azure.Storage.Blobs.Specialized;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Newtonsoft.Json;

    /// <summary>
    /// Implementation of <see cref="DocumentServiceLeaseStore"/> for state in Azure Cosmos DB
    /// </summary>
    internal sealed class DocumentServiceLeaseStoreAzureStorage : DocumentServiceLeaseStore
    {
        internal const string InitializationBlobName = "InitializationBlob";
        private const string InitializationStateName = "InitializationState";
        private const string InitializationStateCompleted = "Completed";
        
        private readonly BlobContainerClient container;
        private string leaseId;

        public DocumentServiceLeaseStoreAzureStorage(
            BlobContainerClient container)
        {
            this.container = container;
        }

        public override async Task<bool> IsInitializedAsync()
        {
            var blob = this.container.GetBlobClient(InitializationBlobName);
            try
            {
                if (!await blob.ExistsAsync())
                {
                    await blob.UploadAsync(new MemoryStream());
                }
                var properties = await blob.GetPropertiesAsync();
                if (properties.Value.Metadata.TryGetValue(InitializationStateName, out var value) && value == InitializationStateCompleted)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        public override async Task MarkInitializedAsync()
        {
            var blob = this.container.GetBlobClient(InitializationBlobName);
            var result = await blob.SetMetadataAsync(new Dictionary<string, string> {{InitializationStateName, InitializationStateCompleted}}, 
                new BlobRequestConditions{LeaseId = this.leaseId}).ConfigureAwait(false);
        }

        public override async Task<bool> AcquireInitializationLockAsync(TimeSpan lockTime)
        {
            try
            {
                var blob = this.container.GetBlobClient(InitializationBlobName);
                BlobLeaseClient blobLeaseClient = blob.GetBlobLeaseClient();
                var response = await blobLeaseClient.AcquireAsync(lockTime);
                this.leaseId = response.Value.LeaseId;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override async Task<bool> ReleaseInitializationLockAsync()
        {
            try
            {
                var blob = this.container.GetBlobClient(InitializationBlobName);
                BlobLeaseClient blobLeaseClient = blob.GetBlobLeaseClient(this.leaseId);
                await blobLeaseClient.ReleaseAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}