//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

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
        /// <summary>
        /// New instance of meta data for <see cref="ChangeFeedItem{T}"/> created.
        /// </summary>
        /// <param name="conflictResolutionTimestamp"></param>
        /// <param name="lsn"></param>
        /// <param name="operationType"></param>
        /// <param name="previousLsn"></param>
        /// <param name="isTimeToLiveExpired"></param>
        public ChangeFeedMetadata(
            DateTime conflictResolutionTimestamp,
            long lsn,
            ChangeFeedOperationType operationType,
            long previousLsn,
            bool isTimeToLiveExpired)
        {
            this.ConflictResolutionTimestamp = conflictResolutionTimestamp;
            this.Lsn = lsn;
            this.OperationType = operationType;
            this.PreviousLsn = previousLsn;
            this.IsTimeToLiveExpired = isTimeToLiveExpired;
        }

        /// <summary>
        /// The conflict resolution timestamp.
        /// </summary>
        [JsonProperty(PropertyName = "crts", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("crts")]
        [Newtonsoft.Json.JsonConverter(typeof(UnixDateTimeConverter))]
        [System.Text.Json.Serialization.JsonConverter(typeof(Resource.FullFidelity.Converters.STJUnixDateTimeConverter))]
        public DateTime ConflictResolutionTimestamp { get; }

        /// <summary>
        /// The current logical sequence number.
        /// </summary>
        [JsonProperty(PropertyName = "lsn", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("lsn")]
        public long Lsn { get; }

        /// <summary>
        /// The change feed operation type.
        /// </summary>
        [JsonProperty(PropertyName = "operationType", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("operationType")]
        public ChangeFeedOperationType OperationType { get; }

        /// <summary>
        /// The previous logical sequence number.
        /// </summary>
        [JsonProperty(PropertyName = "previousImageLSN", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("previousImageLSN")]
        public long PreviousLsn { get; }

        /// <summary>
        /// Used to distinquish explicit deletes (e.g. via DeleteItem) from deletes caused by TTL expiration (a collection may define time-to-live policy for documents).
        /// </summary>
        [JsonProperty(PropertyName = "timeToLiveExpired", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("timeToLiveExpired")]
        public bool IsTimeToLiveExpired { get; }
    }
}
