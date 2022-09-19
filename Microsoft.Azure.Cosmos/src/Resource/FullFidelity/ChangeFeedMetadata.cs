//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// The metadata of a change feed resource with <see cref="ChangeFeedMode"/> is initialized to <see cref="ChangeFeedMode.FullFidelity"/>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif 
        class ChangeFeedMetadata
    {
        /// <summary>
        /// New instance of meta data for <see cref="ChangeFeedItemChange{T}"/> created.
        /// </summary>
        /// <param name="conflictResolutionTimestamp"></param>
        /// <param name="lsn"></param>
        /// <param name="operationType"></param>
        /// <param name="previousLsn"></param>
        public ChangeFeedMetadata(
            DateTime conflictResolutionTimestamp,
            long lsn,
            ChangeFeedOperationType operationType,
            long previousLsn)
        {
            this.ConflictResolutionTimestamp = conflictResolutionTimestamp;
            this.Lsn = lsn;
            this.OperationType = operationType;
            this.PreviousLsn = previousLsn;
        }

        /// <summary>
        /// The conflict resolution timestamp.
        /// </summary>
        [JsonProperty(PropertyName = "crts", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime ConflictResolutionTimestamp { get; }

        /// <summary>
        /// The current logical sequence number.
        /// </summary>
        [JsonProperty(PropertyName = "lsn", NullValueHandling = NullValueHandling.Ignore)]
        public long Lsn { get; }

        /// <summary>
        /// The change feed operation type.
        /// </summary>
        [JsonProperty(PropertyName = "operationType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ChangeFeedOperationType OperationType { get; }

        /// <summary>
        /// The previous logical sequence number.
        /// </summary>
        [JsonProperty(PropertyName = "previousImageLSN", NullValueHandling = NullValueHandling.Ignore)]
        public long PreviousLsn { get; }

        /// <summary>
        /// Used to distinquish explicit deletes (e.g. via DeleteItem) from deletes caused by TTL expiration (a collection may define time-to-live policy for documents).
        /// </summary>
        [JsonProperty(PropertyName = "timeToLiveExpired", NullValueHandling= NullValueHandling.Ignore)]
        public bool IsTimeToLiveExpired { get; }
    }
}
