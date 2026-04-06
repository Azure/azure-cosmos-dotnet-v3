//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Routing;

    internal sealed class DocumentServiceLeaseContainerInMemory : DocumentServiceLeaseContainer
    {
        private static readonly JsonSerializerOptions DeserializeOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new FeedRangeInternalSystemTextJsonConverter() }
        };

        private readonly ConcurrentDictionary<string, DocumentServiceLease> container;

        internal MemoryStream LeaseStateStream { get; set; }

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

        /// <summary>
        /// Initiates shutdown of in-memory lease container. 
        /// Exports all processor leases from current in-memory lease container.
        /// </summary>
        /// <returns>A task that represents the asynchronous shutdown operation.</returns>
        public override Task ShutdownAsync()
        {
            if (this.LeaseStateStream == null)
            {
                return Task.CompletedTask;
            }

            List<JsonElement> exportedLeases = new List<JsonElement>();

            foreach (DocumentServiceLease lease in this.container.Values)
            {
                if (!(lease.FeedRange is FeedRangeEpk))
                {
                    continue;
                }

                string payload = JsonSerializer.Serialize(lease, lease.GetType(), DeserializeOptions);
                using (JsonDocument doc = JsonDocument.Parse(payload))
                {
                    exportedLeases.Add(doc.RootElement.Clone());
                }
            }

            this.LeaseStateStream.SetLength(0);
            string json = JsonSerializer.Serialize(exportedLeases);
            byte[] data = Encoding.UTF8.GetBytes(json);
            this.LeaseStateStream.Write(data, 0, data.Length);
            this.LeaseStateStream.Position = 0;

            return Task.CompletedTask;
        }

        internal static DocumentServiceLease DeserializeLease(JsonElement leaseElement)
        {
            if (leaseElement.ValueKind == JsonValueKind.Undefined || leaseElement.ValueKind == JsonValueKind.Null)
            {
                throw new ArgumentException("Lease element cannot be null or undefined.", nameof(leaseElement));
            }

            string payloadJson = leaseElement.GetRawText();

            // Only EPK leases with FeedRange are supported
            DocumentServiceLeaseCoreEpk epkLease = JsonSerializer.Deserialize<DocumentServiceLeaseCoreEpk>(payloadJson, DeserializeOptions);

            if (epkLease?.FeedRange == null)
            {
                throw new InvalidOperationException("Only EPK-based leases with a FeedRange are supported for deserialization.");
            }

            return epkLease;
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
