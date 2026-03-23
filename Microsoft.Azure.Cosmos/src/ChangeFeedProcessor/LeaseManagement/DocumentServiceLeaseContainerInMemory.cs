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
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Routing;

    internal sealed class DocumentServiceLeaseContainerInMemory : DocumentServiceLeaseContainer
    {
        private static readonly JsonSerializerOptions DeserializeOptions = new JsonSerializerOptions
        {
            Converters = { new FeedRangeInternalSystemTextJsonConverter() }
        };

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
                
                string payload = JsonSerializer.Serialize(lease, lease.GetType(), DeserializeOptions);
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

        internal static DocumentServiceLease DeserializeLease(JsonElement leaseElement)
        {
            if (leaseElement.ValueKind == JsonValueKind.Undefined || leaseElement.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            string payloadJson = leaseElement.GetRawText();

            // Try EPK lease first, then fall back to Core lease
            try
            {
                DocumentServiceLeaseCoreEpk epkLease = JsonSerializer.Deserialize<DocumentServiceLeaseCoreEpk>(payloadJson, DeserializeOptions);
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
                return JsonSerializer.Deserialize<DocumentServiceLeaseCore>(payloadJson, DeserializeOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// System.Text.Json converter for <see cref="FeedRangeInternal"/> that handles
        /// serialization and deserialization of FeedRange types (FeedRangeEpk, FeedRangePartitionKeyRange).
        /// </summary>
        private sealed class FeedRangeInternalSystemTextJsonConverter : JsonConverter<FeedRangeInternal>
        {
            public override FeedRangeInternal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using JsonDocument doc = JsonDocument.ParseValue(ref reader);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("Range", out JsonElement rangeElement))
                {
                    string min = rangeElement.GetProperty("Min").GetString();
                    string max = rangeElement.GetProperty("Max").GetString();

                    bool isMinInclusive = !rangeElement.TryGetProperty("IsMinInclusive", out JsonElement minIncEl)
                        || minIncEl.GetBoolean();
                    bool isMaxInclusive = rangeElement.TryGetProperty("IsMaxInclusive", out JsonElement maxIncEl)
                        && maxIncEl.GetBoolean();

                    return new FeedRangeEpk(new Range<string>(min, max, isMinInclusive, isMaxInclusive));
                }

                if (root.TryGetProperty("PartitionKeyRangeId", out JsonElement pkRangeIdElement))
                {
                    return new FeedRangePartitionKeyRange(pkRangeIdElement.GetString());
                }

                return null;
            }

            public override void Write(Utf8JsonWriter writer, FeedRangeInternal value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                if (value is FeedRangeEpk epk)
                {
                    writer.WritePropertyName("Range");
                    writer.WriteStartObject();
                    writer.WriteString("Min", epk.Range.Min);
                    writer.WriteString("Max", epk.Range.Max);
                    writer.WriteBoolean("IsMinInclusive", epk.Range.IsMinInclusive);
                    writer.WriteBoolean("IsMaxInclusive", epk.Range.IsMaxInclusive);
                    writer.WriteEndObject();
                }
                else if (value is FeedRangePartitionKeyRange pkRange)
                {
                    writer.WriteString("PartitionKeyRangeId", pkRange.PartitionKeyRangeId);
                }

                writer.WriteEndObject();
            }
        }
    }
}
