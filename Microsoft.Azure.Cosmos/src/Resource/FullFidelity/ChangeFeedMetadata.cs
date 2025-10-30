//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.Resource.FullFidelity;
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
        public DateTime ConflictResolutionTimestamp => UnixEpoch.AddSeconds(this.ConflictResolutionTimestampInSeconds.Value);

        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName(ChangeFeedMetadataFields.ConflictResolutionTimestamp)]
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.ConflictResolutionTimestamp, NullValueHandling = NullValueHandling.Ignore)]
        internal double? ConflictResolutionTimestampInSeconds { get; set; }

        /// <summary>
        /// The current change's logical sequence number.
        /// </summary>
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.Lsn, NullValueHandling = NullValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName(ChangeFeedMetadataFields.Lsn)]
        public long Lsn { get; internal set; }

        /// <summary>
        /// The change's feed operation type <see cref="ChangeFeedOperationType"/>.
        /// </summary>
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.OperationType, NullValueHandling = NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName(ChangeFeedMetadataFields.OperationType)]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
        public ChangeFeedOperationType OperationType { get; internal set; }

        /// <summary>
        /// The previous change's logical sequence number.
        /// </summary>
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.PreviousImageLSN, NullValueHandling = NullValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName(ChangeFeedMetadataFields.PreviousImageLSN)]
        public long PreviousLsn { get; internal set; }

        /// <summary>
        /// Used to distinquish explicit deletes (e.g. via DeleteItem) from deletes caused by TTL expiration (a collection may define time-to-live policy for documents).
        /// </summary>
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.TimeToLiveExpired, NullValueHandling = NullValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName(ChangeFeedMetadataFields.TimeToLiveExpired)]
        public bool IsTimeToLiveExpired { get; internal set; }

        [System.Text.Json.Serialization.JsonInclude]
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.PartitionKey, NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName(ChangeFeedMetadataFields.PartitionKey)]
        public Dictionary<string, object> PartitionKey { get; internal set; }
    }
}
