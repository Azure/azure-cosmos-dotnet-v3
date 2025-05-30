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
        public DateTime ConflictResolutionTimestamp { get; internal set; }

        /// <summary>
        /// The current change's logical sequence number.
        /// </summary>
        public long Lsn { get; internal set; }

        /// <summary>
        /// The change's feed operation type <see cref="ChangeFeedOperationType"/>.
        /// </summary>
        public ChangeFeedOperationType OperationType { get; internal set; }

        /// <summary>
        /// The previous change's logical sequence number.
        /// </summary>
        public long PreviousLsn { get; internal set; }

        /// <summary>
        /// Used to distinguish explicit deletes (e.g. via DeleteItem) from deletes caused by TTL expiration (a collection may define time-to-live policy for documents).
        /// </summary>
        public bool IsTimeToLiveExpired { get; internal set; }

        /// <summary>
        /// Applicable for delete operations only, otherwise null.
        /// The id of the previous item version. 
        /// </summary>
        public string Id { get; internal set; }

        /// <summary>
        ///  Applicable for delete operations only, otherwise null.
        /// The partition key of the previous item version. string  is the partition key property name and object is the partition key property value. All levels of hierarchy will be represented in order if a HPK is used.
        /// </summary>
        public List<(string, object)> PartitionKey { get; internal set; }
    }
}
