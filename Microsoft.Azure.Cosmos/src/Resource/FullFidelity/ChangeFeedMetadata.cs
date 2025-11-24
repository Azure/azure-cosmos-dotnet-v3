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
        [Newtonsoft.Json.JsonIgnore]
        public DateTime ConflictResolutionTimestamp => UnixEpoch.AddSeconds(this.ConflictResolutionTimestampInSeconds);

        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName(ChangeFeedMetadataFields.ConflictResolutionTimestamp)]
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.ConflictResolutionTimestamp, Required = Required.Always)]
        internal double ConflictResolutionTimestampInSeconds { get; set; }

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
        /// Applicable for delete operations only, otherwise null.
        /// The partition key of the previous item version represented as a dictionary where the key is the partition key property name 
        /// and the value is the partition key property value. All levels of hierarchy will be present if a hierarchical partition key (HPK) is used.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For single partition key containers, the dictionary will contain one entry with the partition key path name (without the leading '/') 
        /// as the key and the partition key value as the value.
        /// </para>
        /// <para>
        /// For hierarchical partition key containers, the dictionary will contain multiple entries, one for each level of the hierarchy, 
        /// as defined in the container's partition key definition.
        /// </para>
        /// <para>
        /// Example for a single partition key container with partition key path "/tenantId":
        /// <code>
        /// {
        ///     "tenantId": "tenant123"
        /// }
        /// </code>
        /// </para>
        /// <para>
        /// Example for a hierarchical partition key container with partition key paths ["/tenantId", "/userId", "/sessionId"]:
        /// <code>
        /// {
        ///     "tenantId": "tenant123",
        ///     "userId": "user456",
        ///     "sessionId": "session789"
        /// }
        /// </code>
        /// </para>
        /// <para>
        /// The partition key values can be of different types (string, number, boolean, null) depending on the document's schema.
        /// For example, with partition key paths ["/category", "/priority"]:
        /// <code>
        /// {
        ///     "category": "electronics",
        ///     "priority": 1
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName(ChangeFeedMetadataFields.PartitionKey)]
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.PartitionKey, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> PartitionKey { get; internal set; }
    }
}
