//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class DocumentServiceLeaseContainerInMemory : DocumentServiceLeaseContainer
    {
        private readonly ConcurrentDictionary<string, DocumentServiceLease> container;

        public DocumentServiceLeaseContainerInMemory(ConcurrentDictionary<string, DocumentServiceLease> container)
        {
            this.container = container;
        }

        public override Task<IReadOnlyList<DocumentServiceLease>> GetAllLeasesAsync()
        {
            return Task.FromResult<IReadOnlyList<DocumentServiceLease>>(this.container.Values.ToList().AsReadOnly());
        }

        public override Task<IEnumerable<DocumentServiceLease>> GetOwnedLeasesAsync()
        {
            return Task.FromResult<IEnumerable<DocumentServiceLease>>(this.container.Values.AsEnumerable());
        }

        public override Task<IReadOnlyList<JsonElement>> ExportLeasesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<JsonElement> exportedLeases = new List<JsonElement>();
            
            foreach (DocumentServiceLease lease in this.container.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                string payload = JsonSerializer.Serialize(lease, lease.GetType());
                using (JsonDocument doc = JsonDocument.Parse(payload))
                {
                    exportedLeases.Add(doc.RootElement.Clone());
                }
            }

            return Task.FromResult<IReadOnlyList<JsonElement>>(exportedLeases.AsReadOnly());
        }

        public override Task ImportLeasesAsync(
            IReadOnlyList<JsonElement> leases,
            bool overwriteExisting = false,
            CancellationToken cancellationToken = default)
        {
            if (leases == null)
            {
                throw new ArgumentNullException(nameof(leases));
            }

            cancellationToken.ThrowIfCancellationRequested();

            foreach (JsonElement leaseElement in leases)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DocumentServiceLease lease = DeserializeLease(leaseElement);
                if (lease == null)
                {
                    continue;
                }

                if (overwriteExisting)
                {
                    this.container[lease.Id] = lease;
                }
                else
                {
                    // Only add if not already present
                    this.container.TryAdd(lease.Id, lease);
                }
            }

            return Task.CompletedTask;
        }

        private static DocumentServiceLease DeserializeLease(JsonElement leaseElement)
        {
            if (leaseElement.ValueKind == JsonValueKind.Undefined || leaseElement.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            string payloadJson = leaseElement.GetRawText();

            // Try EPK lease first, then fall back to Core lease
            try
            {
                DocumentServiceLeaseCoreEpk epkLease = JsonSerializer.Deserialize<DocumentServiceLeaseCoreEpk>(payloadJson);
                if (epkLease?.FeedRange != null)
                {
                    return epkLease;
                }
            }
            catch (JsonException)
            {
                // Fall through to try Core lease
            }

            try
            {
                return JsonSerializer.Deserialize<DocumentServiceLeaseCore>(payloadJson);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
