//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Resource.FullFidelity;
    using Microsoft.Azure.Cosmos.Resource.FullFidelity.Converters;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// The metadata of a change feed resource with <see cref="ChangeFeedMode"/> is initialized to <see cref="ChangeFeedMode.AllVersionsAndDeletes"/>.
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(ChangeFeedMetadataConverter))]
    [JsonConverter(typeof(ChangeFeedMetadataNewtonSoftConverter))]
#if PREVIEW
    public
#else
    internal
#endif
    class ChangeFeedMetadata
    {
        /// <summary>
        /// The change's conflict resolution timestamp.
        /// </summary>
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.ConflictResolutionTimestamp, NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime ConflictResolutionTimestamp { get; internal set; }

        /// <summary>
        /// The current change's logical sequence number.
        /// </summary>
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.Lsn, NullValueHandling = NullValueHandling.Ignore)]
        public long Lsn { get; internal set; }

        /// <summary>
        /// The change's feed operation type <see cref="ChangeFeedOperationType"/>.
        /// </summary>
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.OperationType, NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ChangeFeedOperationType OperationType { get; internal set; }

        /// <summary>
        /// The previous change's logical sequence number.
        /// </summary>
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.PreviousImageLSN, NullValueHandling = NullValueHandling.Ignore)]
        public long PreviousLsn { get; internal set; }

        /// <summary>
        /// Used to distinguish explicit deletes (e.g. via DeleteItem) from deletes caused by TTL expiration (a collection may define time-to-live policy for documents).
        /// </summary>
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.TimeToLiveExpired, NullValueHandling = NullValueHandling.Ignore)]
        public bool IsTimeToLiveExpired { get; internal set; }

        /// <summary>
        /// Used for delete operations only.
        /// The id of the previous item version. 
        /// </summary>
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.Id, NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; internal set; }

        /// <summary>
        ///  Used for delete operations only.
        /// The partition key of the previous item version. string  is the partition key property name and object is the partition key property value. All levels of hierarchy will be represented in order if a HPK is used.
        /// </summary>
        [JsonProperty(PropertyName = ChangeFeedMetadataFields.PartitionKey, NullValueHandling = NullValueHandling.Ignore)]
        public List<(string, object)> PartitionKey { get; internal set; }
    }
}
