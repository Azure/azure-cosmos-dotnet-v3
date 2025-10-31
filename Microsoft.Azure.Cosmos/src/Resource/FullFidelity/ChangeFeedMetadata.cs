//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Resource.FullFidelity;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// The metadata of a change feed resource with <see cref="ChangeFeedMode"/> is initialized to <see cref="ChangeFeedMode.AllVersionsAndDeletes"/>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    class ChangeFeedMetadata
    {
        private readonly static DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// The change's conflict resolution timestamp.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public DateTime ConflictResolutionTimestamp => UnixEpoch.AddSeconds(this.ConflictResolutionTimestampInSeconds.Value);

        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName(ChangeFeedMetadataFields.ConflictResolutionTimestamp)]
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.ConflictResolutionTimestamp, NullValueHandling = NullValueHandling.Ignore)]
        internal double? ConflictResolutionTimestampInSeconds { get; set; }

        /// <summary>
        /// The current change's logical sequence number.
        /// </summary>
        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName(ChangeFeedMetadataFields.Lsn)]
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.Lsn, NullValueHandling = NullValueHandling.Ignore)]
        public long Lsn { get; internal set; }

        /// <summary>
        /// The change's feed operation type <see cref="ChangeFeedOperationType"/>.
        /// </summary>
        [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName(ChangeFeedMetadataFields.OperationType)]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.OperationType, NullValueHandling = NullValueHandling.Ignore)]
        public ChangeFeedOperationType OperationType { get; internal set; }

        /// <summary>
        /// The previous change's logical sequence number.
        /// </summary>
        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName(ChangeFeedMetadataFields.PreviousImageLSN)]
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.PreviousImageLSN, NullValueHandling = NullValueHandling.Ignore)]
        public long PreviousLsn { get; internal set; }

        /// <summary>
        /// Used to distinguish explicit deletes (e.g. via DeleteItem) from deletes caused by TTL expiration (a collection may define time-to-live policy for documents).
        /// </summary>
        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName(ChangeFeedMetadataFields.TimeToLiveExpired)]
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.TimeToLiveExpired, NullValueHandling = NullValueHandling.Ignore)]
        public bool IsTimeToLiveExpired { get; internal set; }

        /// <summary>
        /// Applicable for delete operations only, otherwise null.
        /// The id of the previous item version. 
        /// </summary>
        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName(ChangeFeedMetadataFields.Id)]
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.Id, NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; internal set; }

        /// <summary>
        ///  Applicable for delete operations only, otherwise null.
        /// The partition key of the previous item version. string  is the partition key property name and object is the partition key property value. All levels of hierarchy will be represented in order if a HPK is used.
        /// </summary>
        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName(ChangeFeedMetadataFields.PartitionKey)]
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.PartitionKey, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> PartitionKey { get; internal set; }
    }
}
