//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Text.Json;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// The metadata of a change feed resource with <see cref="ChangeFeedMode"/> is initialized to <see cref="ChangeFeedMode.AllOperations"/>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif 
        class ChangeFeedMetadata
    {
        /// <summary>
        /// The conflict resolved timestamp.
        /// </summary>
        [JsonProperty(PropertyName = "crts", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CRTS { get; set; }

        /// <summary>
        /// The log sequence number.
        /// </summary>
        [JsonProperty(PropertyName = "lsn", NullValueHandling = NullValueHandling.Ignore)]
        public long LSN { get; set; }

        /// <summary>
        /// The operation type.
        /// </summary>
        [JsonProperty(PropertyName = "operationType")]
        [JsonConverter(typeof(StringEnumConverter))]
        internal ChangeFeedOperationType OperationType { get; set; }

        /// <summary>
        /// The previous image log sequence number.
        /// </summary>
        [JsonProperty(PropertyName = "previousImageLSN", NullValueHandling = NullValueHandling.Ignore)]
        public long PreviousImageLSN { get; set; }
    }
}
